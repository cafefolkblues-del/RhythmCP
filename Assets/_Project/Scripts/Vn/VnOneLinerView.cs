using TMPro;
using UnityEngine;

namespace RhythmCP.Vn
{
    /// "VN만 보기"를 켰을 때 화면 하단에 잠깐 뜨는 캐릭터 한마디("흥미로운 손님이네" 등). 정해진 시간 뒤 사라진다.
    public class VnOneLinerView : MonoBehaviour
    {
        [Tooltip("레이캐스트 끄기 — 떠 있어도 클릭 진행을 막지 않게.")]
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_Text _text;

        [SerializeField] float _fadeSec = 0.3f;

        float _left, _total;

        void Awake() => _group.alpha = 0f;

        public void Show(string name, string text, float seconds)
        {
            _name.text = name;
            _text.text = text;
            _total = seconds;
            _left = seconds;
        }

        void Update()
        {
            if (_left <= 0f) return;
            _left -= Time.unscaledDeltaTime;
            float shown = _total - _left;
            // 처음·끝 _fadeSec 동안 서서히.
            _group.alpha = Mathf.Clamp01(Mathf.Min(shown, _left) / Mathf.Max(0.01f, _fadeSec));
        }
    }
}
