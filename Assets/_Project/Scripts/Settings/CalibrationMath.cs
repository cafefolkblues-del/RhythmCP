using System;
using System.Collections.Generic;

namespace RhythmCP.Settings
{
    /// 판정 오프셋 보정 계산(순수). 탭 오차(입력 − 클릭, 초) → 제안값(ms).
    public static class CalibrationMath
    {
        /// 중앙값: 평균은 한두 번 크게 헛친 탭에 끌려가서, 튀는 값에 강한 중앙값을 쓴다.
        /// 표본이 minSamples보다 적으면 null(다시 측정).
        public static float? SuggestMs(IList<double> deltasSec, int minSamples = 6)
        {
            if (deltasSec == null || deltasSec.Count < minSamples) return null;

            var sorted = new List<double>(deltasSec);
            sorted.Sort();
            int n = sorted.Count;
            double median = n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;

            float ms = (float)Math.Round(median * 1000.0);
            return Math.Max(-GameSettings.OffsetLimitMs, Math.Min(GameSettings.OffsetLimitMs, ms));
        }

        /// 가장 가까운 클릭 번호와 그 클릭에 대한 오차. 클릭 간격 절반보다 멀면 어느 클릭에도 속하지 않는 입력(false).
        public static bool TryMatchBeat(double tapTime, double firstBeatSec, double intervalSec, int beatCount, out int index, out double delta)
        {
            index = (int)Math.Round((tapTime - firstBeatSec) / intervalSec);
            delta = tapTime - (firstBeatSec + index * intervalSec);
            return index >= 0 && index < beatCount && Math.Abs(delta) <= intervalSec * 0.5;
        }
    }
}
