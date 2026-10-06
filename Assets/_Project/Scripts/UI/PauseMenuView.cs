using RhythmCP.Rhythm;
using RhythmCP.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.UI
{
    /// 일시정지 오버레이(와이어프레임 03): 계속(3·2·1) / 리트라이 / 설정·오프셋 / 나가기(허브 전까지 비활성).
    /// 설정은 공용 프리팹 SettingsOverlay를 띄운다 — 타이틀(M10)도 같은 프리팹을 쓴다.
    public class PauseMenuView : MonoBehaviour
    {
        [SerializeField] PauseController _controller;
        [SerializeField] GameObject _panel;
        [SerializeField] Button _continue;
        [SerializeField] Button _retry;
        [SerializeField] Button _settings;

        [Tooltip("허브(M4) 전까지 비활성.")]
        [SerializeField] Button _exit;

        [SerializeField] TMP_Text _countdown;
        [SerializeField] SettingsOverlay _settingsPrefab;

        SettingsOverlay _openSettings;

        void Awake()
        {
            _panel.SetActive(false);
            _countdown.gameObject.SetActive(false);

            // 허브 전까지 비활성. 채보 에디터 테스트 플레이 중엔 에디터로 돌아가는 버튼.
            _exit.interactable = PlaytestHandoff.Active;
            if (PlaytestHandoff.Active)
            {
                var label = _exit.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "에디터로";
                _exit.onClick.AddListener(PlaytestHandoff.ReturnToEditor);
            }
        }

        void OnEnable()
        {
            _controller.StateChanged += OnStateChanged;
            _controller.CountdownTick += OnCountdown;
            _continue.onClick.AddListener(_controller.Continue);
            _retry.onClick.AddListener(_controller.Retry);
            _settings.onClick.AddListener(OpenSettings);
        }

        void OnDisable()
        {
            _controller.StateChanged -= OnStateChanged;
            _controller.CountdownTick -= OnCountdown;
            _continue.onClick.RemoveListener(_controller.Continue);
            _retry.onClick.RemoveListener(_controller.Retry);
            _settings.onClick.RemoveListener(OpenSettings);
        }

        void OnStateChanged(PauseState state) => _panel.SetActive(state == PauseState.Paused && _openSettings == null);

        void OnCountdown(int n)
        {
            _countdown.gameObject.SetActive(n > 0);
            if (n > 0) _countdown.text = n.ToString();
        }

        void OpenSettings()
        {
            if (_openSettings != null) return;
            _panel.SetActive(false);
            _controller.PushOverlay();
            _openSettings = Instantiate(_settingsPrefab);
            _openSettings.Closed += OnSettingsClosed;
        }

        void OnSettingsClosed()
        {
            _openSettings = null;
            _controller.PopOverlay();
            _panel.SetActive(_controller.State == PauseState.Paused);
        }
    }
}
