using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 하단 검사기 결과 목록 + 상태 줄. 항목을 누르면 그 위치로 이동.
    public class IssuesView : MonoBehaviour
    {
        [SerializeField] ChartEditorSession _session;
        [SerializeField] Button _rowTemplate;
        [SerializeField] TMP_Text _summary;
        [SerializeField] TMP_Text _status;

        [Tooltip("목록에 보일 최대 개수. 넘치면 요약에 개수만.")]
        [SerializeField] int _maxRows = 40;

        readonly List<Button> _rows = new List<Button>();

        void Start()
        {
            _rowTemplate.gameObject.SetActive(false);
            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy() => _session.StateChanged -= Refresh;

        void Update()
        {
            // 위치·시간은 매 프레임 바뀌므로 상태 줄만 따로 갱신.
            var doc = _session.Document;
            if (doc == null) return;
            double t = _session.Playback.IsPlaying ? _session.Playback.SongTime : _session.CursorSec;
            string pos = BeatGrid.Position(doc.Tempo.SecToBeat(t), _session.SnapDivision);
            string save = doc.IsDirty ? "저장 대기" : "저장됨";
            string mode = _session.IsRecording ? "● 녹음" : _session.Playback.IsPlaying ? "▶ 재생" : "■ 정지";
            _status.text = $"{mode}   {pos}   {Format(t)}   노트 {doc.Data.notes.Count}   {save}   {_session.LastMessage}";
        }

        void Refresh()
        {
            var issues = _session.Issues;
            int errors = issues.FindAll(i => i.Severity == IssueSeverity.Error).Count;
            _summary.text = issues.Count == 0 ? "검사기: 문제 없음" : $"검사기: 오류 {errors} · 경고 {issues.Count - errors}";

            int shown = Mathf.Min(issues.Count, _maxRows);
            while (_rows.Count < shown)
            {
                var row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
                int index = _rows.Count;
                row.onClick.AddListener(() => Jump(index));
                _rows.Add(row);
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < shown;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;
                var issue = issues[i];
                string mark = issue.Severity == IssueSeverity.Error ? "<color=#ff6b6b>오류</color>"
                    : issue.Severity == IssueSeverity.Warning ? "<color=#ffd166>경고</color>" : "<color=#9ad>정보</color>";
                _rows[i].GetComponentInChildren<TMP_Text>().text = $"{mark}  {BeatGrid.Position(issue.Beat, _session.SnapDivision)}  {issue.Message}";
            }
        }

        void Jump(int index)
        {
            if (index < _session.Issues.Count) _session.JumpToBeat(_session.Issues[index].Beat);
        }

        static string Format(double sec)
        {
            bool neg = sec < 0;
            sec = System.Math.Abs(sec);
            int m = (int)(sec / 60);
            return $"{(neg ? "-" : "")}{m:00}:{sec - m * 60:00.000}";
        }
    }
}
