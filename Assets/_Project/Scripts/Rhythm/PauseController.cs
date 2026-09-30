using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmCP.Rhythm
{
    public enum PauseState
    {
        Playing,
        Paused,
        Countdown,
    }

    /// 일시정지 흐름: Esc / HUD 버튼 / 창 포커스 잃음 → 일시정지 → 계속 → 3·2·1 → 멈춘 자리부터 재개(⑤ 확정).
    /// 메뉴 UI는 이 컴포넌트를 참조해 버튼을 연결하고, 이 컴포넌트는 UI를 모른다(상태 이벤트만 낸다).
    public class PauseController : MonoBehaviour
    {
        [SerializeField] RhythmSession _session;
        [SerializeField] SongClock _clock;
        [SerializeField] JudgementSystem _judgement;

        // TODO(연출): 카세트 플레이어 모양 버튼 + 누를 때 연출 고려 중 — 카타나 제로 UI 참고.
        [Tooltip("HUD 우하단 ‖ 버튼.")]
        [SerializeField] Button _hudPauseButton;

        [SerializeField] int _countdownFrom = 3;

        [Tooltip("카운트다운 한 칸(실시간 초). 일시정지 중 timeScale=0이라 실시간으로 잰다.")]
        [SerializeField] float _countdownStepSec = 1f;

        [Tooltip("창 포커스를 잃으면 자동 일시정지. 에디터에선 인스펙터 클릭만 해도 걸리니 테스트 중엔 꺼도 된다.")]
        [SerializeField] bool _pauseOnFocusLost = true;

        public event Action<PauseState> StateChanged;

        /// 3, 2, 1 → 0(재개 순간). 취소되면 -1.
        public event Action<int> CountdownTick;

        InputAction _pauseKey;
        int _overlayDepth;
        int _overlayClosedFrame = -1;

        public PauseState State { get; private set; } = PauseState.Playing;

        void Awake()
        {
            _pauseKey = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _pauseKey.performed += _ => OnPauseKey();
        }

        void OnEnable()
        {
            _pauseKey.Enable();
            if (_hudPauseButton != null) _hudPauseButton.onClick.AddListener(Pause);
        }

        void OnDisable()
        {
            _pauseKey.Disable();
            if (_hudPauseButton != null) _hudPauseButton.onClick.RemoveListener(Pause);
        }

        void OnDestroy()
        {
            _pauseKey.Dispose();
            // 씬을 떠날 때 멈춘 채로 남지 않게.
            Time.timeScale = 1f;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _pauseOnFocusLost) Pause();
        }

        /// 설정 창처럼 Esc를 자기가 처리하는 오버레이가 떠 있는 동안 Esc로 재개하지 않게 막는다.
        public void PushOverlay() => _overlayDepth++;
        public void PopOverlay()
        {
            _overlayDepth = Math.Max(0, _overlayDepth - 1);
            // 오버레이를 닫은 그 Esc가 같은 프레임에 여기로도 들어와 곧바로 재개되는 걸 막는다(콜백 순서 비보장).
            _overlayClosedFrame = Time.frameCount;
        }

        public void Pause()
        {
            if (!_session.IsPlaying) return;
            if (State == PauseState.Countdown) CancelCountdown();
            if (State != PauseState.Playing) return;

            _clock.Pause();
            // timeScale 0: 판정 팝업·캐릭터 반응 등 Time 기반 연출도 같이 멈춘다.
            Time.timeScale = 0f;
            SetState(PauseState.Paused);
        }

        public void Continue()
        {
            if (State != PauseState.Paused) return;
            SetState(PauseState.Countdown);
            StartCoroutine(CountdownRoutine());
        }

        public void Retry() => _session.Retry();

        void OnPauseKey()
        {
            if (_overlayDepth > 0 || Time.frameCount == _overlayClosedFrame) return;
            switch (State)
            {
                case PauseState.Playing: Pause(); break;
                case PauseState.Paused: Continue(); break;
                case PauseState.Countdown: CancelCountdown(); break;
            }
        }

        IEnumerator CountdownRoutine()
        {
            for (int i = _countdownFrom; i >= 1; i--)
            {
                CountdownTick?.Invoke(i);
                // WaitForSecondsRealtime: timeScale 0이라 일반 WaitForSeconds는 영원히 안 끝난다.
                yield return new WaitForSecondsRealtime(_countdownStepSec);
            }

            CountdownTick?.Invoke(0);
            Time.timeScale = 1f;
            _clock.Resume();
            _judgement.SyncHoldsAfterResume();
            SetState(PauseState.Playing);
        }

        void CancelCountdown()
        {
            StopAllCoroutines();
            CountdownTick?.Invoke(-1);
            SetState(PauseState.Paused);
        }

        void SetState(PauseState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
