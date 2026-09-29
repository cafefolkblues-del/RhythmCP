using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;

namespace RhythmCP.UI
{
    /// 체력바. 채움 이미지의 anchorMax.x로 비율을 표현한다
    /// (Image.fillAmount는 스프라이트가 있어야 동작해서, 임시 UI 단계에선 앵커 방식이 더 단순).
    public class HpBarView : MonoBehaviour
    {
        [SerializeField] RhythmHealth _health;
        [SerializeField] RectTransform _fill;
        [SerializeField] TMP_Text _label;

        void OnEnable() => _health.Changed += Refresh;
        void OnDisable() => _health.Changed -= Refresh;

        void Refresh(RhythmHealth health)
        {
            float ratio = health.Max > 0 ? health.Current / health.Max : 0f;
            _fill.anchorMax = new Vector2(ratio, _fill.anchorMax.y);
            // 올림: 홀드 이탈로 0.3이 남았는데 0으로 보이면 죽은 줄 안다.
            _label.text = $"{Mathf.CeilToInt(health.Current)} / {health.Max}";
        }
    }
}
