using System;
using System.Collections.Generic;
using RhythmCP.Chart;

namespace RhythmCP.Character
{
    /// 보너스 노트 배치(순수 계산). 기존 노트와 겹치지 않는 박자 격자 위에 위·아래 번갈아 놓는다.
    public static class BonusNotePlanner
    {
        /// 같은 레인에서 이 박자 거리 안에 노트가 있으면 그 자리는 비운다.
        const double ClearanceBeats = 0.25;

        public static List<PlayNote> Plan(IEnumerable<PlayNote> existing, TempoMap tempo,
            double fromSec, double toSec, int count, double beatSpacing)
        {
            var result = new List<PlayNote>();
            if (count <= 0 || beatSpacing <= 0 || toSec <= fromSec) return result;

            var occupied = new List<PlayNote>(existing);
            double beat = Math.Ceiling(tempo.SecToBeat(fromSec) / beatSpacing) * beatSpacing;
            var lane = Lane.Top;

            for (; result.Count < count; beat += beatSpacing)
            {
                double time = tempo.BeatToSec(beat);
                if (time > toSec) break;

                // 이번 차례 레인이 막히면 반대 레인, 둘 다 막히면 그 박자는 건너뛴다.
                Lane? chosen = IsFree(occupied, lane, beat) ? lane
                    : IsFree(occupied, Other(lane), beat) ? Other(lane)
                    : (Lane?)null;
                if (chosen == null) continue;

                var note = new PlayNote
                {
                    Type = NoteType.Tap,
                    Lane = chosen.Value,
                    Beat = beat,
                    Time = time,
                    EndBeat = beat,
                    EndTime = time,
                    Speed = 1f,
                    IsBonus = true,
                };
                result.Add(note);
                occupied.Add(note);
                lane = Other(chosen.Value);
            }
            return result;
        }

        static Lane Other(Lane lane) => lane == Lane.Top ? Lane.Bottom : Lane.Top;

        static bool IsFree(List<PlayNote> notes, Lane lane, double beat)
        {
            foreach (var n in notes)
            {
                // 연타 구간은 어느 버튼이든 연타로 먹으므로 양 레인 모두 막힌다.
                bool anyLane = n.Type == NoteType.Mash;
                if (!anyLane && n.Lane != lane) continue;
                if (beat >= n.Beat - ClearanceBeats && beat <= n.EndBeat + ClearanceBeats) return false;
            }
            return true;
        }
    }
}
