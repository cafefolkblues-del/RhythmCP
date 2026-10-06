using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace RhythmCP.Vn
{
    /// 에피소드·플래그 등록부 JSON 읽기/쓰기. 저장 전에 빈 목록·빈 문자열을 null로 바꿔 파일에 안 나오게 한다(가독성).
    public static class VnSerializer
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };

        public static VnEpisode ReadEpisode(string json) => JsonConvert.DeserializeObject<VnEpisode>(json, Settings);

        public static string WriteEpisode(VnEpisode episode)
        {
            foreach (var line in episode.lines) Tidy(line);
            return JsonConvert.SerializeObject(episode, Settings);
        }

        public static VnFlagRegistry ReadFlags(string json) => JsonConvert.DeserializeObject<VnFlagRegistry>(json, Settings) ?? new VnFlagRegistry();

        public static string WriteFlags(VnFlagRegistry registry) => JsonConvert.SerializeObject(registry, Settings);

        public static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value, Settings), Settings);

        static void Tidy(VnLine l)
        {
            l.asCharacter = Blank(l.asCharacter);
            l.expr = Blank(l.expr);
            l.pos = Blank(l.pos);
            l.bg = Blank(l.bg);
            l.cg = Blank(l.cg);
            l.bgm = Blank(l.bgm);
            l.exit = l.exit != null && l.exit.Count > 0 ? l.exit : null;
            l.fx = l.fx != null && l.fx.Count > 0 ? l.fx : null;
            l.condition = l.condition != null && l.condition.Count > 0 ? l.condition : null;
            if (l.choice != null)
            {
                foreach (var c in l.choice)
                {
                    c.add = c.add != null && c.add.Count > 0 ? c.add : null;
                    c.set = c.set != null && c.set.Count > 0 ? c.set : null;
                }
                l.choice = l.choice.Count > 0 ? l.choice : null;
            }
        }

        static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

        /// 새 라인 id: 기존 L### 중 가장 큰 번호 + 1. 중간에 끼워 넣어도 기존 id는 그대로(기읽·세이브 기준 보존).
        public static string NextLineId(IEnumerable<VnLine> lines)
        {
            int max = 0;
            foreach (var l in lines)
                if (l.id != null && l.id.StartsWith("L") && int.TryParse(l.id.Substring(1), out int n) && n > max) max = n;
            return $"L{max + 1:000}";
        }
    }
}
