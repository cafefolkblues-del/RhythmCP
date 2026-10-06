using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmCP.Vn;
using RhythmCP.VnEditing;

namespace RhythmCP.Tests
{
    public class VnEditorTests
    {
        static VnLine L(string id, string speaker, string text = "…") => new VnLine { id = id, speaker = speaker, text = text };

        static VnEpisode Episode(params VnLine[] lines) => new VnEpisode
        {
            meta = new VnMeta { id = "ep_c01" },
            cast = new List<VnCastEntry> { new VnCastEntry { id = "yume", honorific = "식객" } },
            lines = lines.ToList(),
        };

        static VnFlagRegistry Registry() => new VnFlagRegistry
        {
            flags = new List<VnFlagDef>
            {
                new VnFlagDef { id = "foreshadow.hall", kind = VnFlagDef.Counter },
                new VnFlagDef { id = "saw_lock", kind = VnFlagDef.Bool },
            },
        };

        static VnKnownIds Known() => new VnKnownIds { Characters = new HashSet<string> { "yume", "nemu", "madoromi" } };

        static List<string> Messages(VnEpisode ep, VnFlagRegistry reg = null) =>
            VnValidator.Validate(ep, reg ?? Registry(), Known()).Select(i => $"{i.Severity}:{i.LineIndex}:{i.Message}").ToList();

        // ---------------- 문서

        [Test]
        public void Document_AssignsMissingAndDuplicateIds_KeepsExisting()
        {
            var doc = new VnDocument(Episode(L("L005", "yume"), L(null, "yume"), L("L005", "nemu")));
            CollectionAssert.AreEqual(new[] { "L005", "L006", "L007" }, doc.Episode.lines.Select(l => l.id).ToArray());
            Assert.IsTrue(doc.IsDirty);
        }

        [Test]
        public void Document_InsertDuplicateMoveDelete_UndoRedo()
        {
            var doc = new VnDocument(Episode(L("L001", "yume", "a"), L("L002", "yume", "b")));
            Assert.IsFalse(doc.IsDirty);

            int i = doc.InsertLine(0, "nemu");
            Assert.AreEqual(1, i);
            Assert.AreEqual("L003", doc.Episode.lines[1].id, "중간에 넣어도 기존 id는 그대로, 새 id는 최대+1");
            Assert.AreEqual("nemu", doc.Episode.lines[1].speaker);

            int d = doc.DuplicateLine(0);
            Assert.AreEqual("L004", doc.Episode.lines[d].id);
            Assert.AreEqual("a", doc.Episode.lines[d].text);

            Assert.AreEqual(0, doc.MoveLine(1, -1));
            Assert.AreEqual("L004", doc.Episode.lines[0].id);
            Assert.AreEqual(0, doc.MoveLine(0, -1), "맨 위에서 위로는 그대로");

            doc.DeleteLines(new[] { 0, 3 });
            CollectionAssert.AreEqual(new[] { "L001", "L003" }, doc.Episode.lines.Select(l => l.id).ToArray());

            doc.Undo();
            Assert.AreEqual(4, doc.Episode.lines.Count);
            doc.Redo();
            Assert.AreEqual(2, doc.Episode.lines.Count);
        }

        [Test]
        public void Document_SetCondition_AppliesToAllSelected_AsSeparateCopies()
        {
            var doc = new VnDocument(Episode(L("L001", "yume"), L("L002", "yume"), L("L003", "yume")));
            doc.SetCondition(new[] { 0, 2 }, new Dictionary<string, string> { { "saw_lock", "true" } });
            Assert.AreEqual("true", doc.Episode.lines[0].condition["saw_lock"]);
            Assert.IsNull(doc.Episode.lines[1].condition);
            doc.Episode.lines[0].condition["saw_lock"] = "false";
            Assert.AreEqual("true", doc.Episode.lines[2].condition["saw_lock"], "줄마다 따로 복사");
            doc.SetCondition(new[] { 0 }, null);
            Assert.IsNull(doc.Episode.lines[0].condition);
        }

        // ---------------- 글자 문법

        [Test]
        public void Syntax_FormatCondition()
        {
            var c = new Dictionary<string, string> { { "foreshadow.hall", ">=2" }, { "saw_lock", "true" }, { "n", "==3" } };
            Assert.AreEqual("foreshadow.hall >= 2, saw_lock = true, n == 3", VnTextSyntax.FormatCondition(c));
            Assert.AreEqual("", VnTextSyntax.FormatCondition(null));
        }

        [Test]
        public void Syntax_Effects_RoundTrip()
        {
            Assert.IsTrue(VnTextSyntax.TryParseEffects("foreshadow.hall +1, trust.yume -2, saw_lock = TRUE", out var add, out var set, out _));
            Assert.AreEqual(1, add["foreshadow.hall"]);
            Assert.AreEqual(-2, add["trust.yume"]);
            Assert.IsTrue(set["saw_lock"]);
            Assert.AreEqual("foreshadow.hall +1, trust.yume -2, saw_lock = true", VnTextSyntax.FormatEffects(new VnChoice { add = add, set = set }));
            Assert.IsFalse(VnTextSyntax.TryParseEffects("hall = 3", out _, out _, out _), "누적은 +N/-N, 단일은 = true/false");
        }

