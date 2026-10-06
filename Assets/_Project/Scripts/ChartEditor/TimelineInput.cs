using System;
using System.Linq;
using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RhythmCP.ChartEditing
{
    /// 타임라인 마우스 조작. uGUI 포인터 이벤트로 받는다 — 패널이 위에 겹친 곳은 자동으로 이벤트가 안 와서 따로 거를 필요가 없다.
    /// 좌클릭 = 놓기 / 노트 잡고 이동, 드래그 = 홀드·연타 길이, Shift+드래그 = 범위 선택, 우클릭 = 삭제,
    /// 휠 = 스크롤, Ctrl+휠 = 확대·축소, 파형 띠 클릭 = 커서 이동.
    public class TimelineInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        enum Mode
        {
            None,
            Move,
            Create,
            Box,
            Scrub,
            ResizeEnd,
        }

        [SerializeField] ChartEditorSession _session;
        [SerializeField] TimelineView _timeline;

        [Tooltip("노트를 집는 판정 반경(px).")]
        [SerializeField] float _hitRadius = 14f;

        Mode _mode;
        Vector2 _downLocal;
        double _downBeat;
        Lane _downLane;
        NoteData _resizing;

        public void OnPointerDown(PointerEventData e)
        {
            if (_session.Document == null || _session.IsRecording) return;
            var p = Local(e);
            _downLocal = p;
            bool onLane = _timeline.TryLaneAt(p.y, out _downLane);
            _downBeat = _session.SnapBeat(BeatAt(p.x));

            if (e.button == PointerEventData.InputButton.Right)
            {
                var victim = HitTest(p);
                if (victim != null) _session.RemoveNotes(_session.Selection.Contains(victim) ? _session.Selection.ToList() : new() { victim });
                return;
            }
            if (e.button != PointerEventData.InputButton.Left) return;

            if (!onLane)
            {
                _mode = Mode.Scrub;
                _session.SetCursor(_timeline.XToTime(p.x));
                return;
            }

            // 홀드·연타 꼬리를 잡으면 길이 조정(스펙 §6 "홀드 길이 드래그").
            _resizing = TailHitTest(p);
            if (_resizing != null)
            {
                _mode = Mode.ResizeEnd;
                _session.CreatePreview = Clone(_resizing);
                _session.NotifyRedraw();
                return;
            }

            var hit = HitTest(p);
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

            if (shift && hit == null)
            {
                _mode = Mode.Box;
                _session.BoxSelect = new Rect(p, Vector2.zero);
            }
            else if (hit != null)
            {
                if (!_session.Selection.Contains(hit))
                {
                    if (!shift) _session.Selection.Clear();
                    _session.Selection.Add(hit);
                }
                _mode = Mode.Move;
                _session.MovePreview = new MovePreview { Active = true };
            }
            else
            {
                var note = _session.NewNote(_downLane, _downBeat);
                if (note.type == NoteType.Hold || note.type == NoteType.Mash)
                {
                    _mode = Mode.Create;
                    _session.CreatePreview = note;
                }
                else
                {
                    _session.AddNote(note);
                    _mode = Mode.None;
                }
            }
            _session.SetCursor(_timeline.XToTime(p.x));
            _session.NotifyRedraw();
        }

        public void OnDrag(PointerEventData e)
        {
            if (_session.Document == null) return;
            var p = Local(e);
            double beat = _session.SnapBeat(BeatAt(p.x));

            switch (_mode)
            {
                case Mode.Scrub:
                    _session.SetCursor(_timeline.XToTime(p.x));
                    break;
                case Mode.Move:
                    _timeline.TryLaneAt(p.y, out var lane);
                    _session.MovePreview = new MovePreview
                    {
                        Active = true,
                        DeltaBeat = beat - _downBeat,
                        LaneShift = lane == _downLane ? 0 : 1,
                    };
                    break;
                case Mode.Create:
                case Mode.ResizeEnd:
                    _session.CreatePreview.endBeat = Math.Max(_session.CreatePreview.beat + 1.0 / _session.SnapDivision, beat);
                    break;
                case Mode.Box:
                    _session.BoxSelect = Rect.MinMaxRect(Mathf.Min(_downLocal.x, p.x), Mathf.Min(_downLocal.y, p.y),
                        Mathf.Max(_downLocal.x, p.x), Mathf.Max(_downLocal.y, p.y));
                    break;
            }
            _session.NotifyRedraw();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_session.Document == null) return;
            switch (_mode)
            {
                case Mode.Move:
                    var move = _session.MovePreview;
                    _session.MovePreview = default;
                    _session.MoveSelection(move.DeltaBeat, move.LaneShift);
                    break;
                case Mode.Create:
                    var note = _session.CreatePreview;
                    _session.CreatePreview = null;
                    _session.AddNote(note);
                    break;
                case Mode.Box:
                    SelectInBox(_session.BoxSelect ?? default);
                    _session.BoxSelect = null;
                    break;
                case Mode.ResizeEnd:
                    double end = _session.CreatePreview.endBeat;
                    _session.CreatePreview = null;
                    if (Math.Abs(end - _resizing.endBeat) > 1e-9) _session.SetEndBeat(_resizing, end);
                    _resizing = null;
                    break;
            }
            _mode = Mode.None;
            _session.NotifyRedraw();
        }

        public void OnScroll(PointerEventData e)
        {
            float dir = Mathf.Sign(e.scrollDelta.y);
            if (Mathf.Approximately(e.scrollDelta.y, 0f)) return;
            if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed)
                _timeline.Zoom(dir > 0 ? 1.2f : 1f / 1.2f, Local(e).x);
            else
                _timeline.ScrollBy(-dir * (_timeline.ViewEndSec - _timeline.ViewStartSec) * 0.1);
        }

        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_timeline.Area, e.position, e.pressEventCamera, out var p);
            return p;
        }

        double BeatAt(float x) => _session.Document.Tempo.SecToBeat(_timeline.XToTime(x));

        /// 머리 원 반경 안이거나 홀드·연타 몸통 위면 그 노트.
        NoteData HitTest(Vector2 p)
        {
            if (!_timeline.TryLaneAt(p.y, out var lane)) return null;
            var tempo = _session.Document.Tempo;
            NoteData best = null;
            float bestDist = float.MaxValue;
            foreach (var n in _session.Document.Data.notes)
            {
                if (n.lane != lane && n.type != NoteType.Mash) continue;
                float x0 = _timeline.TimeToX(tempo.BeatToSec(n.beat));
                float dist = Mathf.Abs(p.x - x0);
                if (n.type == NoteType.Hold || n.type == NoteType.Mash)
                {
                    float x1 = _timeline.TimeToX(tempo.BeatToSec(n.endBeat));
                    if (p.x >= x0 && p.x <= x1) dist = 0;
                }
                if (dist <= _hitRadius && dist < bestDist)
                {
                    best = n;
                    bestDist = dist;
                }
            }
            return best;
        }

        NoteData TailHitTest(Vector2 p)
        {
            if (!_timeline.TryLaneAt(p.y, out var lane)) return null;
            var tempo = _session.Document.Tempo;
            foreach (var n in _session.Document.Data.notes)
            {
                if (n.type != NoteType.Hold && n.type != NoteType.Mash) continue;
                if (n.lane != lane && n.type != NoteType.Mash) continue;
                float x1 = _timeline.TimeToX(tempo.BeatToSec(n.endBeat));
                if (Mathf.Abs(p.x - x1) <= _hitRadius * 0.8f) return n;
            }
            return null;
        }

        static NoteData Clone(NoteData n) => new NoteData { type = n.type, lane = n.lane, beat = n.beat, endBeat = n.endBeat, speed = n.speed };

        void SelectInBox(Rect box)
        {
            var tempo = _session.Document.Tempo;
            _session.Selection.Clear();
            foreach (var n in _session.Document.Data.notes)
            {
                var pt = new Vector2(_timeline.TimeToX(tempo.BeatToSec(n.beat)), _timeline.LaneY(n.lane));
                if (box.Contains(pt)) _session.Selection.Add(n);
            }
        }
    }
}
