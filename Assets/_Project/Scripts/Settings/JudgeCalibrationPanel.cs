using System;
using System.Collections.Generic;
using RhythmCP.Chart;
using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Settings
{
    /// 판정 오프셋 자동 보정: 메트로놈 클릭에 맞춰 레인 키를 누르면 오차 중앙값을 제안한다(⑤ 확정: 16회, 앞 4회 버림).
    /// 플레이 씬에 기대지 않도록 자기 시계(SongClock, 오프셋 미적용)와 자기 입력(RhythmInput)을 가진다.
    public class JudgeCalibrationPanel : MonoBehaviour
    {
        [SerializeField] SongClock _clock;
        [SerializeField] RhythmInput _input;

        [Tooltip("첫 클릭 시각·간격·개수는 이 클립에 맞춰야 한다(calibration_click.wav = 1.0초부터 0.5초 간격 16회).")]
        [SerializeField] AudioClip _metronome;
        [SerializeField] double _firstBeatSec = 1.0;
        [SerializeField] double _beatIntervalSec = 0.5;
        [SerializeField] int _beatCount = 16;
        [SerializeField] int _discardFirst = 4;

        [Header("UI")]
        [SerializeField] TMP_Text _status;
        [SerializeField] Image _pulse;
        [SerializeField] Button _apply;
        [SerializeField] Button _retry;
        [SerializeField] Button _cancel;

        readonly List<double> _deltas = new List<double>();
        readonly HashSet<int> _tappedBeats = new HashSet<int>();
        Action _onClosed;
        bool _measuring;
        float? _suggestion;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            _apply.onClick.AddListener(Apply);
            _retry.onClick.AddListener(StartMeasure);
            _cancel.onClick.AddListener(Close);
        }

        void OnEnable() => _input.LanePressed += OnPressed;
        void OnDisable() => _input.LanePressed -= OnPressed;

        public void Open(Action onClosed)
        {
            _onClosed = onClosed;
            gameObject.SetActive(true);
            StartMeasure();
        }

        public void Close()
        {
            _measuring = false;
            _clock.Stop();
            gameObject.SetActive(false);
            _onClosed?.Invoke();
        }

        void StartMeasure()
        {
            _deltas.Clear();
            _tappedBeats.Clear();
            _suggestion = null;
            _apply.gameObject.SetActive(false);
            _retry.gameObject.SetActive(false);
            _measuring = true;
            _clock.Begin(_metronome, 0.3);
            UpdateStatus();
        }

        void OnPressed(Lane lane, double realtime)
        {
            if (!_measuring) return;
            double t = _clock.RealtimeToSongTime(realtime);
            if (!CalibrationMath.TryMatchBeat(t, _firstBeatSec, _beatIntervalSec, _beatCount, out int index, out double delta)) return;

            // 한 클릭에 첫 입력만 센다(두 번 누르면 두 번째는 버림). 앞 몇 회는 박자 잡는 구간이라 버린다.
            if (!_tappedBeats.Add(index) || index < _discardFirst) return;
            _deltas.Add(delta);
            UpdateStatus();
        }

        void Update()
        {
            if (!_measuring) return;

            double t = _clock.SongTime;
            double sinceBeat = (t - _firstBeatSec) % _beatIntervalSec;
            bool onBeat = t >= _firstBeatSec - 0.01 && sinceBeat >= 0 && sinceBeat < 0.08;
            _pulse.enabled = onBeat;

            if (t > _firstBeatSec + _beatCount * _beatIntervalSec) Finish();
        }

        void Finish()
        {
            _measuring = false;
            _pulse.enabled = false;
            _suggestion = CalibrationMath.SuggestMs(_deltas);
            _retry.gameObject.SetActive(true);
            _apply.gameObject.SetActive(_suggestion.HasValue);
            _status.text = _suggestion.HasValue
                ? $"제안 {_suggestion.Value:+0;-0;0} ms  (현재 {GameSettings.JudgeOffsetMs:+0;-0;0} ms)"
                : "입력이 부족해요. 다시 측정해 주세요.";
        }

        void Apply()
        {
            if (_suggestion.HasValue) GameSettings.JudgeOffsetMs = _suggestion.Value;
            Close();
        }

        void UpdateStatus() =>
            _status.text = $"클릭 소리에 맞춰 레인 키(F·D·J·K)를 누르세요\n{_deltas.Count} / {_beatCount - _discardFirst}";
    }
}
