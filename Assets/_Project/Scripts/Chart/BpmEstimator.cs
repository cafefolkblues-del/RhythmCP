using System;
using System.Collections.Generic;

namespace RhythmCP.Chart
{
    /// 탭 시각들 → BPM·첫 박 오프셋. BPM 탭 창(에디터)과 채보 에디터가 같은 계산을 쓰게 모은 순수 함수.
    /// 탭 간격 평균 대신 '탭 번호 → 시각' 직선 회귀: 한 번 삐끗한 탭이 그대로 섞이는 평균보다 덜 흔들린다.
    public static class BpmEstimator
    {
        public const int MinTaps = 4;

        public static bool TryFit(IReadOnlyList<double> taps, out double bpm, out double offsetSec, out double jitterMs)
        {
            bpm = offsetSec = jitterMs = 0;
            int n = taps?.Count ?? 0;
            if (n < MinTaps) return false;

            double meanI = (n - 1) / 2.0, meanT = 0.0;
            foreach (double t in taps) meanT += t;
            meanT /= n;

            double sxy = 0.0, sxx = 0.0;
            for (int i = 0; i < n; i++)
            {
                sxy += (i - meanI) * (taps[i] - meanT);
                sxx += (i - meanI) * (i - meanI);
            }

            double period = sxy / sxx;
            if (period <= 0.0) return false;
            double intercept = meanT - period * meanI;

            double sse = 0.0;
            for (int i = 0; i < n; i++)
            {
                double r = taps[i] - (intercept + period * i);
                sse += r * r;
            }

            bpm = 60.0 / period;
            offsetSec = Mod(intercept, period);
            jitterMs = Math.Sqrt(sse / n) * 1000.0;
            return true;
        }

        /// BPM을 정수 등으로 고정했을 때 그 BPM에 맞는 오프셋.
        public static double OffsetFor(IReadOnlyList<double> taps, double bpm)
        {
            double period = 60.0 / bpm;
            double sum = 0.0;
            for (int i = 0; i < taps.Count; i++) sum += taps[i] - i * period;
            return Mod(sum / taps.Count, period);
        }

        static double Mod(double value, double period) => ((value % period) + period) % period;
    }
}
