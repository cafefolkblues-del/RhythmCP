using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace RhythmCP.Chart
{
    /// 채보 JSON 읽기/쓰기. 에디터 도구와 런타임이 같은 설정을 쓰게 한 곳에 모은다.
    public static class ChartSerializer
    {
        // StringEnumConverter: JsonUtility는 enum을 숫자로 써서 사람이/Claude가 채보를 읽고 고치기 어렵다.
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };

        public static ChartData FromJson(string json) => JsonConvert.DeserializeObject<ChartData>(json, Settings);

        public static string ToJson(ChartData chart) => JsonConvert.SerializeObject(chart, Settings);
    }
}
