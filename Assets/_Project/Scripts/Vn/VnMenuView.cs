using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 상단 메뉴바(와이어프레임 01): 오토 · 스킵 · 기읽 · 백로그 · 세이브 · 로드 · VN만.
    /// 켜진 모드는 버튼 색으로 표시. 동작은 세션이 이벤트를 받아 처리한다.
    public class VnMenuView : MonoBehaviour
    {
        [SerializeField] Button _auto;
        [SerializeField] Button _skip;
        [SerializeField] Button _readSkip;
        [SerializeField] Button _backlog;
        [SerializeField] Button _save;
        [SerializeField] Button _load;
        [SerializeField] Button _vnOnly;

        [SerializeField] Color _offColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] Color _onColor = new Color(1f, 0.85f, 0.35f, 1f);

        public event Action<VnAdvanceMode> ModeClicked;
        public event Action BacklogClicked;
        public event Action SaveClicked;
        public event Action LoadClicked;
        public event Action VnOnlyClicked;

        void OnEnable()
        {
            _auto.onClick.AddListener(OnAuto);
            _skip.onClick.AddListener(OnSkip);
            _readSkip.onClick.AddListener(OnReadSkip);
            _backlog.onClick.AddListener(OnBacklog);
            _save.onClick.AddListener(OnSave);
            _load.onClick.AddListener(OnLoad);
            _vnOnly.onClick.AddListener(OnVnOnly);
        }

        void OnDisable()
        {
            _auto.onClick.RemoveListener(OnAuto);
            _skip.onClick.RemoveListener(OnSkip);
            _readSkip.onClick.RemoveListener(OnReadSkip);
            _backlog.onClick.RemoveListener(OnBacklog);
            _save.onClick.RemoveListener(OnSave);
            _load.onClick.RemoveListener(OnLoad);
            _vnOnly.onClick.RemoveListener(OnVnOnly);
        }

        void OnAuto() => ModeClicked?.Invoke(VnAdvanceMode.Auto);
        void OnSkip() => ModeClicked?.Invoke(VnAdvanceMode.Skip);
        void OnReadSkip() => ModeClicked?.Invoke(VnAdvanceMode.ReadSkip);
        void OnBacklog() => BacklogClicked?.Invoke();
        void OnSave() => SaveClicked?.Invoke();
        void OnLoad() => LoadClicked?.Invoke();
        void OnVnOnly() => VnOnlyClicked?.Invoke();

        public void ShowMode(VnAdvanceMode mode)
        {
            Tint(_auto, mode == VnAdvanceMode.Auto);
            Tint(_skip, mode == VnAdvanceMode.Skip);
            Tint(_readSkip, mode == VnAdvanceMode.ReadSkip);
        }

        public void ShowVnOnly(bool on) => Tint(_vnOnly, on);

        void Tint(Button button, bool on)
        {
            if (button.targetGraphic != null) button.targetGraphic.color = on ? _onColor : _offColor;
        }
    }
}
