using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmCP.Vn
{
    /// VN 키 입력. 액션은 코드로 정의(RhythmInput과 같은 방식).
    /// 마우스 클릭 진행은 여기서 받지 않는다 — 화면 전체 투명 버튼(VnPlaySession._advanceArea)이 받아야
    /// 선택지·메뉴 버튼 위 클릭이 진행으로 새지 않는다.
    public class VnInput : MonoBehaviour
    {
        public event Action Advance;

        /// 0부터. 1번 키 = 0.
        public event Action<int> ChoiceKey;

        /// 휠 위 = 백로그 열기.
        public event Action Backlog;

        /// Esc · 우클릭 = 열린 창 닫기.
        public event Action Cancel;

        /// Ctrl을 누르는 동안 일시 스킵.
        public bool SkipHeld => _skipHold.IsPressed();

        InputAction _advance, _backlog, _cancel, _skipHold;
        readonly InputAction[] _choices = new InputAction[9];

        void Awake()
        {
            _advance = new InputAction("VnAdvance", InputActionType.Button);
            _advance.AddBinding("<Keyboard>/space");
            _advance.AddBinding("<Keyboard>/enter");
            _advance.AddBinding("<Keyboard>/numpadEnter");
            _advance.AddBinding("<Keyboard>/z");
            _advance.AddBinding("<Mouse>/scroll/down");
            _advance.performed += _ => Advance?.Invoke();

            _backlog = new InputAction("VnBacklog", InputActionType.Button, "<Mouse>/scroll/up");
            _backlog.performed += _ => Backlog?.Invoke();

            _cancel = new InputAction("VnCancel", InputActionType.Button);
            _cancel.AddBinding("<Keyboard>/escape");
            _cancel.AddBinding("<Mouse>/rightButton");
            _cancel.performed += _ => Cancel?.Invoke();

            _skipHold = new InputAction("VnSkipHold", InputActionType.Button);
            _skipHold.AddBinding("<Keyboard>/leftCtrl");
            _skipHold.AddBinding("<Keyboard>/rightCtrl");

            for (int i = 0; i < _choices.Length; i++)
            {
                int index = i;
                var a = new InputAction($"VnChoice{i + 1}", InputActionType.Button);
                a.AddBinding($"<Keyboard>/{i + 1}");
                a.AddBinding($"<Keyboard>/numpad{i + 1}");
                a.performed += _ => ChoiceKey?.Invoke(index);
                _choices[i] = a;
            }
        }

        void OnEnable()
        {
            _advance.Enable();
            _backlog.Enable();
            _cancel.Enable();
            _skipHold.Enable();
            foreach (var a in _choices) a.Enable();
        }

        void OnDisable()
        {
            _advance.Disable();
            _backlog.Disable();
            _cancel.Disable();
            _skipHold.Disable();
            foreach (var a in _choices) a.Disable();
        }

        void OnDestroy()
        {
            _advance.Dispose();
            _backlog.Dispose();
            _cancel.Dispose();
            _skipHold.Dispose();
            foreach (var a in _choices) a.Dispose();
        }
    }
}
