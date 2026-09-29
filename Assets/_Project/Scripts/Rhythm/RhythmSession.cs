using System;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    public enum SessionEndReason
    {
        Cleared,
        HealthDepleted,
    }

    /// 한 곡 플레이의 조립·시작·종료. 각 시스템은 서로를 모르고, 여기서만 연결한다.
    /// 곡/난이도는 지금은 인스펙터 지정 — 허브(M4) 곡 선택이 생기면 씬 간 전달로 교체.
    public class RhythmSession : MonoBehaviour
    {
        [SerializeField] SongDefinition _song;
        [SerializeField] Difficulty _difficulty;
        [SerializeField] RhythmConfig _config;

        [Header("시스템")]
        [SerializeField] SongClock _clock;
        [SerializeField] RhythmInput _input;
        [SerializeField] JudgementSystem _judgement;
        [SerializeField] NoteSpawner _spawner;
        [SerializeField] ComboCounter _combo;
        [SerializeField] RhythmHealth _health;

        /// 체력 0이어도 결과창은 띄운다(2026-09-30 확정) — 결과 화면(③)이 이 이벤트를 받는다.
        public event Action<SessionEndReason> Ended;

        PlayChart _chart;
        bool _playing;

        public PlayChart Chart => _chart;
        public RhythmConfig Config => _config;

        void Start()
        {
            _chart = ChartLoader.Load(_song.GetChart(_difficulty));

            _judgement.Init(_chart, _clock, _input, _config.Windows);
            _spawner.Init(_chart, _clock, _judgement, _config.ScrollSpeed);
            _combo.Init(_judgement);
            _health.Init(_judgement, _config.MaxHp, _config.MissDamage);
            _health.Depleted += OnHealthDepleted;

            _clock.Begin(_song.Clip, _config.LeadInSec);
            _playing = true;
        }

        void OnDestroy()
        {
            if (_health != null) _health.Depleted -= OnHealthDepleted;
        }

        void Update()
        {
            if (!_playing) return;

            // 채보가 곡보다 먼저 끝나도 곡은 끝까지 듣게 둔다 — 둘 중 늦은 쪽 기준.
            double end = Math.Max(_chart.LastNoteEndSec + _config.TailSec, _clock.ClipLength);
            if (_clock.SongTime >= end) End(SessionEndReason.Cleared);
        }

        void OnHealthDepleted(RhythmHealth _) => End(SessionEndReason.HealthDepleted);

        void End(SessionEndReason reason)
        {
            if (!_playing) return;
            _playing = false;

            _judgement.Stop();
            _clock.Stop();

            Debug.Log($"[RhythmSession] 종료: {reason} · MaxCombo {_combo.MaxCombo} · HP {_health.Current}/{_health.Max}");
            Ended?.Invoke(reason);
        }
    }
}
