using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmCP.Chart
{
    /// ChartData(박자) → PlayChart(초). 동시치기 판별, 보류 기능 차단도 여기서 한다.
    public static class ChartLoader
    {
        /// 같은 박자로 볼 오차. 셋잇단(1/3)처럼 double로 딱 떨어지지 않는 박자 때문에 필요.
        const double SameBeatEpsilon = 1e-4;

        public static PlayChart Load(TextAsset chartJson)
        {
            if (chartJson == null) throw new ArgumentNullException(nameof(chartJson));
            return Build(ChartSerializer.FromJson(chartJson.text));
        }

        public static PlayChart Build(ChartData data)
        {
            if (data.formatVersion > ChartData.CurrentFormatVersion)
                Debug.LogWarning($"[ChartLoader] {data.songId}: 채보 포맷 v{data.formatVersion}이 엔진(v{ChartData.CurrentFormatVersion})보다 새로움");

            var tempo = new TempoMap(data.bpms, data.offsetSec);
            var notes = new List<PlayNote>(data.notes.Count);

            foreach (var src in data.notes)
            {
                if (src.type == NoteType.Obstacle)
                {
                    Debug.LogWarning($"[ChartLoader] {data.songId}: 회피 장애물은 아직 미지원 — beat {src.beat} 건너뜀");
                    continue;
                }

                bool hasLength = src.type == NoteType.Hold || src.type == NoteType.Mash;
                double endBeat = hasLength ? Math.Max(src.endBeat, src.beat) : src.beat;

                // 빠른 홀드·연타는 보류(2026-09-30). 필요해지면 이 조건만 풀면 된다 — NoteView는 이미 Speed를 쓴다.
                float speed = src.type == NoteType.Tap || src.type == NoteType.Heart ? src.speed : 1f;
                if (speed <= 0f) speed = 1f;

                notes.Add(new PlayNote
                {
                    Type = src.type,
                    Lane = src.lane,
                    Beat = src.beat,
                    Time = tempo.BeatToSec(src.beat),
                    EndTime = tempo.BeatToSec(endBeat),
                    Speed = speed,
                });
            }

            // Sort는 불안정 정렬이라 같은 시각이면 레인 순으로 고정해 둔다(재현성).
            notes.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Lane.CompareTo(b.Lane));
            MarkGeminis(notes);

            return new PlayChart(data, tempo, notes);
        }

        static void MarkGeminis(List<PlayNote> notes)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].Type != NoteType.Tap) continue;

                for (int j = i + 1; j < notes.Count && notes[j].Beat - notes[i].Beat < SameBeatEpsilon; j++)
                {
                    if (notes[j].Type == NoteType.Tap && notes[j].Lane != notes[i].Lane)
                    {
                        notes[i].IsGemini = true;
                        notes[j].IsGemini = true;
                    }
                }
            }
        }
    }
}
