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

        [TestCase(0, "L008")]
        [TestCase(1, "L007")]
        public void SampleEpisode_PlaysToEnd_BothChoices(int pick, string gatedLine)
        {
            string path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/VN/base/ep_c01.json");
            var ep = VnSerializer.ReadEpisode(System.IO.File.ReadAllText(path));
            var p = Play(ep);
            var seen = new List<string>();
            p.LineShown += s => seen.Add(s.Line.id);
            p.Begin();
            for (int guard = 0; guard < 100 && !p.Ended; guard++)
            {
                if (p.AwaitingChoice) p.Choose(pick);
                p.Advance();
            }
            Assert.IsTrue(p.Ended);
            CollectionAssert.Contains(seen, gatedLine);
            Assert.AreEqual(11, seen.Count, "if 라인 둘 중 하나만");
            Assert.IsEmpty(p.Stage.actors);
            Assert.IsNull(p.Stage.bgm);
            Assert.AreEqual("room", p.Stage.bg);
        }

        // ---------------- ③ 기읽·세이브·오토

        static string TempDir()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rhythmcp_vn_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }

        [Test]
        public void ReadLog_PersistsAcrossInstances()
        {
            string path = System.IO.Path.Combine(TempDir(), "read.json");
            var a = new VnReadLog(path);
            a.MarkRead("ep_c01", "L003");
            Assert.IsTrue(a.Dirty);
            a.Flush();
            var b = new VnReadLog(path);
            Assert.IsTrue(b.IsRead("ep_c01", "L003"));
            Assert.IsFalse(b.IsRead("ep_c01", "L004"));
            Assert.IsFalse(b.Dirty);
        }

        [Test]
        public void SaveLoad_RestoresSameLineStageAndFlags()
        {
            var a = L("L001", "yume"); a.pos = "left"; a.bg = "hall";
            var b = L("L002", "nemu"); b.pos = "right"; b.cg = "cg_x";
            var ch = L("L003", "mc");
            ch.choice = new List<VnChoice> { new VnChoice { text = "x", add = new Dictionary<string, int> { { "k", 2 } } } };
            var c = L("L004", "nemu"); c.exit = new List<string> { "yume" };
            var ep = Episode(a, b, ch, c);

            var p = Play(ep);
            p.Begin();
            p.Advance(); p.Advance(); p.Choose(0); p.Advance();   // L004 표시 중
            var store = new VnSaveStore(TempDir(), 3);
            store.Save(1, new VnSaveData
            {
                episodeId = ep.meta.id, lineId = p.Current.Line.id,
                flags = VnSerializer.Clone(p.Flags), stage = VnSerializer.Clone(p.StageBeforeCurrent),
            });
            Assert.IsFalse(store.Exists(0));
            var data = store.Load(1);

            var q = Play(ep, data.flags);
            q.Begin(q.IndexOf(data.lineId), data.stage);
            Assert.AreEqual("L004", q.Current.Line.id);
            CollectionAssert.AreEqual(new[] { "yume" }, q.Current.Exited, "그 라인의 퇴장 연출도 다시 나온다");
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(p.Stage), Newtonsoft.Json.JsonConvert.SerializeObject(q.Stage));
            Assert.AreEqual(2, q.Flags.Counter("k"));
        }

        [Test]
        public void SaveStore_CorruptFile_IsEmptySlot()
        {
            string dir = TempDir();
            var store = new VnSaveStore(dir, 2);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "slot_01.json"), "{ broken");
            Assert.IsNull(store.Load(0));
        }

        static VnShownLine Shown(bool read) => new VnShownLine { Line = L("L001", "yume"), WasRead = read };

        [Test]
        public void Auto_WaitsBasePlusPerChar_AfterTyping()
        {
            var auto = new VnAutoAdvance(1f, 0.1f, 0.05f) { Mode = VnAdvanceMode.Auto };
            auto.OnLineShown(Shown(false), 10);   // 1 + 1 = 2초
            Assert.IsFalse(auto.Tick(5f, true, false, false), "찍는 중엔 대기 안 셈");
            Assert.IsFalse(auto.Tick(1.5f, false, false, false));
            Assert.IsTrue(auto.Tick(0.6f, false, false, false));
            Assert.IsFalse(auto.Tick(10f, false, true, false), "선택지에서 기다림");
            Assert.AreEqual(VnAdvanceMode.Auto, auto.Mode, "오토는 선택지에서 안 꺼짐");
        }

        [Test]
        public void ReadSkip_StopsAtUnread_Skip_StopsAtChoice()
        {
            var auto = new VnAutoAdvance(1f, 0f, 0.05f) { Mode = VnAdvanceMode.ReadSkip };
            auto.OnLineShown(Shown(true), 5);
            Assert.IsTrue(auto.Tick(0.06f, true, false, false), "읽은 줄은 찍는 중이어도 넘김");
            auto.OnLineShown(Shown(false), 5);
            Assert.AreEqual(VnAdvanceMode.Manual, auto.Mode);

            auto.Mode = VnAdvanceMode.Skip;
            auto.OnLineShown(Shown(false), 5);
            Assert.AreEqual(VnAdvanceMode.Skip, auto.Mode, "전체 스킵은 안 읽은 줄도 넘김");
            Assert.IsFalse(auto.Tick(0.06f, false, true, false));
            Assert.AreEqual(VnAdvanceMode.Manual, auto.Mode);

            Assert.IsTrue(auto.Tick(0.06f, false, false, true), "Ctrl 홀드는 모드 무관");
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
