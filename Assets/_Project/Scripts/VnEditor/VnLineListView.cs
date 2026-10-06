using System.Collections.Generic;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 왼쪽 라인 목록. 한 줄 = "L007  유메  앉아요. 혼자…  ◆2  [if]". mc 화자는 기울임.
    /// 클릭 = 선택, Shift = 범위, Ctrl = 추가/빼기. 검사기 오류·경고는 앞에 색 점, 미리보기 플래그로 가려지는 줄은 흐리게.
    public class VnLineListView : MonoBehaviour
    {
        [SerializeField] VnEditorSession _session;

        [Tooltip("행 템플릿(자식 TMP_Text). 같은 부모(ScrollRect content, Vertical Layout Group)에 복제된다.")]
        [SerializeField] Button _rowTemplate;
        [SerializeField] ScrollRect _scroll;

        [SerializeField] Button _add;
        [SerializeField] Button _duplicate;
        [SerializeField] Button _delete;
        [SerializeField] Button _up;
        [SerializeField] Button _down;

        [SerializeField] Color _selected = new Color(0.35f, 0.5f, 0.8f, 1f);
        [SerializeField] Color _normal = new Color(1f, 1f, 1f, 0.06f);

        [Tooltip("행 미리보기 글자 수.")]
        [SerializeField] int _previewChars = 22;

        readonly List<Button> _rows = new List<Button>();
        readonly List<TMP_Text> _labels = new List<TMP_Text>();
        int _lastPrimary = -1;

        void Start()
        {
            _rowTemplate.gameObject.SetActive(false);
            _add.onClick.AddListener(_session.InsertAfterSelection);
            _duplicate.onClick.AddListener(_session.DuplicateSelection);
            _delete.onClick.AddListener(_session.DeleteSelection);
            _up.onClick.AddListener(() => _session.MoveSelection(-1));
            _down.onClick.AddListener(() => _session.MoveSelection(1));
            _session.Redraw += Refresh;
            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            _session.Redraw -= Refresh;
            _session.StateChanged -= Refresh;
        }

        void Refresh()
        {
            var ep = _session.Episode;
            int count = ep?.lines.Count ?? 0;

            while (_rows.Count < count)
            {
                var row = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
                int index = _rows.Count;
                row.onClick.AddListener(() => OnRowClicked(index));
                _rows.Add(row);
                _labels.Add(row.GetComponentInChildren<TMP_Text>());
            }

            var severity = WorstIssuePerLine(count);
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < count;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;

                var line = ep.lines[i];
                bool hidden = !VnCondition.Evaluate(line.condition, _session.PreviewFlags);
                _labels[i].text = Describe(line, severity[i], hidden);
                _labels[i].alpha = hidden ? 0.45f : 1f;
                if (_rows[i].targetGraphic != null) _rows[i].targetGraphic.color = _session.Selection.Contains(i) ? _selected : _normal;
            }

            bool hasLine = _session.PrimaryLine != null;
            _duplicate.interactable = hasLine;
            _delete.interactable = hasLine;
            _up.interactable = hasLine && _session.Primary > 0;
            _down.interactable = hasLine && _session.Primary < count - 1;
            _add.interactable = ep != null;

            if (_session.Primary != _lastPrimary)
            {
                _lastPrimary = _session.Primary;
                KeepVisible(_session.Primary, count);
            }
        }

        string Describe(VnLine line, VnIssueSeverity? severity, bool hidden)
        {
            string dot = severity == VnIssueSeverity.Error ? "<color=#ff6b6b>●</color> "
                : severity == VnIssueSeverity.Warning ? "<color=#ffd166>●</color> " : "  ";
            // 승인 구조: mc는 기울임(비가시 남주라 화면에 없는 화자임을 목록에서도 구분).
            string speaker = line.speaker == VnIds.Mc ? (string.IsNullOrEmpty(line.asCharacter) ? "<i>mc</i>" : $"<i>mc({line.asCharacter})</i>")
                : line.speaker == VnIds.Narration ? "<color=#aaa>나레이션</color>" : _session.Catalog.DisplayName(line.speaker);
            string text = line.text ?? string.Empty;
            if (text.Length > _previewChars) text = text.Substring(0, _previewChars) + "…";
            string choice = line.IsChoice ? $"  <color=#9ad>◆{line.choice.Count}</color>" : "";
            string cond = line.condition != null ? (hidden ? "  <color=#888>[if 가려짐]</color>" : "  <color=#9ad>[if]</color>") : "";
            return $"{dot}<color=#999>{line.id}</color>  {speaker}  {text}{choice}{cond}";
        }

        VnIssueSeverity?[] WorstIssuePerLine(int count)
        {
            var result = new VnIssueSeverity?[count];
            foreach (var issue in _session.Issues)
            {
                if (issue.LineIndex < 0 || issue.LineIndex >= count) continue;
                var cur = result[issue.LineIndex];
                if (cur == null || issue.Severity < cur) result[issue.LineIndex] = issue.Severity;
            }
            return result;
        }

        void OnRowClicked(int index)
        {
            var kb = Keyboard.current;
            bool shift = kb != null && kb.shiftKey.isPressed;
            bool ctrl = kb != null && kb.ctrlKey.isPressed;
            _session.Select(index, ctrl, shift);
        }

        /// 키보드로 선택이 목록 밖으로 나가면 따라 스크롤.
        void KeepVisible(int index, int count)
        {
            if (_scroll == null || index < 0 || count <= 1) return;
            Canvas.ForceUpdateCanvases();
            var content = _scroll.content;
            var viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
            var row = (RectTransform)_rows[index].transform;
            float rowTop = -row.anchoredPosition.y - row.rect.height * (1f - row.pivot.y);
            float rowBottom = rowTop + row.rect.height;
            float viewTop = content.anchoredPosition.y;
            float viewBottom = viewTop + viewport.rect.height;
            if (rowTop < viewTop) content.anchoredPosition = new Vector2(content.anchoredPosition.x, rowTop);
            else if (rowBottom > viewBottom) content.anchoredPosition = new Vector2(content.anchoredPosition.x, rowBottom - viewport.rect.height);
        }
    }
}
