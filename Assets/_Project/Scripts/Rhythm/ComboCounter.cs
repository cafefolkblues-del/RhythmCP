using System;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// Miss만 콤보를 끊는다(Good 유지). 홀드 틱·연타 1타도 +1, 하트는 콤보와 무관(2026-09-30 확정).
    public class ComboCounter : MonoBehaviour
    {
        public event Action<ComboCounter> Changed;

        /// 콤보가 오른 순간(출처, 판정, 오른 뒤 콤보). 점수는 판정 이벤트가 아니라 이걸 받는다 —
        /// 판정 이벤트를 따로 받으면 구독 순서에 따라 콤보 갱신 전 값으로 배수를 계산할 수 있어서.
        public event Action<ComboSource, Judgement, int> Advanced;

        JudgementSystem _judgement;

        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }

        public void Init(JudgementSystem judgement)
        {
            // 리트라이로 Init이 다시 불려도 이중 구독되지 않게.
            Unsubscribe();
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            _judgement.HoldTicked += OnHoldTicked;
            _judgement.MashHit += OnMashHit;
            Combo = 0;
            MaxCombo = 0;
            Changed?.Invoke(this);
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_judgement == null) return;
            _judgement.Judged -= OnJudged;
            _judgement.HoldTicked -= OnHoldTicked;
            _judgement.MashHit -= OnMashHit;
        }

        void OnJudged(JudgeResult result)
        {
            if (result.Note.Type == NoteType.Heart) return;

            if (result.IsHit) Add(ComboSource.Note, result.Judgement);
            else
            {
                Combo = 0;
                Changed?.Invoke(this);
            }
        }

        void OnHoldTicked(PlayNote note, bool held, double seconds)
        {
            if (held) Add(ComboSource.HoldTick, Judgement.Perfect);
        }

        void OnMashHit(PlayNote note, int hits) => Add(ComboSource.MashHit, Judgement.Perfect);

        void Add(ComboSource source, Judgement judgement)
        {
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;
            Changed?.Invoke(this);
            Advanced?.Invoke(source, judgement, Combo);
        }
    }
}
