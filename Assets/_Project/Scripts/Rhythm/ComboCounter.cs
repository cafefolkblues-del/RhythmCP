using System;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// Miss만 콤보를 끊는다(Good 유지). 홀드 틱·연타 1타도 +1, 하트는 콤보와 무관(2026-09-30 확정).
    public class ComboCounter : MonoBehaviour
    {
        public event Action<ComboCounter> Changed;

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

            if (result.IsHit) Add();
            else
            {
                Combo = 0;
                Changed?.Invoke(this);
            }
        }

        void OnHoldTicked(PlayNote note, bool held, double seconds)
        {
            if (held) Add();
        }

        void OnMashHit(PlayNote note, int hits) => Add();

        void Add()
        {
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;
            Changed?.Invoke(this);
        }
    }
}
