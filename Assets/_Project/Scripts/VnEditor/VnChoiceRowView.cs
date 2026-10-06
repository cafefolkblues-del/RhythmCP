using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 인스펙터 선택지 한 행(템플릿): 번호 · 텍스트 · 효과("foreshadow.hall +1, saw_lock = true") · 삭제.
    public class VnChoiceRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text _number;
        [SerializeField] TMP_InputField _text;
        [SerializeField] TMP_InputField _effects;
        [SerializeField] Button _delete;

        public event Action<int, string> TextEdited;
        public event Action<int, string> EffectsEdited;
        public event Action<int> DeleteClicked;

        int _index;

        void Awake()
        {
            _text.onEndEdit.AddListener(v => TextEdited?.Invoke(_index, v));
            _effects.onEndEdit.AddListener(v => EffectsEdited?.Invoke(_index, v));
            _delete.onClick.AddListener(() => DeleteClicked?.Invoke(_index));
        }

        public void Set(int index, string text, string effects)
        {
            _index = index;
            _number.text = (index + 1).ToString();
            if (!_text.isFocused) _text.SetTextWithoutNotify(text ?? string.Empty);
            if (!_effects.isFocused) _effects.SetTextWithoutNotify(effects ?? string.Empty);
        }
    }
}
