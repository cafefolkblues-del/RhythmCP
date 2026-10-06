using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// Graphic 색 하나를 목표색으로 선형 보간. 뷰의 Update에서 Tick을 부른다.
    /// DOTween 같은 패키지 없이 쓰려고 직접 둔다(연출이 페이드뿐이라 이걸로 충분).
    public class VnColorTween
    {
        readonly Graphic _target;
        Color _from, _to;
        float _t = 1f, _duration;
        Action _done;

        public VnColorTween(Graphic target) => _target = target;

        public bool Running => _t < 1f;
        public Color Target => _to;

        /// duration 0 이하면 즉시. done은 도착했을 때 한 번(즉시면 바로).
        public void To(Color to, float duration, Action done = null)
        {
            _from = _target.color;
            _to = to;
            _done = done;
            _duration = duration;
            _t = 0f;
            if (duration <= 0f) Finish();
        }

        public void Tick(float dt)
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + dt / _duration);
            _target.color = Color.Lerp(_from, _to, _t);
            if (_t >= 1f) Finish();
        }

        void Finish()
        {
            _t = 1f;
            _target.color = _to;
            var done = _done;
            _done = null;
            done?.Invoke();
        }
    }
}
