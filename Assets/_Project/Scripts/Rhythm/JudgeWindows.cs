using System;

namespace RhythmCP.Rhythm
{
    public enum Judgement
    {
        Perfect,
        Great,
        Good,
        Miss,
    }

    /// 타이밍 오차 → 판정 등급. 순수 계산이라 EditMode 테스트로 경계값을 고정한다.
    public readonly struct JudgeWindows
    {
        public readonly double Perfect;
        public readonly double Great;
        public readonly double Good;

        public JudgeWindows(double perfect, double great, double good)
        {
            Perfect = perfect;
            Great = great;
            Good = good;
        }

        /// delta = 입력 시각 − 노트 시각 (음수 = 빠름).
        /// Good 윈도우 밖이면 false — 그 입력은 이 노트와 무관한 것으로 보고 무시한다(빈 타격 벌칙 없음).
        public bool TryEvaluate(double delta, out Judgement judgement)
        {
            double abs = Math.Abs(delta);
            if (abs <= Perfect) { judgement = Judgement.Perfect; return true; }
            if (abs <= Great) { judgement = Judgement.Great; return true; }
            if (abs <= Good) { judgement = Judgement.Good; return true; }
            judgement = Judgement.Miss;
            return false;
        }

        /// 노트가 판정선을 이만큼 지나면 Miss.
        public bool IsTooLate(double delta) => delta > Good;
    }
}
