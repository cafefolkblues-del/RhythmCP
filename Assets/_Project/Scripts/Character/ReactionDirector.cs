using RhythmCP.Chart;
using RhythmCP.Rhythm;
using UnityEngine;

namespace RhythmCP.Character
{
    /// 플레이 이벤트 → 캐릭터 반응 상태. 우선순위(2026-09-30 확정): 행동(타격·동시타격·홀드·미스) > 감정(응원·놀람·슬픔).
    ///  - 행동은 들어오는 즉시 현재 반응을 교체한다.
    ///  - 감정은 행동 모션(홀드 유지 포함)이 끝날 때까지 대기했다가 나오고, 대기 제한을 넘기면 버린다.
    ///    그래서 미스 = 미스 모션 → 이어서 슬픔(스펙 §8 두 항목을 모두 살리는 해석).
    public class ReactionDirector : MonoBehaviour
    {
        [SerializeField] ReactionTable _table;
        [SerializeField] CharacterReactionView _view;

        JudgementSystem _judgement;
        FeverGauge _fever;

        float _actionUntil;
        bool _holding;
        float _emotionUntil;
        ReactionTable.EmotionRule _pending;
        float _pendingExpire;

        // 레인별 타격 모션 1·2 번갈아(2026-09-30 확정)
        readonly bool[] _useSecond = new bool[2];

        // 동시치기: 반대 레인이 같은 박자를 이미 맞혔으면 두 번째 타격을 동시타격 모션으로 바꾼다.
        readonly double[] _lastGeminiBeat = { double.NaN, double.NaN };

        public void Init(JudgementSystem judgement, FeverGauge fever, CharacterDefinition character)
        {
            Unsubscribe();
            _judgement = judgement;
            _fever = fever;
            _judgement.Judged += OnJudged;
            _judgement.HoldBreakChanged += OnHoldBreakChanged;
            _judgement.MashStarted += OnMashStarted;
            _judgement.MashHit += OnMashHit;
            _fever.Started += OnFeverStarted;

            _view.SetCharacter(character);
            _view.Show(ReactionState.Idle);
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_judgement != null)
            {
                _judgement.Judged -= OnJudged;
                _judgement.HoldBreakChanged -= OnHoldBreakChanged;
                _judgement.MashStarted -= OnMashStarted;
                _judgement.MashHit -= OnMashHit;
            }
            if (_fever != null) _fever.Started -= OnFeverStarted;
        }

        void OnJudged(JudgeResult r)
        {
            var note = r.Note;
            if (!r.IsHit)
            {
                if (note.IsPenaltyFree) return;
                _holding = false;
                PlayAction(ReactionState.Miss, _table.MissSec);
                QueueEmotion(EmotionTrigger.Miss);
                return;
            }

            if (note.Type == NoteType.Hold)
            {
                if (r.Part == NotePart.Head)
                {
                    _holding = true;
                    PlayAction(ReactionState.HoldStart, _table.HoldStartSec);
                }
                else
                {
                    _holding = false;
                    PlayAction(ReactionState.HoldEnd, _table.HoldEndSec);
                }
                return;
            }

            if (note.IsGemini)
            {
                int lane = (int)note.Lane;
                _lastGeminiBeat[lane] = note.Beat;
                if (_lastGeminiBeat[1 - lane] == note.Beat)
                {
                    PlayAction(ReactionState.Gemini, _table.GeminiSec);
                    return;
                }
            }

            PlayAction(NextHit(note.Lane), _table.HitSec);
        }

        void OnMashHit(PlayNote note, int hits) => PlayAction(NextHit(note.Lane), _table.HitSec);

        void OnMashStarted(PlayNote note) => QueueEmotion(EmotionTrigger.MashStart);

        void OnFeverStarted(FeverGauge fever) => QueueEmotion(EmotionTrigger.FeverStart);

        void OnHoldBreakChanged(PlayNote note, bool broken)
        {
            _holding = !broken;
            if (!broken) _view.Show(ReactionState.HoldLoop);
        }

        ReactionState NextHit(Lane lane)
        {
            int i = (int)lane;
            bool second = _useSecond[i];
            _useSecond[i] = !second;
            if (lane == Lane.Top) return second ? ReactionState.HitTop2 : ReactionState.HitTop1;
            return second ? ReactionState.HitBottom2 : ReactionState.HitBottom1;
        }

        void PlayAction(ReactionState state, float duration)
        {
            _actionUntil = Time.time + duration;
            _emotionUntil = 0f;
            _view.Show(state);
        }

        void QueueEmotion(EmotionTrigger trigger)
        {
            var rule = _table.Find(trigger);
            if (rule == null) return;
            _pending = rule;
            _pendingExpire = Time.time + rule.queueTimeoutSec;
        }

        void Update()
        {
            float now = Time.time;
            if (now < _actionUntil) return;

            if (_holding)
            {
                // 홀드 시작 모션이 끝나면 유지 자세. 홀드 중엔 감정이 끼어들지 않는다(행동 우선).
                _view.Show(ReactionState.HoldLoop);
                return;
            }

            if (_pending != null)
            {
                if (now <= _pendingExpire)
                {
                    _emotionUntil = now + _pending.durationSec;
                    _view.Show(_pending.state);
                }
                _pending = null;
            }

            if (now >= _emotionUntil) _view.Show(ReactionState.Idle);
        }
    }
}
