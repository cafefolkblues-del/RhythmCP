using RhythmCP.Rhythm;
using UnityEngine;

namespace RhythmCP.UI
{
    /// 피버 게이지(하단) + 발동 중 화면 테두리. 테두리 = 캐릭터 공통 연출(Q6), 캐릭터별 스윕은 ⑥.
    public class FeverView : MonoBehaviour
    {
        [SerializeField] FeverGauge _fever;

        [Tooltip("게이지 채움. anchorMax.x로 비율 표현(HpBarView와 같은 방식).")]
        [SerializeField] RectTransform _fill;

        [SerializeField] GameObject _activeLabel;
        [SerializeField] GameObject _border;

        void OnEnable()
        {
            _fever.Changed += Refresh;
            _fever.Started += OnToggled;
            _fever.Ended += OnToggled;
        }

        void OnDisable()
        {
            _fever.Changed -= Refresh;
            _fever.Started -= OnToggled;
            _fever.Ended -= OnToggled;
        }

        void Start() => OnToggled(_fever);

        void Refresh(FeverGauge fever) => _fill.anchorMax = new Vector2(fever.Fill, _fill.anchorMax.y);

        void OnToggled(FeverGauge fever)
        {
            _border.SetActive(fever.IsActive);
            _activeLabel.SetActive(fever.IsActive);
        }
    }
}
