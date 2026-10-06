using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace RhythmCP.ChartEditing
{
    /// 자동 채보 패턴 라이브러리(Data/AutoChart/patterns_{난이도}.json). 1마디 단위 리듬 + 레인 템플릿.
    /// 텍스트라 사람·Claude가 직접 추가·수정하고, 에디터의 "패턴으로 저장"이 harvest 항목을 덧붙인다.
    [Serializable]
    public class PatternLibrary
    {
        public string difficulty;
        public string note;
        public List<RhythmPattern> rhythms = new List<RhythmPattern>();
        public List<LanePattern> lanes = new List<LanePattern>();

        [Serializable]
        public class RhythmPattern
        {
            public string id;
            public List<double> beats = new List<double>();

            /// groove(기본) / rest(숨 쉬는 마디) / fill(마무리) / pickup(다음 마디로 당김) — 마디 위치별 가·감점에 쓴다.
            public string role = "groove";

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool triplet;

            public string source = "seed";
        }

        [Serializable]
        public class LanePattern
        {
            public string id;

            /// "A"/"B" 문자열. 노트 수만큼 반복해 적용(A = 마디 시작 레인).
            public string lanes;

            public string source = "seed";
        }

        public static PatternLibrary Parse(string json) => JsonConvert.DeserializeObject<PatternLibrary>(json);

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        /// 에디터에서 수집한 패턴 추가. 같은 리듬·레인이 이미 있으면 그 ID를 돌려주고 추가하지 않는다.
        public (string rhythmId, string laneId, bool added) Harvest(List<double> beats, string lanes)
        {
            bool added = false;
            var rhythm = rhythms.FirstOrDefault(r => SameBeats(r.beats, beats));
            if (rhythm == null)
            {
                rhythm = new RhythmPattern
                {
                    id = $"h_{rhythms.Count(r => r.source == "harvest") + 1}",
                    beats = beats,
                    role = "groove",
                    triplet = beats.Any(b => Math.Abs(b * 4 - Math.Round(b * 4)) > 1e-3),
                    source = "harvest",
                };
                rhythms.Add(rhythm);
                added = true;
            }

            var lane = this.lanes.FirstOrDefault(l => l.lanes == lanes);
            if (lane == null && lanes.Length > 0)
            {
                lane = new LanePattern { id = lanes, lanes = lanes, source = "harvest" };
                this.lanes.Add(lane);
                added = true;
            }
            return (rhythm.id, lane?.id, added);
        }

        static bool SameBeats(List<double> a, List<double> b) =>
            a.Count == b.Count && a.Zip(b, (x, y) => Math.Abs(x - y) < 1e-6).All(v => v);

        public static PatternLibrary Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
    }
}
