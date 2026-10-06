using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RhythmCP.EditorTools
{
    /// 곡을 틀어놓고 박자에 맞춰 스페이스를 두드려 BPM 과 첫 박 오프셋을 잡는 창.
    ///
    /// 탭 간격을 평균 내지 않고 '탭 번호 → 곡 시각' 직선 회귀를 쓴다.
    /// 평균은 한 번 삐끗한 탭이 그대로 섞이지만, 회귀는 전체 추세(기울기 = 박 간격)를 보므로 덜 흔들린다.
    /// 잡힌 값은 메트로놈 클릭을 곡 위에 겹쳐 틀어 귀로 확인하고, ±ms 버튼으로 오프셋을 다듬는다.
    ///
    /// 시각 기준: 곡 시작은 PlayScheduled 로 dspTime 에 고정하고, 탭 시각은 EditorApplication.timeSinceStartup 로 잰다.
    /// dspTime 은 오디오 버퍼 단위(수십 ms)로 뚝뚝 끊겨 올라가서 탭 시각을 재기엔 거칠다.
    public class BpmTapWindow : EditorWindow
    {
        const double StartLeadSeconds = 0.1;
        const double ScheduleAheadSeconds = 0.15;
        const double TapResetGapSeconds = 2.0;
        const int MinTapsForResult = RhythmCP.Chart.BpmEstimator.MinTaps;

        [SerializeField] AudioClip _clip;
        [SerializeField] float _startAt;
        [SerializeField] bool _metronome = true;
        [SerializeField] int _beatsPerBar = 4;

        // 메트로놈과 복사가 쓰는 '확정값'. 탭 결과로 채워지고, 손으로도 고칠 수 있다.
        [SerializeField] double _bpm = 120.0;
        [SerializeField] double _offset;

        readonly List<double> _taps = new List<double>();
        double _fitBpm, _fitOffset, _fitJitterMs;
        bool _hasFit;

        GameObject _host;
        AudioSource _music;
        readonly AudioSource[] _clicks = new AudioSource[2];
        AudioClip _clickClip, _accentClip;
        int _nextClickSource;

        bool _playing;
        double _startDsp;    // 곡의 _startAt 지점이 울리는 dspTime
        double _startWall;   // 같은 순간의 timeSinceStartup
        double _playFrom;    // 재생을 시작한 곡 시각
        long _lastScheduledBeat;

        [MenuItem("Tools/RhythmCP/BPM 탭")]
        static void Open() => GetWindow<BpmTapWindow>("BPM 탭");

        void OnEnable()
        {
            EditorApplication.update += Tick;
            wantsMouseMove = false;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            Stop();
            if (_host != null) DestroyImmediate(_host);
        }

        // ------------------------------------------------------------------ 재생

        void EnsureAudio()
        {
            if (_host != null) return;

            _host = new GameObject("__BpmTapAudio") { hideFlags = HideFlags.HideAndDontSave };
            _music = _host.AddComponent<AudioSource>();
            _music.playOnAwake = false;

            for (int i = 0; i < _clicks.Length; i++)
            {
                _clicks[i] = _host.AddComponent<AudioSource>();
                _clicks[i].playOnAwake = false;
            }

            _clickClip = CreateClick("click", 1000f);
            _accentClip = CreateClick("accent", 1600f);
        }

        static AudioClip CreateClick(string name, float frequency)
        {
            const int rate = 44100;
            int length = rate / 30;
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 90f) * 0.8f;
            }

            AudioClip clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        double SongTime => _playing ? _playFrom + (EditorApplication.timeSinceStartup - _startWall) : _startAt;

        void Play()
        {
            if (_clip == null) return;
            EnsureAudio();

            if (EditorUtility.audioMasterMute)
                Debug.LogWarning("[BPM 탭] 게임 뷰의 'Mute Audio' 가 켜져 있어 소리가 나지 않습니다.");

            _music.Stop();
            _music.clip = _clip;
            _music.time = Mathf.Clamp(_startAt, 0f, _clip.length - 0.01f);

            double now = AudioSettings.dspTime;
            _startDsp = now + StartLeadSeconds;
            _startWall = EditorApplication.timeSinceStartup + StartLeadSeconds;
            _playFrom = _music.time;
            _music.PlayScheduled(_startDsp);

            _lastScheduledBeat = long.MinValue;
            _playing = true;
        }

        void Stop()
        {
            if (!_playing) return;
            _startAt = (float)Math.Min(SongTime, _clip != null ? _clip.length : 0f);
            _playing = false;
            if (_music != null) _music.Stop();
            foreach (AudioSource click in _clicks)
                if (click != null) click.Stop();
        }

        void Tick()
        {
            if (!_playing) return;

            if (_clip == null || SongTime >= _clip.length)
            {
                Stop();
                _startAt = 0f;
                Repaint();
                return;
            }

            if (_metronome) ScheduleClicks();
            Repaint();
        }

        /// 가까운 미래의 박을 PlayScheduled 로 예약한다. 박마다 소스를 번갈아 써서
        /// 직전 예약을 덮어쓰지 않게 한다(PlayScheduled 는 소스당 하나만 걸린다).
        void ScheduleClicks()
        {
            if (_bpm <= 0.0) return;
            double period = 60.0 / _bpm;

            double horizon = SongTime + ScheduleAheadSeconds;
            long beat = (long)Math.Ceiling((SongTime - _offset) / period);
            if (beat <= _lastScheduledBeat) beat = _lastScheduledBeat + 1;

            for (; _offset + beat * period <= horizon; beat++)
            {
                double beatSongTime = _offset + beat * period;
                if (beatSongTime < _playFrom) continue;

                AudioSource source = _clicks[_nextClickSource];
                _nextClickSource = (_nextClickSource + 1) % _clicks.Length;

                bool accent = _beatsPerBar > 0 && ((beat % _beatsPerBar) + _beatsPerBar) % _beatsPerBar == 0;
                source.clip = accent ? _accentClip : _clickClip;
                source.PlayScheduled(_startDsp + (beatSongTime - _playFrom));
                _lastScheduledBeat = beat;
            }
        }

        // ------------------------------------------------------------------ 탭 / 회귀

        void Tap()
        {
            double time = SongTime;
            if (_taps.Count > 0 && time - _taps[_taps.Count - 1] > TapResetGapSeconds) _taps.Clear();
            _taps.Add(time);
            Fit();
        }

        /// t_i = a + b·i 최소제곱. b = 박 간격, a mod b = 첫 박 위치.
        // 계산은 런타임 공용 BpmEstimator로 옮김(채보 에디터와 같은 식을 쓰도록).
        void Fit() => _hasFit = RhythmCP.Chart.BpmEstimator.TryFit(_taps, out _fitBpm, out _fitOffset, out _fitJitterMs);

        double FitOffsetFor(double bpm) => RhythmCP.Chart.BpmEstimator.OffsetFor(_taps, bpm);

        static double Mod(double value, double period) => ((value % period) + period) % period;

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            HandleKeys();

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(_playing))
                _clip = (AudioClip)EditorGUILayout.ObjectField("곡", _clip, typeof(AudioClip), false);

            if (_clip == null)
            {
                EditorGUILayout.HelpBox("AudioClip 을 넣어주세요.", MessageType.Info);
                return;
            }

            DrawTransport();
            EditorGUILayout.Space(8f);
            DrawTapArea();
            EditorGUILayout.Space(8f);
            DrawResult();
        }

        void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.T)
            {
                if (_playing) Tap();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (_playing) Stop(); else Play();
                e.Use();
            }
        }

        void DrawTransport()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = _playing ? new Color(1f, 0.6f, 0.5f) : new Color(0.45f, 1f, 0.6f);
            if (GUILayout.Button(_playing ? "정지 (Enter)" : "재생 (Enter)", GUILayout.Width(100f), GUILayout.Height(24f)))
            {
                if (_playing) Stop(); else Play();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("처음으로", GUILayout.Width(70f), GUILayout.Height(24f)))
            {
                bool wasPlaying = _playing;
                Stop();
                _startAt = 0f;
                if (wasPlaying) Play();
            }

            _metronome = GUILayout.Toggle(_metronome, "메트로놈", "Button", GUILayout.Width(70f), GUILayout.Height(24f));
            EditorGUILayout.EndHorizontal();

            double time = SongTime;
            EditorGUI.BeginChangeCheck();
            float seek = EditorGUILayout.Slider($"{FormatTime(time)} / {FormatTime(_clip.length)}", (float)time, 0f, _clip.length);
            if (EditorGUI.EndChangeCheck())
            {
                bool wasPlaying = _playing;
                Stop();
                _startAt = seek;
                if (wasPlaying) Play();
            }
        }

        void DrawTapArea()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 70f, GUILayout.ExpandWidth(true));
            bool recent = _playing && _taps.Count > 0 && SongTime - _taps[_taps.Count - 1] < 0.1;

            EditorGUI.DrawRect(rect, recent ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.2f, 0.2f, 0.2f));
            var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            style.normal.textColor = Color.white;
            string label = _playing ? $"박자에 맞춰 스페이스 / 클릭  ({_taps.Count})" : "재생 후 탭하세요";
            GUI.Label(rect, label, style);

            if (_playing && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Tap();
                Event.current.Use();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("탭 초기화", GUILayout.Width(80f))) { _taps.Clear(); _hasFit = false; }
            EditorGUILayout.LabelField($"{TapResetGapSeconds:F0}초 이상 쉬면 자동으로 새로 셉니다.", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawResult()
        {
            EditorGUILayout.LabelField("탭 결과", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!_hasFit)
            {
                EditorGUILayout.LabelField($"탭 {MinTapsForResult}번 이상 필요합니다.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"BPM {_fitBpm:F2}   오프셋 {_fitOffset * 1000.0:F0} ms   흔들림 ±{_fitJitterMs:F0} ms");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("그대로 적용")) { _bpm = _fitBpm; _offset = _fitOffset; _lastScheduledBeat = long.MinValue; }
                if (GUILayout.Button("정수 BPM 으로 적용"))
                {
                    _bpm = Math.Round(_fitBpm);
                    _offset = FitOffsetFor(_bpm);
                    _lastScheduledBeat = long.MinValue;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("확정값 (메트로놈 기준)", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _bpm = Math.Max(1.0, EditorGUILayout.DoubleField("BPM", _bpm));
            double offsetMs = EditorGUILayout.DoubleField("첫 박 오프셋 (ms)", _offset * 1000.0);
            _beatsPerBar = Mathf.Clamp(EditorGUILayout.IntField("마디당 박", _beatsPerBar), 0, 16);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (int step in new[] { -10, -1, 1, 10 })
            {
                if (GUILayout.Button($"{step:+#;-#} ms", GUILayout.Width(56f))) offsetMs += step;
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                _offset = Mod(offsetMs / 1000.0, 60.0 / _bpm);
                _lastScheduledBeat = long.MinValue;
            }

            if (GUILayout.Button("클립보드에 복사"))
            {
                EditorGUIUtility.systemCopyBuffer = $"bpm: {_bpm:0.###}\noffset: {_offset:0.000}";
                ShowNotification(new GUIContent("복사됨"));
            }

            EditorGUILayout.EndVertical();
        }

        static string FormatTime(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
            return $"{(int)span.TotalMinutes}:{span.Seconds:00}.{span.Milliseconds / 10:00}";
        }
    }
}
