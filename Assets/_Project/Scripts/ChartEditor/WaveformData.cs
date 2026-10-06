using System;
using System.IO;

namespace RhythmCP.ChartEditing
{
    /// 파형: 일정 간격마다 최대 진폭(0~1). 곡은 압축 상태로 메모리에 올라가 AudioClip.GetData가 실패하므로(2026-10-06 확인),
    /// 에디터 런처가 플레이 진입 전에 계산해 Library에 캐시로 쓰고, 채보 에디터는 그 파일만 읽는다.
    public class WaveformData
    {
        public const string CacheDir = "Library/RhythmCP/Waveforms";

        readonly float[] _peaks;
        readonly double _secondsPerPeak;

        public WaveformData(float[] peaks, double secondsPerPeak)
        {
            _peaks = peaks;
            _secondsPerPeak = secondsPerPeak;
        }

        public float PeakBetween(double fromSec, double toSec)
        {
            int a = Math.Max(0, (int)(fromSec / _secondsPerPeak));
            int b = Math.Min(_peaks.Length - 1, (int)(toSec / _secondsPerPeak));
            float max = 0f;
            for (int i = a; i <= b; i++) if (_peaks[i] > max) max = _peaks[i];
            return max;
        }

        public static string CachePath(string audioGuid) => Path.Combine(CacheDir, audioGuid + ".wave");

        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var w = new BinaryWriter(File.Create(path));
            w.Write(_secondsPerPeak);
            w.Write(_peaks.Length);
            foreach (var p in _peaks) w.Write(p);
        }

        public static WaveformData Read(string path)
        {
            if (!File.Exists(path)) return null;
            using var r = new BinaryReader(File.OpenRead(path));
            double spp = r.ReadDouble();
            var peaks = new float[r.ReadInt32()];
            for (int i = 0; i < peaks.Length; i++) peaks[i] = r.ReadSingle();
            return new WaveformData(peaks, spp);
        }

        /// 원본 샘플(모노로 섞인) → 피크 배열.
        public static WaveformData FromSamples(float[] interleaved, int channels, int frequency, double secondsPerPeak = 0.005)
        {
            int frames = interleaved.Length / Math.Max(1, channels);
            int perPeak = Math.Max(1, (int)(frequency * secondsPerPeak));
            var peaks = new float[(frames + perPeak - 1) / perPeak];
            for (int f = 0; f < frames; f++)
            {
                float v = 0f;
                for (int c = 0; c < channels; c++) v = Math.Max(v, Math.Abs(interleaved[f * channels + c]));
                int i = f / perPeak;
                if (v > peaks[i]) peaks[i] = v;
            }
            return new WaveformData(peaks, (double)perPeak / frequency);
        }
    }
}
