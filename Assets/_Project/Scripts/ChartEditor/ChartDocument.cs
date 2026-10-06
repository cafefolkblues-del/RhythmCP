using System;
using System.Collections.Generic;
using RhythmCP.Chart;

namespace RhythmCP.ChartEditing
{
    /// 편집 중인 채보 한 개. 모든 수정은 Edit()를 거쳐 되돌리기 기록·정렬·TempoMap 갱신·Changed 알림이 한 곳에서 일어난다.
    /// 되돌리기는 수정 전 채보 전체 사본(스냅숏)을 쌓는 방식 — 명령별 역연산보다 단순해서 틀릴 곳이 없고,
    /// 채보 크기(노트 수천 개 이하)에선 메모리·속도 모두 문제없다.
    public class ChartDocument
    {
        const int UndoLimit = 200;

        readonly LinkedList<string> _undo = new LinkedList<string>();
        readonly Stack<string> _redo = new Stack<string>();

        public ChartData Data { get; private set; }
        public TempoMap Tempo { get; private set; }

        /// 마지막 저장 이후 수정이 있었는지.
        public bool IsDirty { get; private set; }

        /// BPM 목록이 잘못돼 TempoMap을 못 만들 때의 이유. null이면 정상.
        public string TempoError { get; private set; }

        public event Action Changed;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public ChartDocument(ChartData data)
        {
            Data = data;
            Normalize();
        }

        public void Edit(Action<ChartData> mutate)
        {
            _undo.AddLast(ChartSerializer.ToJson(Data));
            if (_undo.Count > UndoLimit) _undo.RemoveFirst();
            _redo.Clear();

            mutate(Data);
            IsDirty = true;
            Normalize();
            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            _redo.Push(ChartSerializer.ToJson(Data));
            Restore(_undo.Last.Value);
            _undo.RemoveLast();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            _undo.AddLast(ChartSerializer.ToJson(Data));
            Restore(_redo.Pop());
            return true;
        }

        /// 외부(파일 변경·Claude 편집)에서 다시 읽은 내용으로 통째로 교체. 되돌리기 기록은 유지해 실수로 덮여도 돌아갈 수 있게.
        public void ReplaceFromOutside(ChartData data)
        {
            _undo.AddLast(ChartSerializer.ToJson(Data));
            _redo.Clear();
            Data = data;
            IsDirty = false;
            Normalize();
            Changed?.Invoke();
        }

        public void MarkSaved() => IsDirty = false;

        void Restore(string json)
        {
            Data = ChartSerializer.FromJson(json);
            IsDirty = true;
            Normalize();
            Changed?.Invoke();
        }

        void Normalize()
        {
            Data.notes ??= new List<NoteData>();
            Data.bpms ??= new List<BpmPoint>();
            Data.notes.Sort((a, b) => a.beat != b.beat ? a.beat.CompareTo(b.beat) : a.lane.CompareTo(b.lane));
            Data.bpms.Sort((a, b) => a.beat.CompareTo(b.beat));

            try
            {
                Tempo = new TempoMap(Data.bpms, Data.offsetSec);
                TempoError = null;
            }
            catch (ArgumentException e)
            {
                // BPM 목록을 고치는 도중의 잘못된 상태 — 마지막 정상 TempoMap을 유지해 화면이 깨지지 않게 하고 이유만 보인다.
                TempoError = e.Message;
                Tempo ??= new TempoMap(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = 120 } }, Data.offsetSec);
            }
        }

        /// 새 채보 기본값. 다른 난이도가 있으면 BPM·오프셋을 복사해 온다.
        public static ChartData CreateEmpty(string songId, Difficulty difficulty, ChartData copyTimingFrom = null)
        {
            var data = new ChartData { songId = songId, difficulty = difficulty };
            if (copyTimingFrom != null)
            {
                data.offsetSec = copyTimingFrom.offsetSec;
                foreach (var b in copyTimingFrom.bpms) data.bpms.Add(new BpmPoint { beat = b.beat, bpm = b.bpm });
            }
            else
            {
                data.bpms.Add(new BpmPoint { beat = 0, bpm = 120 });
            }
            return data;
        }
    }
}
