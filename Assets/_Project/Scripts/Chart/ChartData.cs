using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace RhythmCP.Chart
{
    public enum NoteType
    {
        Tap,
        Hold,
        Mash,
        Heart,

        /// 회피 장애물 — 스펙상 필드만 확보. 로더가 경고 후 건너뛴다.
        Obstacle,
    }

    public enum Lane
    {
        Top,
        Bottom,
    }

    public enum Difficulty
    {
        Easy,
        Hard,
    }

    /// 채보 파일(JSON) 한 개 = 곡 하나의 난이도 하나.
    /// 시간은 전부 박자(beat) 단위. 초는 TempoMap이 bpms + offsetSec로 계산한다.
    [Serializable]
    public class ChartData
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string songId;
        public Difficulty difficulty;

        /// 오디오 파일 안에서 0박이 울리는 시각(초). 유저 캘리브레이션과는 별개.
        public double offsetSec;

        /// 첫 항목은 반드시 beat 0. beat 오름차순.
        public List<BpmPoint> bpms = new List<BpmPoint>();

        /// 클라이맥스 무미스 구간. 채보당 하나, 없으면 null.
        public ClimaxMarker climax;

        public List<NoteData> notes = new List<NoteData>();
    }

    [Serializable]
    public class BpmPoint
    {
        public double beat;
        public double bpm;
    }

    [Serializable]
    public class ClimaxMarker
    {
        public double startBeat;
        public double endBeat;
    }

    [Serializable]
    public class NoteData
    {
        public NoteType type;
        public Lane lane;
        public double beat;

        /// Hold / Mash 전용. 다른 종류는 0이고 파일에 쓰지 않는다.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public double endBeat;

        /// 스크롤 속도 배율. 빠른 노트 = 1.5. 1이면 파일에 쓰지 않는다.
        /// IgnoreAndPopulate: 필드가 빠진 JSON을 읽을 때 CLR 기본값 0이 아니라 1로 채우려고.
        [DefaultValue(1f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public float speed = 1f;
    }
}
