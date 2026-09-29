using System;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// Miss만 콤보를 끊는다(Good 유지 — 2026-09-30 확정).
    public class ComboCounter : MonoBehaviour
    {
        public event Action<ComboCounter> Changed;

        JudgementSystem _judgement;

        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }

        public void Init(JudgementSystem judgement)
        {
            // 리트라이로 Init이 다시 불려도 이중 구독되지 않게.
            if (_judgement != null) _judgement.Judged -= OnJudged;
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            Combo = 0;
            MaxCombo = 0;
            Changed?.Invoke(this);
        }

        void OnDestroy()
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
        }

        void OnJudged(JudgeResult result)
        {
            if (result.Judgement == Judgement.Miss)
            {
                Combo = 0;
            }
            else
            {
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;
            }
            Changed?.Invoke(this);
        }
    }
}
