using System.Collections.Generic;
using System.Linq;
using RhythmCP.Vn;

namespace RhythmCP.VnEditing
{
    public enum VnIssueSeverity
    {
        Error,
        Warning,
        Info,
    }

    public readonly struct VnIssue
    {
        public readonly VnIssueSeverity Severity;

        /// -1 = 에피소드 전체(메타·출연진).
        public readonly int LineIndex;
        public readonly string Message;

        public VnIssue(VnIssueSeverity severity, int lineIndex, string message)
        {
            Severity = severity;
            LineIndex = lineIndex;
            Message = message;
        }
    }

    /// 검사기가 대조할 id 목록. 에디터는 카탈로그에서 만든다. 에셋 목록이 null이면 그 검사는 건너뜀(테스트용).
    public class VnKnownIds
    {
        public HashSet<string> Characters = new HashSet<string>();
        public HashSet<string> Backgrounds;
        public HashSet<string> Cgs;
        public HashSet<string> Bgms;
    }

    /// 에피소드 검사기(순수). 오류 = 재생이 틀어지는 것, 경고 = 재생은 되지만 의도와 다를 가능성, 정보 = 참고.
    public static class VnValidator
    {
        public static List<VnIssue> Validate(VnEpisode ep, VnFlagRegistry registry, VnKnownIds known)
        {
            var issues = new List<VnIssue>();
            void Add(VnIssueSeverity s, int i, string m) => issues.Add(new VnIssue(s, i, m));

            // ---- 메타·출연진
            if (string.IsNullOrEmpty(ep.meta.id)) Add(VnIssueSeverity.Error, -1, "meta.id가 비어 있음");
            if (ep.meta.edition != VnIds.Base && ep.meta.edition != VnIds.Adult)
                Add(VnIssueSeverity.Error, -1, $"edition은 base/adult: \"{ep.meta.edition}\"");
            if (ep.meta.route != VnIds.Common && !known.Characters.Contains(ep.meta.route))
                Add(VnIssueSeverity.Error, -1, $"route는 common 또는 캐릭터 id: \"{ep.meta.route}\"");
            foreach (var c in ep.cast)
            {
                if (!known.Characters.Contains(c.id)) Add(VnIssueSeverity.Warning, -1, $"출연진 \"{c.id}\"가 카탈로그에 없음");
                if (string.IsNullOrWhiteSpace(c.honorific)) Add(VnIssueSeverity.Warning, -1, $"\"{c.id}\" 호칭이 비어 있음");
            }

            var ids = new HashSet<string>();
            string lastCharacter = null;

            for (int i = 0; i < ep.lines.Count; i++)
            {
                var l = ep.lines[i];

                // ---- id
                if (string.IsNullOrEmpty(l.id)) Add(VnIssueSeverity.Error, i, "라인 id 없음");
                else if (!ids.Add(l.id)) Add(VnIssueSeverity.Error, i, $"라인 id 중복: {l.id}");

                // ---- 화자
                bool isMc = l.speaker == VnIds.Mc;
                bool isNarration = l.speaker == VnIds.Narration;
                if (string.IsNullOrEmpty(l.speaker)) Add(VnIssueSeverity.Error, i, "화자 없음");
                else if (!isMc && !isNarration)
                {
                    if (!known.Characters.Contains(l.speaker)) Add(VnIssueSeverity.Error, i, $"모르는 화자: \"{l.speaker}\"");
                    lastCharacter = l.speaker;
                }

                if (!string.IsNullOrEmpty(l.asCharacter))
                {
                    if (!isMc) Add(VnIssueSeverity.Warning, i, "as는 mc 라인에서만 쓰인다");
                    else if (ep.HonorificOf(l.asCharacter) == null) Add(VnIssueSeverity.Error, i, $"as \"{l.asCharacter}\"의 호칭이 출연진에 없음");
                }
                else if (isMc)
                {
                    // if로 가려진 줄 때문에 실제 재생에선 달라질 수 있지만, 앞에 캐릭터가 하나도 없으면 확실히 "???".
                    if (lastCharacter == null) Add(VnIssueSeverity.Error, i, "mc 호칭을 정할 캐릭터가 앞에 없음 — as 지정");
                    else if (ep.HonorificOf(lastCharacter) == null) Add(VnIssueSeverity.Error, i, $"\"{lastCharacter}\"의 호칭이 출연진에 없음");
                }

                if (string.IsNullOrWhiteSpace(l.text)) Add(VnIssueSeverity.Warning, i, "텍스트 비어 있음");

                // ---- 연출
                if ((isMc || isNarration) && (!string.IsNullOrEmpty(l.pos) || !string.IsNullOrEmpty(l.expr)))
                    Add(VnIssueSeverity.Warning, i, "mc·나레이션의 pos/expr은 무시된다");
                if (!string.IsNullOrEmpty(l.pos) && !VnIds.Positions.Contains(l.pos))
                    Add(VnIssueSeverity.Error, i, $"pos는 left/center/right: \"{l.pos}\"");

                if (!string.IsNullOrEmpty(l.bg))
                {
                    int dot = l.bg.LastIndexOf('.');
                    string bgId = dot > 0 ? l.bg.Substring(0, dot) : l.bg;
                    if (dot > 0 && !VnIds.Transitions.Contains(l.bg.Substring(dot + 1)))
                        Add(VnIssueSeverity.Error, i, $"배경 전환은 cut/fade: \"{l.bg}\"");
                    if (known.Backgrounds != null && !known.Backgrounds.Contains(bgId))
                        Add(VnIssueSeverity.Warning, i, $"카탈로그에 없는 배경: {bgId}");
                }
                if (!string.IsNullOrEmpty(l.cg) && l.cg != VnIds.Off && known.Cgs != null && !known.Cgs.Contains(l.cg))
                    Add(VnIssueSeverity.Warning, i, $"카탈로그에 없는 CG: {l.cg}");
                if (!string.IsNullOrEmpty(l.bgm) && l.bgm != VnIds.Stop && known.Bgms != null && !known.Bgms.Contains(l.bgm))
                    Add(VnIssueSeverity.Warning, i, $"카탈로그에 없는 BGM: {l.bgm}");
                if (l.fx != null)
                    foreach (var fx in l.fx)
                        if (!VnIds.Effects.Contains(fx)) Add(VnIssueSeverity.Error, i, $"모르는 fx: \"{fx}\"");
                if (l.exit != null)
                    foreach (var id in l.exit)
                        if (!known.Characters.Contains(id)) Add(VnIssueSeverity.Error, i, $"퇴장 대상이 캐릭터가 아님: \"{id}\"");

                // ---- 조건
                if (l.condition != null)
                    foreach (var kv in l.condition)
                    {
                        var def = registry.Find(kv.Key);
                        if (def == null) { Add(VnIssueSeverity.Error, i, $"등록 안 된 플래그: {kv.Key}"); continue; }
                        bool boolExpr = VnCondition.IsBoolExpr(kv.Value);
                        if (!boolExpr && !VnCondition.TryParse(kv.Value, out _, out _))
                            Add(VnIssueSeverity.Error, i, $"조건 문법: {kv.Key} \"{kv.Value}\"");
                        else if (boolExpr == def.IsCounter)
                            Add(VnIssueSeverity.Error, i, def.IsCounter ? $"{kv.Key}는 누적 — 숫자 비교로" : $"{kv.Key}는 단일 — true/false로");
                    }

                // ---- 선택지
                if (l.choice != null)
                {
                    if (l.choice.Count == 0) Add(VnIssueSeverity.Error, i, "선택지가 비어 있음");
                    if (l.choice.Count > 9) Add(VnIssueSeverity.Warning, i, "선택지 9개 초과 — 숫자키로 못 고름");
                    for (int c = 0; c < l.choice.Count; c++)
                    {
                        var ch = l.choice[c];
                        if (string.IsNullOrWhiteSpace(ch.text)) Add(VnIssueSeverity.Error, i, $"선택지 {c + 1} 텍스트 없음");
                        if (ch.add != null)
                            foreach (var kv in ch.add)
                            {
                                var def = registry.Find(kv.Key);
                                if (def == null) Add(VnIssueSeverity.Error, i, $"선택지 {c + 1}: 등록 안 된 플래그 {kv.Key}");
                                else if (!def.IsCounter) Add(VnIssueSeverity.Error, i, $"선택지 {c + 1}: {kv.Key}는 단일 — add 대신 set");
                            }
                        if (ch.set != null)
                            foreach (var kv in ch.set)
                            {
                                var def = registry.Find(kv.Key);
                                if (def == null) Add(VnIssueSeverity.Error, i, $"선택지 {c + 1}: 등록 안 된 플래그 {kv.Key}");
                                else if (def.IsCounter) Add(VnIssueSeverity.Error, i, $"선택지 {c + 1}: {kv.Key}는 누적 — set 대신 add");
                            }
                    }
                }
            }

            // ---- 등록부
            foreach (var group in registry.flags.GroupBy(f => f.id).Where(g => g.Count() > 1))
                Add(VnIssueSeverity.Error, -1, $"flags.json에 같은 플래그가 두 번: {group.Key}");
            foreach (var f in registry.flags)
                if (f.kind != VnFlagDef.Counter && f.kind != VnFlagDef.Bool)
                    Add(VnIssueSeverity.Error, -1, $"플래그 {f.id} 종류는 counter/bool: \"{f.kind}\"");

            return issues;
        }
    }
}
