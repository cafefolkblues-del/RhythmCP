using System;
using RhythmCP.Character;
using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmCP.Rhythm
{
    /// 한 곡 플레이의 조립·시작·종료. 각 시스템은 서로를 모르고, 여기서만 연결한다.
    /// 곡/난이도는 지금은 인스펙터 지정 — 허브(M4) 곡 선택이 생기면 GameSession 싱글톤에서 받는 것으로 교체.
    /// 예외 처리(참조 누락·깨진 채보 등)는 플레이를 여러 번 돌려 실제로 터지는 지점을 보고 나중에 한 번에 정리한다.
    public class RhythmSession : MonoBehaviour
    {
        [SerializeField] SongDefinition _song;
        [SerializeField] Difficulty _difficulty;
        [SerializeField] RhythmConfig _config;

        [Tooltip("플레이어블 캐릭터. 비우면 능력 없음·표준 체력. 허브 생기면 GameSession에서 받는다.")]
        [SerializeField] CharacterDefinition _character;

        [Header("시스템")]
        [SerializeField] SongClock _clock;
        [SerializeField] RhythmInput _input;
        [SerializeField] JudgementSystem _judgement;
        [SerializeField] NoteSpawner _spawner;
        [SerializeField] ComboCounter _combo;
        [SerializeField] RhythmHealth _health;
        [SerializeField] FeverGauge _fever;
        [SerializeField] ScoreKeeper _score;
        [SerializeField] PlayStats _stats;
        [SerializeField] ReactionDirector _reactions;

        /// 체력 0이어도 결과창은 띄운다(2026-09-30 확정).
        public event Action<PlayResult> Finished;

        PlayChart _chart;
        bool _playing;
        IDisposable _abilityRuntime;

        public SongDefinition Song => _song;
        public Difficulty Difficulty => _difficulty;
        public PlayChart Chart => _chart;
        public double SongTime => _clock.SongTime;

        /// 곡 끝 = 마지막 노트 + 여유와 오디오 길이 중 늦은 쪽. 채보가 먼저 끝나도 곡은 끝까지 듣게.
        public double EndTime => Math.Max(_chart.LastNoteEndSec + _config.TailSec, _clock.ClipLength);

        void Start()
        {
            _chart = ChartLoader.Load(_song.GetChart(_difficulty));

            // 순서 주의: 콤보가 피버보다 먼저 판정 이벤트를 받는다 → 피버를 발동시킨 그 타격은 배수 없이 계산되고
            // 다음 타격부터 피버 배수가 붙는다(매 판 같은 결과가 나오도록 고정).
            // 특수능력 수치형 보정은 각 시스템 Init 인자에 얹는다(시스템은 캐릭터를 모른다).
            var ability = _character != null ? _character.Ability : null;
            var mods = ability != null ? ability.Modifiers : new RhythmModifiers();
            int maxHp = _character != null ? _character.MaxHp : _config.MaxHp;

            var windows = _config.Windows.Widen(mods.perfectWindowAddMs, mods.greatWindowAddMs, mods.goodWindowAddMs);
            _judgement.Init(_chart, _clock, _input, windows, _config.HoldTickBeats);
            _spawner.Init(_chart, _clock, _judgement, _config.ScrollSpeed);
            _combo.Init(_judgement);
            _fever.Init(_judgement, _clock, _config, mods.feverGainMultiplier, mods.feverDurationAddSec);
            _score.Init(_combo, _fever, _config.Scoring.WithComboMaxAdd(mods.comboMaxMultiplierAdd));
            _score.ExternalMultiplier = mods.scoreMultiplier;
            _stats.Init(_judgement, _chart);
            _health.Init(_judgement, maxHp,
                Mathf.RoundToInt(_config.MissDamage * mods.missDamageMultiplier),
                Mathf.RoundToInt(_config.HeartHeal * mods.heartHealMultiplier),
                _config.HoldDrainPerSec * mods.holdDrainMultiplier);
            _health.Depleted += OnHealthDepleted;
            _reactions.Init(_judgement, _fever, _character);

            // 조건형 능력(이벤트 반응·노트 추가). 모든 시스템 Init 뒤에 붙여야 구독 순서가 기존 규칙(콤보→피버)을 흐트러뜨리지 않는다.
            if (ability != null)
                _abilityRuntime = ability.Activate(new AbilityContext
                {
                    Chart = _chart, Clock = _clock, Judgement = _judgement, Fever = _fever, Spawner = _spawner,
                });

            _clock.Begin(_song.Clip, _config.LeadInSec);
            _playing = true;
        }

        void OnDestroy()
        {
            if (_health != null) _health.Depleted -= OnHealthDepleted;
            _abilityRuntime?.Dispose();
        }

        void Update()
        {
            if (_playing && _clock.SongTime >= EndTime) End(failed: false);
        }

        /// 결과창·일시정지(⑤) 공용. 씬을 다시 불러 모든 상태를 처음부터 만든다.
        /// (씬이 Build Settings에 등록돼 있어야 이름으로 로드된다.)
        public void Retry() => SceneManager.LoadScene(gameObject.scene.name);

        void OnHealthDepleted(RhythmHealth _) => End(failed: true);

        void End(bool failed)
        {
            if (!_playing) return;
            _playing = false;

            _judgement.Stop();
            _clock.Stop();

            var result = BuildResult(failed);
            Debug.Log($"[RhythmSession] 종료: {(failed ? "FAILED" : result.Grade.ToString())} · 점수 {result.Score}/{result.MaxScore} · 히트율 {result.HitRate:P1}");
            Finished?.Invoke(result);
        }

        PlayResult BuildResult(bool failed)
        {
            var rules = _config.Scoring;
            long maxScore = rules.MaxScore(_chart, _config.HoldTickBeats);
            float ratio = maxScore > 0 ? (float)_score.Score / maxScore : 0f;
            float hitRate = _stats.FinalHitRate;

            return new PlayResult
            {
                SongId = _song.SongId,
                SongTitle = _song.Title,
                Difficulty = _difficulty,
                Failed = failed,
                Score = _score.Score,
                MaxScore = maxScore,
                ScoreRatio = ratio,
                Grade = rules.Evaluate(ratio, hitRate),
                HitRate = hitRate,
                Accuracy = rules.Accuracy(_stats.Perfect, _stats.Great, _stats.Good, _stats.TotalJudgements),
                MaxCombo = _combo.MaxCombo,
                Perfect = _stats.Perfect,
                Great = _stats.Great,
                Good = _stats.Good,
                Miss = _stats.Miss,
                HasClimax = _stats.HasClimax,
                ClimaxClear = !failed && _stats.HasClimax && !_stats.ClimaxMissed,
            };
        }
    }
}
