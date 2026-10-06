using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 선택지 오버레이. 버튼 프리팹을 항목 수만큼 만든다. 고른 결과는 플래그만 올린다(분기 없음, 관람형).
    public class VnChoiceView : MonoBehaviour
    {
        [SerializeField] GameObject _panel;

        [Tooltip("버튼이 쌓일 곳(Vertical Layout Group 권장).")]
        [SerializeField] RectTransform _list;

        [Tooltip("자식에 TMP_Text가 있는 Button.")]
        [SerializeField] Button _buttonPrefab;

        readonly List<Button> _buttons = new List<Button>();
        Action<int> _onChosen;

        public bool IsOpen => _panel.activeSelf;
        public int Count => _buttons.Count;

        void Awake() => _panel.SetActive(false);

        public void Show(IReadOnlyList<VnChoice> choices, Action<int> onChosen)
        {
            Clear();
            _onChosen = onChosen;
            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                var button = Instantiate(_buttonPrefab, _list);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"{i + 1}. {choices[i].text}";
                button.onClick.AddListener(() => Choose(index));
                _buttons.Add(button);
            }
            _panel.SetActive(true);
        }

        /// 숫자키 선택용. 범위 밖이면 무시.
        public void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= _buttons.Count) return;
            var callback = _onChosen;
            Hide();
            callback?.Invoke(index);
        }

        public void Hide()
        {
            Clear();
            _panel.SetActive(false);
        }

        void Clear()
        {
            foreach (var b in _buttons) Destroy(b.gameObject);
            _buttons.Clear();
            _onChosen = null;
        }
    }
}
