using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 확인 팝업 하나(스킵·세이브 덮어쓰기 공용). 일시정지 메뉴와 같은 모양: 화면 어둡게 + 가운데 카드 + 확인(분홍)/취소.
    public class VnConfirmView : MonoBehaviour
    {
        [SerializeField] GameObject _panel;
        [SerializeField] TMP_Text _message;
        [SerializeField] Button _confirm;
        [SerializeField] Button _cancel;

        Action _onConfirm;

        public bool IsOpen => _panel.activeSelf;

        void Awake()
        {
            _panel.SetActive(false);
            _confirm.onClick.AddListener(Confirm);
            _cancel.onClick.AddListener(Close);
        }

        public void Show(string message, Action onConfirm)
        {
            _message.text = message;
            _onConfirm = onConfirm;
            _panel.SetActive(true);
        }

        public void Close()
        {
            _onConfirm = null;
            _panel.SetActive(false);
        }

        void Confirm()
        {
            var action = _onConfirm;
            Close();
            action?.Invoke();
        }
    }
}
