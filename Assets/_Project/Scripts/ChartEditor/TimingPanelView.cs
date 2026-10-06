using System.Collections.Generic;
using System.Globalization;
using RhythmCP.Chart;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 우측 패널: BPM 목록 · 오프셋 · BPM 탭 · 클라이맥스 · 보조음.
    /// 입력칸을 다 쓰고 나갈 때(onEndEdit) 한 번에 채보에 반영 → 되돌리기 1단위.
    public class TimingPanelView : MonoBehaviour
    {
        [SerializeField] ChartEditorSession _session;

        [Header("BPM 목록 (행 템플릿을 복제)")]
        [SerializeField] RectTransform _bpmRowTemplate;
        [SerializeField] Button _addBpm;
        [SerializeField] TMP_InputField _offsetMs;
        [SerializeField] TMP_Text _tempoError;

        [Header("BPM 탭 (재생 중 T 키 / 버튼)")]
        [SerializeField] Button _tap;
        [SerializeField] TMP_Text _tapResult;
        [SerializeField] Button _tapApply;

        [Header("클라이맥스")]
        [SerializeField] TMP_Text _climax;
        [SerializeField] Button _climaxClear;

        [Header("보조음")]
        [SerializeField] Toggle _assistHits;
        [SerializeField] Toggle _metronome;
        [SerializeField] Toggle _recordSnap;

        readonly List<RectTransform> _rows = new List<RectTransform>();
        readonly List<double> _taps = new List<double>();
        double _fitBpm, _fitOffset;
        bool _hasFit;

        void Start()
        {
            _bpmRowTemplate.gameObject.SetActive(false);
            _addBpm.onClick.AddListener(AddBpmAtCursor);
            _offsetMs.onEndEdit.AddListener(_ => ApplyTiming());
            _tap.onClick.AddListener(Tap);
            _tapApply.onClick.AddListener(ApplyTap);
            _climaxClear.onClick.AddListener(_session.ClearClimax);
            _assistHits.onValueChanged.AddListener(v => _session.Playback.AssistHits = v);
            _metronome.onValueChanged.AddListener(v => _session.Playback.Metronome = v);
            _recordSnap.onValueChanged.AddListener(v => _session.RecordSnap = v);
            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy() => _session.StateChanged -= Refresh;

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame && !_session.IsRecording) Tap();
        }

        void Refresh()
        {
            var doc = _session.Document;
            if (doc == null) return;
            var bpms = doc.Data.bpms;

            while (_rows.Count < bpms.Count)
            {
                var row = Instantiate(_bpmRowTemplate, _bpmRowTemplate.parent);
                row.gameObject.SetActive(true);
                int index = _rows.Count;
                Field(row, "Beat").onEndEdit.AddListener(_ => ApplyTiming());
                Field(row, "Bpm").onEndEdit.AddListener(_ => ApplyTiming());
                row.Find("Delete").GetComponent<Button>().onClick.AddListener(() => RemoveBpm(index));
                _rows.Add(row);
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < bpms.Count;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;
                SetIfIdle(Field(_rows[i], "Beat"), bpms[i].beat.ToString("0.###", CultureInfo.InvariantCulture));
                SetIfIdle(Field(_rows[i], "Bpm"), bpms[i].bpm.ToString("0.###", CultureInfo.InvariantCulture));
                // 첫 BPM은 반드시 0박 — 지울 수 없다.
                _rows[i].Find("Delete").gameObject.SetActive(i > 0);
            }
            _addBpm.transform.SetAsLastSibling();

            SetIfIdle(_offsetMs, (doc.Data.offsetSec * 1000).ToString("0.#", CultureInfo.InvariantCulture));
            _tempoError.text = doc.TempoError ?? "";

            var c = doc.Data.climax;
            _climax.text = c == null ? "없음  ( [ / ] 로 커서 위치 지정 )"
                : $"{BeatGrid.Position(c.startBeat, _session.SnapDivision)} ~ {BeatGrid.Position(c.endBeat, _session.SnapDivision)}  ({doc.Tempo.BeatToSec(c.endBeat) - doc.Tempo.BeatToSec(c.startBeat):0.0}초)";

            _tapResult.text = _hasFit ? $"BPM {_fitBpm:0.00} · 오프셋 {_fitOffset * 1000:0} ms" : $"재생 중 박자에 맞춰 T  ({_taps.Count})";
            _tapApply.interactable = _hasFit;
        }

        /// 상태 갱신은 자주 오므로, 지금 타이핑 중인 칸은 덮어쓰지 않는다.
        static void SetIfIdle(TMP_InputField field, string text)
        {
            if (!field.isFocused) field.SetTextWithoutNotify(text);
        }

        static TMP_InputField Field(RectTransform row, string name) => row.Find(name).GetComponent<TMP_InputField>();

        void ApplyTiming()
        {
            var list = new List<BpmPoint>();
            var bpms = _session.Document.Data.bpms;
            for (int i = 0; i < bpms.Count && i < _rows.Count; i++)
            {
                double beat = Parse(Field(_rows[i], "Beat").text, bpms[i].beat);
                double bpm = Parse(Field(_rows[i], "Bpm").text, bpms[i].bpm);
                list.Add(new BpmPoint { beat = i == 0 ? 0 : beat, bpm = bpm });
            }
            double offset = Parse(_offsetMs.text, _session.Document.Data.offsetSec * 1000) / 1000.0;
            _session.SetTiming(list, offset);
        }

        void AddBpmAtCursor()
        {
            var doc = _session.Document;
            double beat = _session.SnapBeat(doc.Tempo.SecToBeat(_session.CursorSec));
            if (beat <= 0) beat = BeatGrid.BeatsPerBar;
            var list = new List<BpmPoint>();
            foreach (var b in doc.Data.bpms) list.Add(new BpmPoint { beat = b.beat, bpm = b.bpm });
            list.Add(new BpmPoint { beat = beat, bpm = doc.Tempo.BpmAtBeat(beat) });
            _session.SetTiming(list, doc.Data.offsetSec);
        }

        void RemoveBpm(int index)
        {
            var doc = _session.Document;
            if (index <= 0 || index >= doc.Data.bpms.Count) return;
            var list = new List<BpmPoint>();
            for (int i = 0; i < doc.Data.bpms.Count; i++)
                if (i != index) list.Add(new BpmPoint { beat = doc.Data.bpms[i].beat, bpm = doc.Data.bpms[i].bpm });
            _session.SetTiming(list, doc.Data.offsetSec);
        }

        void Tap()
        {
            if (!_session.Playback.IsPlaying) return;
            double t = _session.Playback.SongTime;
            // 2초 넘게 쉬면 새로 시작(BPM 탭 창과 같은 규칙).
            if (_taps.Count > 0 && t - _taps[_taps.Count - 1] > 2.0) _taps.Clear();
            _taps.Add(t);
            _hasFit = BpmEstimator.TryFit(_taps, out _fitBpm, out _fitOffset, out _);
            Refresh();
        }

        /// 탭 결과는 고정 BPM 곡 전용 — BPM 목록을 통째로 [0박: 탭 BPM]으로 바꾼다.
        void ApplyTap()
        {
            if (!_hasFit) return;
            double bpm = System.Math.Round(_fitBpm);
            double offset = BpmEstimator.OffsetFor(_taps, bpm);
            _session.SetTiming(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = bpm } }, offset);
            _taps.Clear();
            _hasFit = false;
        }

        static double Parse(string s, double fallback) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
