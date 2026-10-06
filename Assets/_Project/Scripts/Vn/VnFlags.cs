using System;
using System.Collections.Generic;
using System.Globalization;

namespace RhythmCP.Vn
{
    /// 전역 플래그 등록부(VN/flags.json). 스크립트에서 쓰는 플래그는 여기 선언해야 한다 — 검사기가 오타를 잡는다.
    [Serializable]
    public class VnFlagRegistry
    {
        public List<VnFlagDef> flags = new List<VnFlagDef>();

        public VnFlagDef Find(string id) => flags.Find(f => f.id == id);
    }

    [Serializable]
    public class VnFlagDef
    {
        public string id;

        /// "counter"(누적, add) | "bool"(단일, set).
        public string kind = Counter;
        public string description;

        public const string Counter = "counter";
        public const string Bool = "bool";

        public bool IsCounter => kind == Counter;
    }

    /// 플레이 중 플래그 값. 누적 = 정수, 단일 = 참/거짓. 라우트는 플래그가 아니다(리듬 카운트, M4).
    [Serializable]
    public class VnFlags
    {
        public Dictionary<string, int> counters = new Dictionary<string, int>();
        public Dictionary<string, bool> bools = new Dictionary<string, bool>();

        public int Counter(string id) => counters.TryGetValue(id, out var v) ? v : 0;
        public bool Bool(string id) => bools.TryGetValue(id, out var v) && v;

        public void Apply(VnChoice choice)
        {
            if (choice.add != null) foreach (var kv in choice.add) counters[kv.Key] = Counter(kv.Key) + kv.Value;
            if (choice.set != null) foreach (var kv in choice.set) bools[kv.Key] = kv.Value;
        }
    }

    /// 라인 표시 조건(if) 평가. 값 문법: 숫자 비교 ">=2" "<=1" "==0" ">3" "<3" "!=1", 단일 플래그 "true"/"false".
    public static class VnCondition
    {
        static readonly string[] Ops = { ">=", "<=", "==", "!=", ">", "<" };

        public static bool Evaluate(Dictionary<string, string> condition, VnFlags flags)
        {
            if (condition == null) return true;
            foreach (var kv in condition)
                if (!TryEvaluate(kv.Key, kv.Value, flags, out bool ok) || !ok) return false;
            return true;
        }

        /// 문법이 틀리면 false(검사기가 잡는다). 맞으면 결과를 result에.
        public static bool TryEvaluate(string flag, string expr, VnFlags flags, out bool result)
        {
            result = false;
            if (expr == null) return false;
            string e = expr.Trim();

            if (bool.TryParse(e, out bool want))
            {
                result = flags.Bool(flag) == want;
                return true;
            }

            if (!TryParse(e, out string op, out int value)) return false;
            int v = flags.Counter(flag);
            result = op switch
            {
                ">=" => v >= value,
                "<=" => v <= value,
                "==" => v == value,
                "!=" => v != value,
                ">" => v > value,
                _ => v < value,
            };
            return true;
        }

        public static bool TryParse(string expr, out string op, out int value)
        {
            op = null;
            value = 0;
            if (expr == null) return false;
            string e = expr.Trim();
            foreach (var o in Ops)
            {
                if (!e.StartsWith(o)) continue;
                op = o;
                return int.TryParse(e.Substring(o.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
            return false;
        }

        public static bool IsBoolExpr(string expr) => expr != null && bool.TryParse(expr.Trim(), out _);
    }
}
