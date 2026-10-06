using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RhythmCP.Vn
{
    /// 에피소드 1개 = JSON 1개(M3 스펙 §2). 라인 배열이 본체.
    /// 팀원이 파일을 직접 열어도 읽히도록: 빈 값은 저장하지 않고(VnSerializer), 필드 순서는 선언 순서로 고정한다.
    [Serializable]
    public class VnEpisode
    {
        public VnMeta meta = new VnMeta();

        /// 이 에피소드 시점의 호칭. 호칭은 이야기 진행에 따라 바뀌므로(2026-10-07) 에피소드마다 둔다.
        public List<VnCastEntry> cast = new List<VnCastEntry>();

        public List<VnLine> lines = new List<VnLine>();

        public string HonorificOf(string characterId) => cast.Find(c => c.id == characterId)?.honorific;
    }

    [Serializable]
    public class VnMeta
    {
        /// 파일 이름과 같다. 공통 = ep_c01~10, 분기 = ep_yume_01~05 …
        public string id;

        /// "common" | 캐릭터 id(yume / nemu / madoromi).
        public string route = VnIds.Common;
        public int epIndex;

        /// "base"(본편) | "adult"(성인판, 레포 제외 폴더). 빌드가 이 값으로 파일셋을 가른다.
        public string edition = VnIds.Base;
    }

    [Serializable]
    public class VnCastEntry
    {
        public string id;

        /// 이 캐릭터가 플레이어(비가시 남주)를 부르는 말. mc 라인 이름표에 쓴다.
        public string honorific;
    }

    [Serializable]
    public class VnLine
    {
        /// 임포트·추가 때 한 번 붙이고 다시 매기지 않는다 — 기읽 기록·세이브 위치의 기준.
        public string id;

        /// 캐릭터 id | "narration" | "mc"(비가시 남주).
        public string speaker;

        /// mc 전용: 이름표에 누구의 호칭을 쓸지. 비우면 직전에 말한 캐릭터.
        [JsonProperty("as")]
        public string asCharacter;

        public string text;

        /// 화자 스프라이트 표정·위치. pos를 한 번 지정해야 무대에 오른다.
        public string expr;
        public string pos;

        /// 무대에서 내보낼 캐릭터들(검은색으로 페이드아웃).
        public List<string> exit;

        /// "hall" 또는 "hall.fade"(id.전환). 전환 생략 = 컷.
        public string bg;

        /// CG id | "off". 한 번 켜면 끌 때까지 유지.
        public string cg;

        /// 이 라인을 띄울 때의 화면 연출: fade · shake · colorBleed.
        public List<string> fx;

        /// BGM id | "stop". 바뀔 때 1초 크로스페이드.
        public string bgm;

        /// 표시 조건: 플래그 → ">=2" / "true" 등. 여러 개면 모두 만족(AND).
        [JsonProperty("if")]
        public Dictionary<string, string> condition;

        /// 선택지: 즉시 분기하지 않고 플래그만 올린다(관람형).
        public List<VnChoice> choice;

        public bool IsChoice => choice != null && choice.Count > 0;
    }

    [Serializable]
    public class VnChoice
    {
        public string text;

        /// 누적 플래그 증가.
        public Dictionary<string, int> add;

        /// 단일 플래그 설정.
        public Dictionary<string, bool> set;
    }

    public static class VnIds
    {
        public const string Narration = "narration";
        public const string Mc = "mc";
        public const string Common = "common";
        public const string Base = "base";
        public const string Adult = "adult";
        public const string Off = "off";
        public const string Stop = "stop";

        public static readonly string[] Positions = { "left", "center", "right" };
        public static readonly string[] Effects = { "fade", "shake", "colorBleed" };
        public static readonly string[] Transitions = { "cut", "fade" };
    }
}
