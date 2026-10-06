using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 우측 패널 "자동 채보": 분석 실행 → 분석 BPM 적용·다운비트 맞추기 → 목표 NPS·셋잇단 → EASY 생성.
    public class AutoChartPanelView : MonoBehaviour
    {
        [SerializeField] ChartEditorSession _session;
        [SerializeField] TMP_Text _status;
        [SerializeField] Button _analyze;
        [SerializeField] Button _reanalyze;
        [SerializeField] Button _applyBpm;
        [SerializeField] Button _half;
        [SerializeField] Button _double;
        [SerializeField] Button _downbeatEarlier;
        [SerializeField] Button _downbeatLater;
        [SerializeField] TMP_InputField _targetNps;
        [SerializeField] Toggle _triplets;
        [SerializeField] Button _generate;

        void Start()
        {
            _analyze.onClick.AddListener(() => _session.RunAnalysis(false));
            _reanalyze.onClick.AddListener(() => _session.RunAnalysis(true));
            _applyBpm.onClick.AddListener(() => _session.ApplyAnalysisTiming(_session.Analysis.bpm));
            // 2배·절반: 분석이 8분 하이햇 등에 끌려 옥타브를 틀렸을 때(스펙 §3 STEP 1 "2:1 옥타브 의심").
            _half.onClick.AddListener(() => _session.ApplyAnalysisTiming(_session.Analysis.bpm / 2));
            _double.onClick.AddListener(() => _session.ApplyAnalysisTiming(_session.Analysis.bpm * 2));
            _downbeatEarlier.onClick.AddListener(() => _session.ShiftDownbeat(-1));
            _downbeatLater.onClick.AddListener(() => _session.ShiftDownbeat(1));
            _targetNps.onEndEdit.AddListener(OnNps);
            _triplets.onValueChanged.AddListener(v => _session.AutoParams.allowTriplets = v);
            _generate.onClick.AddListener(_session.GenerateAuto);

            _targetNps.SetTextWithoutNotify(_session.AutoParams.targetNps.ToString("0.##", CultureInfo.InvariantCulture));
            _triplets.SetIsOnWithoutNotify(_session.AutoParams.allowTriplets);
            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy() => _session.StateChanged -= Refresh;

        void OnNps(string text)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0)
                _session.AutoParams.targetNps = v;
            _targetNps.SetTextWithoutNotify(_session.AutoParams.targetNps.ToString("0.##", CultureInfo.InvariantCulture));
        }

        void Refresh()
        {
            var a = _session.Analysis;
            bool busy = _session.IsAnalyzing;
            bool has = a != null;

            if (busy) _status.text = $"분석 중… ({_session.AnalysisStage})";
            else if (!has) _status.text = "분석 없음 — [분석 실행]";
            else
            {
                string warn = a.warnings.Count > 0 ? "\n<color=#ffd166>" + string.Join("\n", a.warnings) + "</color>" : "";
                _status.text = $"BPM {a.bpm:0.##} · 신뢰도 {a.bpmConfidence:0.00} · 온셋 {a.onsets.Count}{warn}";
            }

            _analyze.interactable = !busy;
            _reanalyze.interactable = !busy && has;
            _applyBpm.interactable = _half.interactable = _double.interactable = !busy && has;
            _generate.interactable = !busy && has;
        }
    }
}