        // ---------------- 검사기

        [Test]
        public void Validator_CleanSampleHasNoErrors()
        {
            string path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/VN/base/ep_c01.json");
            var ep = VnSerializer.ReadEpisode(System.IO.File.ReadAllText(path));
            string flagsPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/VN/flags.json");
            var reg = VnSerializer.ReadFlags(System.IO.File.ReadAllText(flagsPath));
            var issues = VnValidator.Validate(ep, reg, Known());
            CollectionAssert.IsEmpty(issues.Where(i => i.Severity == VnIssueSeverity.Error).Select(i => i.Message));
        }

        [Test]
        public void Validator_CatchesFlagProblems()
        {
            var a = L("L001", "yume");
            a.condition = new Dictionary<string, string> { { "typo.flag", ">=1" }, { "saw_lock", ">=1" }, { "foreshadow.hall", "true" } };
            var b = L("L002", "mc");
            b.choice = new List<VnChoice>
            {
                new VnChoice { text = "x", add = new Dictionary<string, int> { { "saw_lock", 1 } } },
                new VnChoice { text = "", set = new Dictionary<string, bool> { { "foreshadow.hall", true } } },
            };
            var m = Messages(Episode(a, b));
            Assert.That(m, Has.Some.Contains("등록 안 된 플래그: typo.flag"));
            Assert.That(m, Has.Some.Contains("saw_lock는 단일 — true/false로"));
            Assert.That(m, Has.Some.Contains("foreshadow.hall는 누적 — 숫자 비교로"));
            Assert.That(m, Has.Some.Contains("saw_lock는 단일 — add 대신 set"));
            Assert.That(m, Has.Some.Contains("foreshadow.hall는 누적 — set 대신 add"));
            Assert.That(m, Has.Some.Contains("선택지 2 텍스트 없음"));
        }

        [Test]
        public void Validator_CatchesSpeakerHonorificAndStageProblems()
        {
            var mcFirst = L("L001", "mc");                        // 앞에 캐릭터 없음
            var unknown = L("L002", "yumee");                      // 오타 화자
            var noCast = L("L003", "nemu");                        // 출연진에 호칭 없음
            var mcNemu = L("L004", "mc");                          // 직전 = nemu, 호칭 없음
            var bad = L("L005", "yume"); bad.pos = "middle"; bad.bg = "hall.wipe"; bad.fx = new List<string> { "glow" }; bad.exit = new List<string> { "narration" };
            var dup = L("L005", "yume");
            var m = Messages(Episode(mcFirst, unknown, noCast, mcNemu, bad, dup));
            Assert.That(m, Has.Some.Contains("Error:0:mc 호칭을 정할 캐릭터가 앞에 없음"));
            Assert.That(m, Has.Some.Contains("Error:1:모르는 화자"));
            Assert.That(m, Has.Some.Contains("Error:3:\"nemu\"의 호칭이 출연진에 없음"));
            Assert.That(m, Has.Some.Contains("pos는 left/center/right"));
            Assert.That(m, Has.Some.Contains("배경 전환은 cut/fade"));
            Assert.That(m, Has.Some.Contains("모르는 fx"));
            Assert.That(m, Has.Some.Contains("퇴장 대상이 캐릭터가 아님"));
            Assert.That(m, Has.Some.Contains("Error:5:라인 id 중복"));
        }

        [Test]
        public void Validator_AssetChecks_OnlyWhenCatalogHasAssets()
        {
            var a = L("L001", "yume"); a.bg = "hall.fade"; a.cg = "cg_x"; a.bgm = "theme";
            var ep = Episode(a);
            var known = Known();
            Assert.IsEmpty(VnValidator.Validate(ep, Registry(), known), "에셋 목록 null = 검사 안 함");
            known.Backgrounds = new HashSet<string> { "room" };
            Assert.That(VnValidator.Validate(ep, Registry(), known).Select(i => i.Message), Has.Some.Contains("카탈로그에 없는 배경: hall"));
        }

        // ---------------- 파일

        [Test]
        public void NextEpisodeId_PerRoute()
        {
            var existing = new List<VnEpisodeEntry> { new VnEpisodeEntry { Id = "ep_c01" }, new VnEpisodeEntry { Id = "ep_c02" }, new VnEpisodeEntry { Id = "ep_yume_01" } };
            Assert.AreEqual("ep_c03", VnFileStore.NextEpisodeId(existing, VnIds.Common, out int n));
            Assert.AreEqual(3, n);
            Assert.AreEqual("ep_yume_02", VnFileStore.NextEpisodeId(existing, "yume", out _));
            Assert.AreEqual("ep_nemu_01", VnFileStore.NextEpisodeId(existing, "nemu", out _));
        }
    }
}
