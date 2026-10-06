using System;
using System.Collections.Generic;
using System.Linq;
using RhythmCP.Vn;

namespace RhythmCP.VnEditing
{
    /// 편집 중인 에피소드 한 개. 모든 수정은 Edit()를 거쳐 되돌리기 기록·Changed 알림이 한 곳에서 일어난다.
    /// 되돌리기는 채보 에디터(ChartDocument)와 같은 전체 스냅숏 방식 — 에피소드 하나가 수백 줄이라 충분히 가볍다.
    public class VnDocument
    {
        const int UndoLimit = 200;

        readonly LinkedList<string> _undo = new LinkedList<string>();
        readonly Stack<string> _redo = new Stack<string>();

        public VnEpisode Episode { get; private set; }
        public bool IsDirty { get; private set; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public event Action Changed;

        public VnDocument(VnEpisode episode)
        {
            Episode = episode;
            Normalize();
            // 손으로 쓴 JSON(id 없음)을 처음 열 때 한 번만 붙인다 — 이후로는 다시 매기지 않는다.
            if (AssignMissingIds(Episode)) IsDirty = true;
        }

        public void Edit(Action<VnEpisode> mutate)
        {
            _undo.AddLast(Snapshot());
            if (_undo.Count > UndoLimit) _undo.RemoveFirst();
            _redo.Clear();

            mutate(Episode);
            IsDirty = true;
            Normalize();
            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            _redo.Push(Snapshot());
            Restore(_undo.Last.Value);
            _undo.RemoveLast();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            _undo.AddLast(Snapshot());
            Restore(_redo.Pop());
            return true;
        }

        /// 외부(Claude·다른 편집기)가 파일을 고쳤을 때 통째로 교체. 되돌리기 기록은 남겨 실수로 덮여도 돌아갈 수 있게.
        public void ReplaceFromOutside(VnEpisode episode)
        {
            _undo.AddLast(Snapshot());
            _redo.Clear();
            Episode = episode;
            Normalize();
            IsDirty = AssignMissingIds(Episode);
            Changed?.Invoke();
        }

        public void MarkSaved() => IsDirty = false;

        // ---------------- 라인 조작 (모두 Edit 경유 — 한 번 = 되돌리기 한 칸)

        /// after 다음에 새 라인. after = -1이면 맨 앞. 새 인덱스를 돌려준다.
        public int InsertLine(int after, string speaker = VnIds.Narration)
        {
            int index = Math.Max(0, Math.Min(after + 1, Episode.lines.Count));
            Edit(ep => ep.lines.Insert(index, new VnLine { id = VnSerializer.NextLineId(ep.lines), speaker = speaker, text = "" }));
            return index;
        }

        /// 복제본은 새 id(기읽·세이브 기준이 겹치지 않게).
        public int DuplicateLine(int index)
        {
            Edit(ep =>
            {
                var copy = VnSerializer.Clone(ep.lines[index]);
                copy.id = VnSerializer.NextLineId(ep.lines);
                ep.lines.Insert(index + 1, copy);
            });
            return index + 1;
        }

        public void DeleteLines(IEnumerable<int> indices)
        {
            var sorted = indices.Where(i => i >= 0 && i < Episode.lines.Count).Distinct().OrderByDescending(i => i).ToList();
            if (sorted.Count == 0) return;
            Edit(ep =>
            {
                foreach (int i in sorted) ep.lines.RemoveAt(i);
            });
        }

        /// 한 줄 위(-1)/아래(+1)로. 새 인덱스를 돌려준다(끝이면 그대로).
        public int MoveLine(int index, int delta)
        {
            int to = index + delta;
            if (to < 0 || to >= Episode.lines.Count) return index;
            Edit(ep =>
            {
                var line = ep.lines[index];
                ep.lines.RemoveAt(index);
                ep.lines.Insert(to, line);
            });
            return to;
        }

        /// 같은 조건을 여러 줄에 한 번에(결정 5: 블록 if 대신). null·빈 조건 = 조건 제거.
        public void SetCondition(IEnumerable<int> indices, Dictionary<string, string> condition)
        {
            var list = indices.ToList();
            Edit(ep =>
            {
                foreach (int i in list)
                    ep.lines[i].condition = condition == null || condition.Count == 0 ? null : new Dictionary<string, string>(condition);
            });
        }

        /// 선택한 한 줄 수정.
        public void EditLine(int index, Action<VnLine> mutate) => Edit(ep => mutate(ep.lines[index]));

        public static bool AssignMissingIds(VnEpisode episode)
        {
            bool any = false;
            var seen = new HashSet<string>();
            foreach (var line in episode.lines)
            {
                // 빈 id·중복 id(복붙 실수)는 새로. 기존 고유 id는 절대 건드리지 않는다.
                if (!string.IsNullOrEmpty(line.id) && seen.Add(line.id)) continue;
                line.id = VnSerializer.NextLineId(episode.lines);
                seen.Add(line.id);
                any = true;
            }
            return any;
        }

        string Snapshot() => VnSerializer.WriteEpisode(Episode);

        void Restore(string json)
        {
            Episode = VnSerializer.ReadEpisode(json);
            IsDirty = true;
            Normalize();
            Changed?.Invoke();
        }

        void Normalize()
        {
            Episode.meta ??= new VnMeta();
            Episode.cast ??= new List<VnCastEntry>();
            Episode.lines ??= new List<VnLine>();
        }
    }
}
