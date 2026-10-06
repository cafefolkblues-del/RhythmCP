using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 노트 + 선택 표시 + 이동/생성 미리보기 + 녹음 중 노트.
    public class NoteGraphic : TimelineGraphic
    {
        [SerializeField] Sprite _circle;
        [SerializeField] Color _tap = new Color(0.92f, 0.92f, 0.92f);
        [SerializeField] Color _fast = new Color(0.45f, 0.9f, 1f);
        [SerializeField] Color _heart = new Color(0.9f, 0.25f, 0.45f);
        [SerializeField] Color _holdBody = new Color(0.92f, 0.92f, 0.92f, 0.45f);
        [SerializeField] Color _mash = new Color(0.35f, 0.6f, 0.9f);
        [SerializeField] Color _selected = new Color(1f, 0.85f, 0.25f);
        [SerializeField] Color _pending = new Color(0.4f, 1f, 0.5f, 0.8f);

        public override Texture mainTexture => _circle != null ? _circle.texture : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Ready) return;
            var doc = Session.Document;
            var move = Session.MovePreview;

            foreach (var n in doc.Data.notes)
            {
                bool selected = Session.Selection.Contains(n);
                if (selected && move.Active)
                {
                    DrawNote(vh, n, n.beat, n.lane, 0.25f, false);
                    DrawNote(vh, n, n.beat + move.DeltaBeat, Shift(n.lane, move.LaneShift), 1f, true);
                }
                else DrawNote(vh, n, n.beat, n.lane, 1f, selected);
            }

            if (Session.CreatePreview != null)
                DrawNote(vh, Session.CreatePreview, Session.CreatePreview.beat, Session.CreatePreview.lane, 0.6f, false);

            foreach (var n in Session.PendingNotes)
                DrawNote(vh, n, n.beat, n.lane, 1f, false, _pending);

            if (Session.BoxSelect.HasValue)
            {
                var b = Session.BoxSelect.Value;
                Rect(vh, b.xMin, b.yMin, b.xMax, b.yMax, new Color(_selected.r, _selected.g, _selected.b, 0.15f));
            }
        }

        static Lane Shift(Lane lane, int shift) => shift == 0 ? lane : lane == Lane.Top ? Lane.Bottom : Lane.Top;

        void DrawNote(VertexHelper vh, NoteData n, double beat, Lane lane, float alpha, bool selected, Color? overrideColor = null)
        {
            var tempo = Session.Document.Tempo;
            bool hasLength = n.type == NoteType.Hold || n.type == NoteType.Mash;
            double endBeat = hasLength ? beat + (n.endBeat - n.beat) : beat;
            double t0 = tempo.BeatToSec(beat), t1 = tempo.BeatToSec(endBeat);
            if (t1 < Timeline.ViewStartSec - 1 || t0 > Timeline.ViewEndSec + 1) return;

            float x0 = Timeline.TimeToX(t0), x1 = Timeline.TimeToX(t1);
            float y = Timeline.LaneY(lane);
            float r = Timeline.LaneHalfHeight * 0.4f;

            Color body = overrideColor ?? n.type switch
            {
                NoteType.Heart => _heart,
                NoteType.Mash => _mash,
                _ => n.speed > 1f ? _fast : _tap,
            };
            body.a *= alpha;

            if (selected) Circle(vh, x0, y, r * 1.35f, WithAlpha(_selected, alpha));

            if (n.type == NoteType.Hold)
            {
                Rect(vh, x0, y - r * 0.45f, x1, y + r * 0.45f, WithAlpha(overrideColor ?? _holdBody, alpha * (overrideColor.HasValue ? 1f : _holdBody.a)));
                Circle(vh, x1, y, r * 0.6f, body);
            }
            else if (n.type == NoteType.Mash)
            {
                Rect(vh, x0, y - r * 0.9f, x1, y + r * 0.9f, WithAlpha(body, body.a * 0.35f));
                Circle(vh, x0, y, r * 1.3f, body);
                return;
            }
            Circle(vh, x0, y, r, body);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
