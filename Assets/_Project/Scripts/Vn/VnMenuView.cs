using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 상단 메뉴바(와이어프레임 01): 오토 · 스킵 · 기읽 · 백로그 · 세이브 · 로드 · 설정.
    /// "VN만 보기"는 설정 창으로 옮겼다(2026-10-07 플레이테스트 후). 스킵은 모드가 아니라 확인 팝업 → 다음 선택지까지 건너뛰기.
    /// 켜진 모드(오토·기읽)는 버튼 색으로 표시 — 다른 씬과 같은 색(꺼짐 = 흰 반투명, 켜짐 = 분홍).
    public class VnMenuView : MonoBehaviour
    {
        [SerializeField] Button _auto;
        [SerializeField] Button _skip;
        [SerializeField] Button _readSkip;
        [SerializeField] Button _backlog;
        [SerializeField] Button _save;
        [SerializeField] Button _load;
        [SerializeField] Button _settings;

        [SerializeField] Color _offColor = new Color(1f, 1f, 1f, 0.14f);
        [SerializeField] Color _onColor = new Color(0.9f, 0.3f, 0.5f, 1f);

        public event Action<VnAdvanceMode> ModeClicked;
        public event Action SkipClicked;
        public event Action BacklogClicked;
        public event Action SaveClicked;
        public event Action LoadClicked;
        public event Action SettingsClicked;

        void OnEnable()
        {
            _auto.onClick.AddListener(OnAuto);
            _skip.onClick.AddListener(OnSkip);
            _readSkip.onClick.AddListener(OnReadSkip);
            _backlog.onClick.AddListener(OnBacklog);
            _save.onClick.AddListener(OnSave);
            _load.onClick.AddListener(OnLoad);
            _settings.onClick.AddListener(OnSettings);
        }

        void OnDisable()
        {
            _auto.onClick.RemoveListener(OnAuto);
            _skip.onClick.RemoveListener(OnSkip);
            _readSkip.onClick.RemoveListener(OnReadSkip);
            _backlog.onClick.RemoveListener(OnBacklog);
            _save.onClick.RemoveListener(OnSave);
            _load.onClick.RemoveListener(OnLoad);
            _settings.onClick.RemoveListener(OnSettings);
        }

        void OnAuto() => ModeClicked?.Invoke(VnAdvanceMode.Auto);
        void OnReadSkip() => ModeClicked?.Invoke(VnAdvanceMode.ReadSkip);
        void OnSkip() => SkipClicked?.Invoke();
        void OnBacklog() => BacklogClicked?.Invoke();
        void OnSave() => SaveClicked?.Invoke();
        void OnLoad() => LoadClicked?.Invoke();
        void OnSettings() => SettingsClicked?.Invoke();

        public void ShowMode(VnAdvanceMode mode)
        {
            Tint(_auto, mode == VnAdvanceMode.Auto);
            Tint(_readSkip, mode == VnAdvanceMode.ReadSkip);
        }

        void Tint(Button button, bool on)
        {
            if (button.targetGraphic != null) button.targetGraphic.color = on ? _onColor : _offColor;
        }
    }
}
