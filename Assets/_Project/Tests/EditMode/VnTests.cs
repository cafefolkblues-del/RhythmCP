using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmCP.Vn;

namespace RhythmCP.Tests
{
    public class VnTests
    {
        class MemoryReadLog : IVnReadLog
        {
            public readonly HashSet<string> Read = new HashSet<string>();
            public bool IsRead(string ep, string line) => Read.Contains(ep + ":" + line);
            public void MarkRead(string ep, string line) => Read.Add(ep + ":" + line);
        }

        static VnEpisode Episode(params VnLine[] lines) => new VnEpisode
        {
            meta = new VnMeta { id = "ep_t" },
            cast = new List<VnCastEntry>
            {
                new VnCastEntry { id = "yume", honorific = "식객" },
                new VnCastEntry { id = "nemu", honorific = "주인님" },
            },
            lines = lines.ToList(),
        };

        static VnLine L(string id, string speaker, string text = "…") => new VnLine { id = id, speaker = speaker, text = text };

        static VnPlayer Play(VnEpisode ep, VnFlags flags = null, IVnReadLog log = null) =>
            new VnPlayer(ep, flags, id => id == "yume" ? "유메 블랑" : id == "nemu" ? "네무 노아르" : id, log);

        // ---------------- 조건

        [TestCase(">=2", 2, true)]
        [TestCase(">=2", 1, false)]
        [TestCase("<=1", 1, true)]
        [TestCase("==0", 0, true)]
        [TestCase("!=0", 0, false)]
        [TestCase(">3", 4, true)]
        [TestCase("<3", 3, false)]
        public void Condition_Counter(string expr, int value, bool expected)
        {
            var f = new VnFlags();
            f.counters["c"] = value;
            Assert.IsTrue(VnCondition.TryEvaluate("c", expr, f, out bool r));
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void Condition_Bool_And_BadSyntax()
        {
            var f = new VnFlags();
            f.bools["saw"] = true;
            Assert.IsTrue(VnCondition.Evaluate(new Dictionary<string, string> { { "saw", "true" } }, f));
            Assert.IsFalse(VnCondition.Evaluate(new Dictionary<string, string> { { "saw", "false" } }, f));
            Assert.IsFalse(VnCondition.TryEvaluate("c", "~2", f, out _), "문법 오류");
            Assert.IsFalse(VnCondition.Evaluate(new Dictionary<string, string> { { "saw", "true" }, { "c", ">=1" } }, f), "AND");
        }

        // ---------------- 진행

        [Test]
        public void Choice_AddsFlags_AndGatesLaterLines()
        {
            var choice = L("L002", "mc");
            choice.choice = new List<VnChoice>
            {
                new VnChoice { text = "창밖을 본다", add = new Dictionary<string, int> { { "foreshadow.hall", 1 } } },
                new VnChoice { text = "문을 살핀다", set = new Dictionary<string, bool> { { "saw_lock", true } } },
            };
            var gated = L("L003", "yume", "자물쇠 봤죠?");
            gated.condition = new Dictionary<string, string> { { "saw_lock", "true" } };
            var ep = Episode(L("L001", "yume"), choice, gated, L("L004", "narration"));

            var p = Play(ep);
            p.Begin();
            p.Advance();
            Assert.IsTrue(p.AwaitingChoice);
            Assert.IsFalse(p.Advance(), "고르기 전엔 진행 안 함");
            p.Choose(0);
            p.Advance();
            Assert.AreEqual("L004", p.Current.Line.id, "saw_lock 없으니 L003 건너뜀");
            Assert.AreEqual(1, p.Flags.Counter("foreshadow.hall"));

            var p2 = Play(ep);
            p2.Begin();
            p2.Advance();
            p2.Choose(1);
            p2.Advance();
            Assert.AreEqual("L003", p2.Current.Line.id);
        }

        [Test]
        public void Mc_UsesLastSpeakersHonorific_OrAs()
        {
            var asNemu = L("L004", "mc");
            asNemu.asCharacter = "nemu";
            var p = Play(Episode(L("L001", "yume"), L("L002", "mc"), L("L003", "nemu"), asNemu, L("L005", "narration")));
            p.Begin();
            Assert.AreEqual("유메 블랑", p.Current.Name);
            p.Advance();
            Assert.AreEqual(VnSpeakerKind.Mc, p.Current.Kind);
            Assert.AreEqual("식객", p.Current.Name);
            p.Advance();
            p.Advance();
            Assert.AreEqual("주인님", p.Current.Name);
            p.Advance();
            Assert.IsNull(p.Current.Name, "나레이션 이름표 없음");
            Assert.IsFalse(p.Advance());
            Assert.IsTrue(p.Ended);
        }

        [Test]
        public void Stage_PosExitBgCgBgm()
        {
            var a = L("L001", "yume"); a.pos = "left"; a.expr = "soft"; a.bg = "hall.fade"; a.bgm = "theme_a";
            var b = L("L002", "nemu"); b.pos = "right";
            var c = L("L003", "nemu"); c.pos = "left"; c.cg = "cg_earbud";       // 왼쪽 차지 → 유메 내려감
            var d = L("L004", "narration"); d.exit = new List<string> { "nemu" }; d.cg = "off"; d.bgm = "stop"; d.bg = "hall";
            var p = Play(Episode(a, b, c, d));

            p.Begin();
            Assert.AreEqual("fade", p.Current.BgTransition);
            Assert.AreEqual("soft", p.Stage.Actor("yume").expr);
            p.Advance();
            Assert.AreEqual(2, p.Stage.actors.Count);
            p.Advance();
            CollectionAssert.AreEqual(new[] { "yume" }, p.Current.Exited);
            Assert.AreEqual("cg_earbud", p.Stage.cg);
            p.Advance();
            Assert.IsEmpty(p.Stage.actors);
            Assert.IsNull(p.Stage.cg);
            Assert.IsNull(p.Stage.bgm);
            Assert.IsNull(p.Current.BgTransition, "같은 배경이면 전환 없음");
        }

        [Test]
        public void BeginMidway_RebuildsStage_AndLastSpeaker()
        {
            var a = L("L001", "nemu"); a.pos = "center"; a.bg = "room";
            var p = Play(Episode(a, L("L002", "narration"), L("L003", "mc")));
            p.Begin(2);
            Assert.AreEqual("room", p.Stage.bg);
            Assert.AreEqual("center", p.Stage.Actor("nemu").pos);
            Assert.AreEqual("주인님", p.Current.Name);
        }

        [Test]
        public void ReadLog_MarksAndReports()
        {
            var log = new MemoryReadLog();
            var ep = Episode(L("L001", "yume"), L("L002", "yume"));
            var p = Play(ep, null, log);
            p.Begin();
            Assert.IsFalse(p.Current.WasRead);
            var again = Play(ep, null, log);
            again.Begin();
            Assert.IsTrue(again.Current.WasRead);
            Assert.IsTrue(log.Read.Contains("ep_t:L002") == false);
        }

        [Test]
        public void Backlog_RecordsLinesAndChoice()
        {
            var ch = L("L002", "mc", "…(무엇을 할까)");
            ch.choice = new List<VnChoice> { new VnChoice { text = "창밖을 본다" } };
            var p = Play(Episode(L("L001", "yume", "앉아요."), ch));
            p.Begin();
            p.Advance();
            p.Choose(0);
            CollectionAssert.AreEqual(new[] { "앉아요.", "…(무엇을 할까)", "▶ 창밖을 본다" }, p.Backlog.Select(b => b.Text).ToArray());
        }

        // ---------------- 직렬화

        [Test]
        public void Serializer_OmitsEmpty_RoundTrips_AsAndIfKeys()
        {
            var line = L("L001", "mc", "…");
            line.asCharacter = "nemu";
            line.exit = new List<string>();
            line.condition = new Dictionary<string, string> { { "foreshadow.hall", ">=2" } };
            var ep = Episode(line);
            string json = VnSerializer.WriteEpisode(ep);
            StringAssert.Contains("\"as\": \"nemu\"", json);
            StringAssert.Contains("\"if\"", json);
            StringAssert.DoesNotContain("\"exit\"", json);
            StringAssert.DoesNotContain("\"expr\"", json);
            var back = VnSerializer.ReadEpisode(json);
            Assert.AreEqual("nemu", back.lines[0].asCharacter);
            Assert.AreEqual(">=2", back.lines[0].condition["foreshadow.hall"]);
        }

        [Test]
        public void NextLineId_NeverReusesOrRenumbers()
        {
            var lines = new List<VnLine> { L("L001", "a"), L("L007", "a"), L("L003", "a") };
            Assert.AreEqual("L008", VnSerializer.NextLineId(lines));
            Assert.AreEqual("L001", VnSerializer.NextLineId(new List<VnLine>()));
        }
    }
}
