using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 백로그 오버레이. 열 때마다 목록을 다시 만든다 — 한 에피소드 수백 줄이라 풀링 없이도 충분.
    public class VnBacklogView : MonoBehaviour
    {
        [SerializeField] GameObject _panel;
        [SerializeField] ScrollRect _scroll;

        [Tooltip("ScrollRect content(Vertical Layout Group + Content Size Fitter).")]
        [SerializeField] RectTransform _content;
        [SerializeField] VnBacklogEntryView _entryPrefab;
        [SerializeField] Button _close;

        readonly List<VnBacklogEntryView> _entries = new List<VnBacklogEntryView>();

        public bool IsOpen => _panel.activeSelf;

        void Awake() => _panel.SetActive(false);
        void OnEnable() => _close.onClick.AddListener(Close);
        void OnDisable() => _close.onClick.RemoveListener(Close);

        public void Open(IReadOnlyList<VnBacklogEntry> backlog)
        {
            foreach (var e in _entries) Destroy(e.gameObject);
            _entries.Clear();
            foreach (var entry in backlog)
            {
                var view = Instantiate(_entryPrefab, _content);
                view.Set(entry);
                _entries.Add(view);
            }
            _panel.SetActive(true);

            // 레이아웃이 계산된 뒤에 맨 아래(최신 대사)로.
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = 0f;
        }

        public void Close() => _panel.SetActive(false);
    }
}
