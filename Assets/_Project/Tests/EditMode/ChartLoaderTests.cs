using System.Linq;
using NUnit.Framework;
using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.TestTools;

namespace RhythmCP.Tests
{
    public class ChartLoaderTests
    {
        const string Json = @"{
  ""formatVersion"": 1,
  ""songId"": ""t"",
  ""difficulty"": ""Hard"",
  ""offsetSec"": 0.5,
  ""bpms"": [ { ""beat"": 0, ""bpm"": 120 } ],
  ""climax"": { ""startBeat"": 8, ""endBeat"": 16 },
  ""notes"": [
    { ""type"": ""Tap"",   ""lane"": ""Bottom"", ""beat"": 4 },
    { ""type"": ""Tap"",   ""lane"": ""Top"",    ""beat"": 4 },
    { ""type"": ""Tap"",   ""lane"": ""Top"",    ""beat"": 2, ""speed"": 1.5 },
    { ""type"": ""Hold"",  ""lane"": ""Top"",    ""beat"": 6, ""endBeat"": 7, ""speed"": 1.5 },
    { ""type"": ""Obstacle"", ""lane"": ""Top"", ""beat"": 9 }
  ]
}";

        PlayChart Load()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("회피 장애물"));
            return ChartLoader.Build(ChartSerializer.FromJson(Json));
        }

        [Test]
        public void EnumsAndDefaults_ParseFromStrings()
        {
            var data = ChartSerializer.FromJson(Json);
            Assert.AreEqual(Difficulty.Hard, data.difficulty);
            Assert.AreEqual(NoteType.Hold, data.notes[3].type);
            Assert.AreEqual(1f, data.notes[0].speed, "speed 생략 시 1");
        }

        [Test]
        public void Notes_SortedByTime_WithOffset_ObstacleSkipped()
        {
            var chart = Load();
            Assert.AreEqual(4, chart.Notes.Count);
            Assert.AreEqual(0.5 + 2 * 0.5, chart.Notes[0].Time, 1e-9);
            Assert.IsTrue(chart.Notes.Zip(chart.Notes.Skip(1), (a, b) => a.Time <= b.Time).All(x => x));
        }

        [Test]
        public void SameBeatOppositeLanes_AreGemini()
        {
            var chart = Load();
            var geminis = chart.Notes.Where(n => n.IsGemini).ToList();
            Assert.AreEqual(2, geminis.Count);
            Assert.IsTrue(geminis.All(n => n.Beat == 4));
        }

        [Test]
        public void FastHold_IsClampedToNormalSpeed()
        {
            var chart = Load();
            Assert.AreEqual(1.5f, chart.Notes.First(n => n.Type == NoteType.Tap && n.Beat == 2).Speed);
            Assert.AreEqual(1f, chart.Notes.First(n => n.Type == NoteType.Hold).Speed);
        }

        [Test]
        public void Climax_ConvertedToSeconds()
        {
            var chart = Load();
            Assert.IsTrue(chart.HasClimax);
            Assert.AreEqual(0.5 + 8 * 0.5, chart.ClimaxStartSec, 1e-9);
            Assert.AreEqual(0.5 + 16 * 0.5, chart.ClimaxEndSec, 1e-9);
        }

        [Test]
        public void RoundTrip_WritesEnumsAsStrings_AndOmitsDefaults()
        {
            var json = ChartSerializer.ToJson(ChartSerializer.FromJson(Json));
            StringAssert.Contains("\"Tap\"", json);
            StringAssert.Contains("\"Hard\"", json);
            var notes = (Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(json)["notes"];
            Assert.IsNull(notes[0]["speed"], "speed 1은 생략");
            Assert.IsNull(notes[0]["endBeat"], "endBeat 0은 생략");
            Assert.AreEqual(1.5, (double)notes[2]["speed"]);
        }
    }
}
