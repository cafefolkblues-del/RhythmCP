using System;
using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RhythmCP.Rhythm
{
    /// 2버튼(상·하) 입력. 액션은 에셋이 아니라 코드로 정의한다(ForagerCP GameInput과 같은 방식).
    /// 리매핑(M10)이 붙으면 바인딩만 교체하면 되고 이벤트 모양은 그대로다.
    public class RhythmInput : MonoBehaviour
    {
        /// lane, 입력이 실제로 일어난 실시간(Time.realtimeSinceStartupAsDouble 기준).
        public event Action<Lane, double> LanePressed;

        InputAction _top;
        InputAction _bottom;

        void Awake()
        {
            _top = new InputAction("LaneTop", InputActionType.Button);
            _top.AddBinding("<Keyboard>/f");
            _top.AddBinding("<Keyboard>/d");

            _bottom = new InputAction("LaneBottom", InputActionType.Button);
            _bottom.AddBinding("<Keyboard>/j");
            _bottom.AddBinding("<Keyboard>/k");

            _top.performed += ctx => Raise(Lane.Top, ctx);
            _bottom.performed += ctx => Raise(Lane.Bottom, ctx);
        }

        void OnEnable()
        {
            _top.Enable();
            _bottom.Enable();
        }

        void OnDisable()
        {
            _top.Disable();
            _bottom.Disable();
        }

        void OnDestroy()
        {
            _top.Dispose();
            _bottom.Dispose();
        }

        void Raise(Lane lane, InputAction.CallbackContext ctx)
        {
            // ctx.time: 키가 눌린 하드웨어 이벤트 시각. 입력 처리는 프레임 시작에 몰아서 하므로
            // 프레임 시각을 쓰면 프레임 간격만큼 판정이 늦게 찍힌다. 이벤트가 얼마나 묵었는지(age)만큼 되돌린다.
            // InputState.currentTime과 ctx.time은 같은 시간축이라 차이만 쓰면 시간축 변환이 필요 없다.
            double age = Math.Max(0, InputState.currentTime - ctx.time);
            LanePressed?.Invoke(lane, Time.realtimeSinceStartupAsDouble - age);
        }
    }
}
