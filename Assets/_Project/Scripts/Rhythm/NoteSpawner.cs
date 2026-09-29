using System.Collections.Generic;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 노트가 화면 오른쪽 끝에 닿을 시각에 맞춰 NoteView를 만든다.
    /// 생성·삭제는 Instantiate/Destroy — 풀링은 ⑥ juice 작업 때 이펙트와 같이 붙인다.
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] NoteView _tapPrefab;
        [SerializeField] Transform _judgeLine;
        [SerializeField] Transform _topLane;
        [SerializeField] Transform _bottomLane;
        [SerializeField] float _spawnX = 11f;
        [SerializeField] float _despawnX = -11f;

        readonly List<(double spawnTime, PlayNote note)> _pending = new List<(double, PlayNote)>();
        readonly Dictionary<PlayNote, NoteView> _live = new Dictionary<PlayNote, NoteView>();
        int _next;

        SongClock _clock;
        JudgementSystem _judgement;
        float _scrollSpeed;

        public void Init(PlayChart chart, SongClock clock, JudgementSystem judgement, float scrollSpeed)
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
            _clock = clock;
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            _scrollSpeed = scrollSpeed;

            _pending.Clear();
            _live.Clear();
            _next = 0;

            float travel = _spawnX - _judgeLine.position.x;
            foreach (var note in chart.Notes)
            {
                if (note.Type != NoteType.Tap) continue; // ②에서 종류별 프리팹 추가
                _pending.Add((note.Time - travel / (_scrollSpeed * note.Speed), note));
            }

            // 빠른 노트는 늦게 출발해 먼저 나온 느린 노트를 따라잡으므로 노트 시각이 아니라 출발 시각으로 정렬.
            _pending.Sort((a, b) => a.spawnTime.CompareTo(b.spawnTime));
        }

        void OnDestroy()
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
        }

        void Update()
        {
            if (_clock == null) return;

            while (_next < _pending.Count && _pending[_next].spawnTime <= _clock.SongTime)
                Spawn(_pending[_next++].note);
        }

        void Spawn(PlayNote note)
        {
            var laneY = note.Lane == Lane.Top ? _topLane.position.y : _bottomLane.position.y;
            var view = Instantiate(_tapPrefab, transform);
            view.Init(note, _clock, _judgeLine.position.x, laneY, _scrollSpeed * note.Speed, _despawnX);
            _live[note] = view;
        }

        void OnJudged(JudgeResult result)
        {
            if (!_live.TryGetValue(result.Note, out var view)) return;
            _live.Remove(result.Note);
            if (view != null) view.OnJudged(result.Judgement);
        }
    }
}
