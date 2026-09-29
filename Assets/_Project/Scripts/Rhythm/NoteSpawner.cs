using System.Collections.Generic;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 노트가 화면 오른쪽 끝에 닿을 시각에 맞춰 종류별 뷰를 만들고, 판정 이벤트를 해당 뷰에 전달한다.
    /// 생성·삭제는 Instantiate/Destroy — 풀링은 ⑥ juice 작업 때 이펙트와 같이 붙인다.
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] NoteViewBase _tapPrefab;
        [SerializeField] NoteViewBase _heartPrefab;
        [SerializeField] NoteViewBase _holdPrefab;
        [SerializeField] NoteViewBase _mashPrefab;
        [SerializeField] Transform _judgeLine;
        [SerializeField] Transform _topLane;
        [SerializeField] Transform _bottomLane;
        [SerializeField] float _spawnX = 11f;
        [SerializeField] float _despawnX = -11f;

        readonly List<(double spawnTime, PlayNote note)> _pending = new List<(double, PlayNote)>();
        readonly Dictionary<PlayNote, NoteViewBase> _live = new Dictionary<PlayNote, NoteViewBase>();
        int _next;

        SongClock _clock;
        JudgementSystem _judgement;
        float _scrollSpeed;

        public void Init(PlayChart chart, SongClock clock, JudgementSystem judgement, float scrollSpeed)
        {
            Unsubscribe();
            _clock = clock;
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            _judgement.HoldBreakChanged += OnHoldBreakChanged;
            _judgement.MashEnded += OnMashEnded;
            _scrollSpeed = scrollSpeed;

            _pending.Clear();
            _live.Clear();
            _next = 0;

            float travel = _spawnX - _judgeLine.position.x;
            foreach (var note in chart.Notes)
                _pending.Add((note.Time - travel / (_scrollSpeed * note.Speed), note));

            // 빠른 노트는 늦게 출발해 먼저 나온 느린 노트를 따라잡으므로 노트 시각이 아니라 출발 시각으로 정렬.
            _pending.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_judgement == null) return;
            _judgement.Judged -= OnJudged;
            _judgement.HoldBreakChanged -= OnHoldBreakChanged;
            _judgement.MashEnded -= OnMashEnded;
        }

        void Update()
        {
            if (_clock == null) return;

            while (_next < _pending.Count && _pending[_next].spawnTime <= _clock.SongTime)
                Spawn(_pending[_next++].note);
        }

        void Spawn(PlayNote note)
        {
            var prefab = PrefabFor(note.Type);
            if (prefab == null)
            {
                Debug.LogWarning($"[NoteSpawner] {note.Type} 프리팹 없음 — beat {note.Beat} 표시 생략");
                return;
            }

            var laneY = note.Lane == Lane.Top ? _topLane.position.y : _bottomLane.position.y;
            var view = Instantiate(prefab, transform);
            view.Init(note, _clock, _judgeLine.position.x, laneY, _scrollSpeed * note.Speed, _despawnX);
            _live[note] = view;
        }

        NoteViewBase PrefabFor(NoteType type) => type switch
        {
            NoteType.Tap => _tapPrefab,
            NoteType.Heart => _heartPrefab,
            NoteType.Hold => _holdPrefab,
            NoteType.Mash => _mashPrefab,
            _ => null,
        };

        void OnJudged(JudgeResult result)
        {
            if (!_live.TryGetValue(result.Note, out var view)) return;

            // 홀드는 꼬리 판정까지 이벤트를 계속 받아야 하므로 꼬리에서 목록을 뺀다.
            bool done = result.Note.Type != NoteType.Hold || result.Part == NotePart.Tail;
            if (done) _live.Remove(result.Note);
            if (view != null) view.OnJudged(result);
        }

        void OnHoldBreakChanged(PlayNote note, bool broken)
        {
            if (_live.TryGetValue(note, out var view) && view != null) view.OnHoldBreakChanged(broken);
        }

        void OnMashEnded(PlayNote note, int hits)
        {
            if (!_live.TryGetValue(note, out var view)) return;
            _live.Remove(note);
            if (view != null) view.OnMashEnded();
        }
    }
}
