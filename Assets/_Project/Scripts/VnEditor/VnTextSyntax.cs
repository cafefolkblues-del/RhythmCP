using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RhythmCP.Vn;

namespace RhythmCP.VnEditing
{
    /// 조건·선택지 효과 ↔ 사람이 읽는 글자.
    ///   조건 표시 : { "foreshadow.hall": ">=2", "saw_lock": "true" } → "foreshadow.hall >= 2, saw_lock = true"
    ///              (입력은 조건 행 UI — 이 글자는 비교·표시용)
    ///   선택지 효과: "foreshadow.hall +1, saw_lock = true" ↔ add { foreshadow.hall: 1 }, set { saw_lock: true }
    ///              선택지 하나에 효과가 여러 개일 수 있어 한 칸 글자 입력. 오타 플래그는 검사기가 잡는다.
    public static class VnTextSyntax
    {
        static readonly Regex AddItem = new Regex(@"^\s*([\w.]+)\s*([+-])\s*(\d+)\s*$");
        static readonly Regex SetItem = new Regex(@"^\s*([\w.]+)\s*=\s*(true|false)\s*$", RegexOptions.IgnoreCase);

        // ---------------- 조건

        public static string FormatCondition(Dictionary<string, string> condition)
        {
            if (condition == null) return string.Empty;
            return string.Join(", ", condition.Select(kv => VnCondition.IsBoolExpr(kv.Value)
                ? $"{kv.Key} = {kv.Value.Trim().ToLowerInvariant()}"
                : VnCondition.TryParse(kv.Value, out var op, out var v) ? $"{kv.Key} {op} {v}" : $"{kv.Key} {kv.Value}"));
        }

        // ---------------- 선택지 효과

        public static string FormatEffects(VnChoice choice)
        {
            var parts = new List<string>();
            if (choice.add != null) parts.AddRange(choice.add.Select(kv => $"{kv.Key} {(kv.Value < 0 ? "-" : "+")}{System.Math.Abs(kv.Value)}"));
            if (choice.set != null) parts.AddRange(choice.set.Select(kv => $"{kv.Key} = {(kv.Value ? "true" : "false")}"));
            return string.Join(", ", parts);
        }

        public static bool TryParseEffects(string text, out Dictionary<string, int> add, out Dictionary<string, bool> set, out string error)
        {
            add = null;
            set = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            foreach (var item in Split(text))
            {
                var a = AddItem.Match(item);
                if (a.Success)
                {
                    add ??= new Dictionary<string, int>();
                    int n = int.Parse(a.Groups[3].Value, CultureInfo.InvariantCulture);
                    add[a.Groups[1].Value] = a.Groups[2].Value == "-" ? -n : n;
                    continue;
                }
                var s = SetItem.Match(item);
                if (s.Success)
                {
                    set ??= new Dictionary<string, bool>();
                    set[s.Groups[1].Value] = bool.Parse(s.Groups[2].Value);
                    continue;
                }
                error = $"효과 문법: \"{item}\" (예: foreshadow.hall +1, saw_lock = true)";
                return false;
            }
            return true;
        }

        // ---------------- 목록(퇴장·fx)

        public static string FormatList(List<string> list) => list == null ? string.Empty : string.Join(", ", list);

        public static List<string> ParseList(string text)
        {
            var list = Split(text).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            return list.Count == 0 ? null : list;
        }

        static IEnumerable<string> Split(string text) => (text ?? string.Empty).Split(',').Where(s => !string.IsNullOrWhiteSpace(s));
    }
}
