using System;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmCP.Settings
{
    /// 공용 설정 창(프리팹). 일시정지 메뉴가 지금 띄우고, 타이틀(M10)도 같은 프리팹을 띄운다.
    /// 플레이 씬을 모른다 — GameSettings만 읽고 쓴다. 자기 Canvas를 가져서 어느 씬에 떨어뜨려도 맨 위에 뜬다.
    /// (씬에 EventSystem이 있어야 버튼이 눌린다 — 각 씬이 하나씩 가진다.)
    public class SettingsOverlay : MonoBehaviour
    {
        public event Action Closed;

        [SerializeField] GameObject _mainPanel;

        [Header("판정 오프셋")]
        [SerializeField] TMP_Text _judgeValue;
        [SerializeField] Button _judgeMinus5;
        [SerializeField] Button _judgeMinus1;
        [SerializeField] Button _judgePlus1;
        [SerializeField] Button _judgePlus5;
        [SerializeField] Button _judgeCalibrate;
        [SerializeField] JudgeCalibrationPanel _calibration;

        [Header("화면 오프셋")]
        [SerializeField] TMP_Text _visualValue;
        [SerializeField] Button _visualMinus5;
        [SerializeField] Button _visualMinus1;
        [SerializeField] Button _visualPlus1;
        [SerializeField] Button _visualPlus5;
        [SerializeField] VisualOffsetPreview _preview;

        [Header("VN")]
        [Tooltip("\"VN만 보기\"(리듬 없이 스토리만). 2026-10-07 VN 메뉴바에서 설정으로 옮김. 비우면 행 없음.")]
        [SerializeField] Toggle _vnOnly;

        [SerializeField] Button _close;

        InputAction _esc;

        void Awake()
        {
            _judgeMinus5.onClick.AddListener(() => GameSettings.JudgeOffsetMs -= 5);
            _judgeMinus1.onClick.AddListener(() => GameSettings.JudgeOffsetMs -= 1);
            _judgePlus1.onClick.AddListener(() => GameSettings.JudgeOffsetMs += 1);
            _judgePlus5.onClick.AddListener(() => GameSettings.JudgeOffsetMs += 5);
            _visualMinus5.onClick.AddListener(() => GameSettings.VisualOffsetMs -= 5);
            _visualMinus1.onClick.AddListener(() => GameSettings.VisualOffsetMs -= 1);
            _visualPlus1.onClick.AddListener(() => GameSettings.VisualOffsetMs += 1);
            _visualPlus5.onClick.AddListener(() => GameSettings.VisualOffsetMs += 5);
            _judgeCalibrate.onClick.AddListener(OpenCalibration);
            _close.onClick.AddListener(Close);
            if (_vnOnly != null) _vnOnly.onValueChanged.AddListener(on => VnPreferences.VnOnly = on);

            _esc = new InputAction("SettingsBack", InputActionType.Button, "<Keyboard>/escape");
            _esc.performed += _ => OnBack();

            _calibration.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameSettings.Changed += Refresh;
            _esc.Enable();
            Refresh();
        }

        void OnDisable()
        {
            GameSettings.Changed -= Refresh;
            _esc.Disable();
        }

        void OnDestroy() => _esc.Dispose();

        void Refresh()
        {
            _judgeValue.text = $"{GameSettings.JudgeOffsetMs:+0;-0;0} ms";
            _visualValue.text = $"{GameSettings.VisualOffsetMs:+0;-0;0} ms";
            if (_vnOnly != null) _vnOnly.SetIsOnWithoutNotify(VnPreferences.VnOnly);
        }

        void OpenCalibration()
        {
            // 두 메트로놈이 겹쳐 울리지 않게 미리보기를 끄고 보정 창만 띄운다.
            _mainPanel.SetActive(false);
            _preview.gameObject.SetActive(false);
            _calibration.Open(OnCalibrationClosed);
        }

        void OnCalibrationClosed()
        {
            _mainPanel.SetActive(true);
            _preview.gameObject.SetActive(true);
        }

        void OnBack()
        {
            if (_calibration.IsOpen) _calibration.Close();
            else Close();
        }

        public void Close()
        {
            Closed?.Invoke();
            Destroy(gameObject);
        }
    }
}
