using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 하단 검사기 결과 목록(채보 에디터 IssuesView와 같은 모양). 항목을 누르면 그 줄 선택.
    public class VnIssuesView : MonoBehaviour
    {
        [SerializeField] VnEditorSession _session;
        [SerializeField] Button _rowTemplate;
        [SerializeField] TMP_Text _summary;

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

        void Refresh()
        {
            var issues = _session.Issues;
            int errors = issues.FindAll(i => i.Severity == VnIssueSeverity.Error).Count;
            int warnings = issues.FindAll(i => i.Severity == VnIssueSeverity.Warning).Count;
            _summary.text = issues.Count == 0 ? "검사기: 문제 없음" : $"검사기: 오류 {errors} · 경고 {warnings}" + (issues.Count > _maxRows ? $"  (앞 {_maxRows}개만 표시)" : "");

            int shown = Mathf.Min(issues.Count, _maxRows);
            while (_rows.Count < shown)
            {
                var row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
                int index = _rows.Count;
                row.onClick.AddListener(() => Jump(index));
                _rows.Add(row);
            }
            var ep = _session.Episode;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < shown;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;
                var issue = issues[i];
                string mark = issue.Severity == VnIssueSeverity.Error ? "<color=#ff6b6b>오류</color>"
                    : issue.Severity == VnIssueSeverity.Warning ? "<color=#ffd166>경고</color>" : "<color=#9ad>정보</color>";
                string where = issue.LineIndex < 0 || ep == null ? "에피소드" : ep.lines[issue.LineIndex].id;
                _rows[i].GetComponentInChildren<TMP_Text>().text = $"{mark}  {where}  {issue.Message}";
            }
        }

        void Jump(int index)
        {
            if (index < _session.Issues.Count && _session.Issues[index].LineIndex >= 0)
                _session.Select(_session.Issues[index].LineIndex, false, false);
        }
    }
}
