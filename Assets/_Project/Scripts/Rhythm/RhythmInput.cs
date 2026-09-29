using System;
using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace RhythmCP.Rhythm
{
    /// 2버튼(상·하) 입력. 액션은 에셋이 아니라 코드로 정의한다(ForagerCP GameInput과 같은 방식).
    /// 리매핑(M10)이 붙으면 바인딩만 교체하면 되고 이벤트 모양은 그대로다.
    public class RhythmInput : MonoBehaviour
    {
        /// lane, 입력이 실제로 일어난 실시간(Time.realtimeSinceStartupAsDouble 기준).
        /// 같은 레인의 다른 키를 누르고 있어도 새로 누를 때마다 발생한다(연타·F/D 번갈아 치기).
        public event Action<Lane, double> LanePressed;

        /// 그 레인에 묶인 키가 전부 떨어진 순간. 홀드 떼는 판정용.
        public event Action<Lane, double> LaneReleased;

        readonly InputAction[] _actions = new InputAction[2];
        readonly bool[] _held = new bool[2];

        public bool IsHeld(Lane lane) => _held[(int)lane];

        void Awake()
        {
            _actions[(int)Lane.Top] = Create("LaneTop", Lane.Top, "<Keyboard>/f", "<Keyboard>/d");
            _actions[(int)Lane.Bottom] = Create("LaneBottom", Lane.Bottom, "<Keyboard>/j", "<Keyboard>/k");
        }

        InputAction Create(string name, Lane lane, params string[] bindings)
        {
            // PassThrough: Button 타입은 한 액션에 키가 여러 개면 "가장 세게 눌린 키" 하나로 합쳐서,
            // F를 누른 채 D를 누르면 두 번째 입력이 사라진다. PassThrough는 키마다 값 변화를 그대로 넘겨준다.
            var action = new InputAction(name, InputActionType.PassThrough);
            foreach (var b in bindings) action.AddBinding(b);

            // PassThrough는 떼는 순간이 performed(값 0)로 올지 canceled로 올지 버전마다 달라 둘 다 받는다.
            action.performed += ctx => OnControl(lane, action, ctx);
            action.canceled += ctx => OnControl(lane, action, ctx);
            return action;
        }

        void OnEnable()
        {
            foreach (var a in _actions) a.Enable();
        }

        void OnDisable()
        {
            foreach (var a in _actions) a.Disable();
        }

        void OnDestroy()
        {
            foreach (var a in _actions) a.Dispose();
        }

        void OnControl(Lane lane, InputAction action, InputAction.CallbackContext ctx)
        {
            // ctx.time: 키가 눌린 하드웨어 이벤트 시각. 입력 처리는 프레임 시작에 몰아서 하므로
            // 프레임 시각을 쓰면 프레임 간격만큼 판정이 늦게 찍힌다. 이벤트가 얼마나 묵었는지(age)만큼 되돌린다.
            // InputState.currentTime과 ctx.time은 같은 시간축이라 차이만 쓰면 시간축 변환이 필요 없다.
            double age = Math.Max(0, InputState.currentTime - ctx.time);
            double realtime = Time.realtimeSinceStartupAsDouble - age;
            int i = (int)lane;

            if (ctx.control is ButtonControl button && button.isPressed)
            {
                _held[i] = true;
                LanePressed?.Invoke(lane, realtime);
            }
            else if (_held[i] && !AnyPressed(action))
            {
                _held[i] = false;
                LaneReleased?.Invoke(lane, realtime);
            }
        }

        static bool AnyPressed(InputAction action)
        {
            foreach (var control in action.controls)
                if (control is ButtonControl b && b.isPressed) return true;
            return false;
        }
    }
}
