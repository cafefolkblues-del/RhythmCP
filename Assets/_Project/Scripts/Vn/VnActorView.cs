using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 무대 위 캐릭터 스프라이트 한 장(프리팹). 등장 = 검은색에서 밝아짐, 퇴장 = 검은색으로 사라짐, 비화자 = 어둡게.
    /// 스프라이트가 없으면 캐릭터 임시색 사각형 + 이름·표정 글자.
    public class VnActorView : MonoBehaviour
    {
        [SerializeField] Image _image;

        [Tooltip("아트가 없을 때 이름·표정을 띄우는 글자.")]
        [SerializeField] TMP_Text _placeholderLabel;

        static readonly Color Gone = new Color(0f, 0f, 0f, 0f);

        VnColorTween _tween;
        VnCharacter _character;
        string _id;
        Color _base = Color.white;
        Color _tint = Color.white;
        bool _leaving;

        public RectTransform Rect => (RectTransform)transform;
        public bool Leaving => _leaving;

        void Awake() => _tween = new VnColorTween(_image);

        void Update()
        {
            _tween.Tick(Time.unscaledDeltaTime);
            if (_placeholderLabel != null) _placeholderLabel.alpha = _image.color.a;
        }

        public void Setup(string id, VnCharacter character)
        {
            _id = id;
            _character = character;
            _image.color = Gone;
        }

        public void SetExpression(string expr)
        {
            var sprite = _character != null ? _character.GetSprite(expr) : null;
            _image.sprite = sprite;
            _base = sprite != null ? Color.white : (_character != null ? _character.PlaceholderColor : new Color(0.6f, 0.6f, 0.6f));
            if (_placeholderLabel != null)
            {
                string name = _character != null ? _character.DisplayName : _id;
                _placeholderLabel.text = sprite != null ? string.Empty : string.IsNullOrEmpty(expr) ? name : $"{name}\n<size=70%>{expr}</size>";
            }
            // 색은 여기서 바로 바꾸지 않는다 — 이어서 부르는 SetTint가 지금 색(등장 직후면 검정 투명)에서 보간한다.
            // 여기서 바꾸면 새 캐릭터가 검정에서 밝아지는 등장 페이드가 사라진다.
        }

        /// 비화자면 어둡게. 진행 중인 등장 페이드도 새 목표색으로 이어간다.
        public void SetTint(Color tint, float duration)
        {
            if (_leaving) return;
            _tint = tint;
            _tween.To(_base * tint, duration);
        }

        public void Exit(float duration)
        {
            if (_leaving) return;
            _leaving = true;
            // 검은색으로 어두워지며 투명해진다 — 회색 배경 위에서 "검은색으로 사라지는" 느낌.
            _tween.To(Gone, duration, () => Destroy(gameObject));
        }
    }
}
