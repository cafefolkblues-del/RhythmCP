using TMPro;
using UnityEngine;

namespace RhythmCP.Character
{
    /// 반응 상태 표시. 캐릭터에 그 상태 스프라이트가 있으면 교체하고, 없으면 임시 색 + 상태 이름 글자.
    /// 모션(흔들림·스케일 등)은 ⑥ juice. Animator는 트리거를 문자열로 불러야 해서 쓰지 않는다.
    public class CharacterReactionView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _renderer;

        [Tooltip("아트가 없을 때 상태 이름을 띄우는 월드 텍스트.")]
        [SerializeField] TMP_Text _placeholderLabel;

        [Header("임시 색 (아트 없을 때)")]
        [SerializeField] Color _idle = new Color(0.35f, 0.55f, 0.8f, 0.6f);
        [SerializeField] Color _action = new Color(0.55f, 0.75f, 1f, 0.9f);
        [SerializeField] Color _miss = new Color(0.9f, 0.3f, 0.3f, 0.9f);
        [SerializeField] Color _cheer = new Color(1f, 0.85f, 0.3f, 0.9f);
        [SerializeField] Color _surprise = new Color(1f, 0.55f, 0.2f, 0.9f);
        [SerializeField] Color _sad = new Color(0.55f, 0.45f, 0.75f, 0.9f);

        CharacterDefinition _character;
        Sprite _placeholderSprite;
        ReactionState _current = (ReactionState)(-1);

        public ReactionState Current => _current;

        void Awake() => _placeholderSprite = _renderer.sprite;

        public void SetCharacter(CharacterDefinition character)
        {
            _character = character;
            _current = (ReactionState)(-1);
        }

        public void Show(ReactionState state)
        {
            if (state == _current) return;
            _current = state;

            var sprite = _character != null ? _character.GetSprite(state) : null;
            if (sprite != null)
            {
                _renderer.sprite = sprite;
                _renderer.color = Color.white;
                _placeholderLabel.text = string.Empty;
                return;
            }

            _renderer.sprite = _placeholderSprite;
            _renderer.color = PlaceholderColor(state);
            _placeholderLabel.text = state == ReactionState.Idle ? string.Empty : state.ToString();
        }

        Color PlaceholderColor(ReactionState state) => state switch
        {
            ReactionState.Idle => _idle,
            ReactionState.Miss => _miss,
            ReactionState.Cheer => _cheer,
            ReactionState.Surprise => _surprise,
            ReactionState.Sad => _sad,
            _ => _action,
        };
    }
}
