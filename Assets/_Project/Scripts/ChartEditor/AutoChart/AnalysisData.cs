using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace RhythmCP.ChartEditing
{
    /// analysis.json(채보 스펙 §2② 분석 사이드카) — Tools/Analysis/analyze.py가 만든다.
    /// 필드 이름은 JSON 그대로(파이썬 쪽과 1:1). 자동 배치(AutoCharter)와 Claude의 HARD 작업이 같이 읽는다.
    [Serializable]
    public class AnalysisData
    {
        public int version;
        public string audio;
        public string audioHash;
        public int durationMs;

        public double bpm;
        public int offsetMs;
        public double bpmConfidence;
        public List<BpmCandidate> bpmCandidates = new List<BpmCandidate>();
        public double tempoDrift;
        public double beatJitterMs;
        public List<string> warnings = new List<string>();

        public List<Onset> onsets = new List<Onset>();
        public List<Segment> segments = new List<Segment>();

        public double DurationSec => durationMs / 1000.0;

        [Serializable]
        public class BpmCandidate
        {
            public double bpm;
            public double score;
        }

        [Serializable]
        public class Onset
        {
            public int tMs;
            public double strength;
            public string band;

            /// 다음 온셋 전까지 소리가 −6dB 안에서 이어진 시간(홀드 판정 재료, 스펙에 없는 추가 필드).
            public int sustainMs;

            public double Sec => tMs / 1000.0;
            public double SustainSec => sustainMs / 1000.0;
        }

        [Serializable]
        public class Segment
        {
            public int startMs;
            public int endMs;
            public string label;
            public double energy;

            public double StartSec => startMs / 1000.0;
            public double EndSec => endMs / 1000.0;
        }

        public static AnalysisData Read(string path) =>
            File.Exists(path) ? JsonConvert.DeserializeObject<AnalysisData>(File.ReadAllText(path)) : null;
    }
}
