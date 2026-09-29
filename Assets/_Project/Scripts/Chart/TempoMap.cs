using System;
using System.Collections.Generic;

namespace RhythmCP.Chart
{
    /// 박자 ↔ 오디오 시각(초) 변환. BPM 변속 구간을 이어 붙인 구간별 선형 함수.
    /// 런타임(판정)과 에디터(격자)가 같은 변환을 쓰도록 순수 C#으로 둔다.
    public class TempoMap
    {
        readonly double[] _beats;
        readonly double[] _bpms;
        readonly double[] _secs;

        public int SegmentCount => _beats.Length;

        public TempoMap(IList<BpmPoint> points, double offsetSec)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("BPM 목록이 비어 있음");
            if (points[0].beat != 0)
                throw new ArgumentException($"첫 BPM은 beat 0이어야 함 (현재 {points[0].beat})");

            int n = points.Count;
            _beats = new double[n];
            _bpms = new double[n];
            _secs = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (points[i].bpm <= 0)
                    throw new ArgumentException($"BPM은 0보다 커야 함 (index {i}: {points[i].bpm})");
                if (i > 0 && points[i].beat <= points[i - 1].beat)
                    throw new ArgumentException($"BPM beat는 오름차순이어야 함 (index {i})");

                _beats[i] = points[i].beat;
                _bpms[i] = points[i].bpm;
                _secs[i] = i == 0
                    ? offsetSec
                    : _secs[i - 1] + (_beats[i] - _beats[i - 1]) * 60.0 / _bpms[i - 1];
            }
        }

        /// 0박 이전(음수 beat)은 첫 구간 BPM으로 외삽 — 리드인 동안 노트 위치 계산에 필요.
        public double BeatToSec(double beat)
        {
            int i = SegmentByBeat(beat);
            return _secs[i] + (beat - _beats[i]) * 60.0 / _bpms[i];
        }

        public double SecToBeat(double sec)
        {
            int i = SegmentBySec(sec);
            return _beats[i] + (sec - _secs[i]) * _bpms[i] / 60.0;
        }

        public double BpmAtBeat(double beat) => _bpms[SegmentByBeat(beat)];

        // 변속 지점은 곡당 수 개라 이진 탐색 대신 선형 탐색으로 충분.
        int SegmentByBeat(double beat)
        {
            int i = 0;
            while (i + 1 < _beats.Length && _beats[i + 1] <= beat) i++;
            return i;
        }

        int SegmentBySec(double sec)
        {
            int i = 0;
            while (i + 1 < _secs.Length && _secs[i + 1] <= sec) i++;
            return i;
        }
    }
}
