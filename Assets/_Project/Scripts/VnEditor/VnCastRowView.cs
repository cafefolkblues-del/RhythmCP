using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 출연진 한 행(템플릿): 캐릭터 이름 · 이 에피소드 시점의 호칭 · 빼기.
    public class VnCastRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_InputField _honorific;
        [SerializeField] Button _remove;

        public event Action<string, string> HonorificEdited;
        public event Action<string> RemoveClicked;

        string _characterId;

        void Awake()
        {
            _honorific.onEndEdit.AddListener(v => HonorificEdited?.Invoke(_characterId, v));
            _remove.onClick.AddListener(() => RemoveClicked?.Invoke(_characterId));
        }

        public void Set(string id, string displayName, string honorific)
        {
            _characterId = id;
            _name.text = $"{displayName} ({id})";
            if (!_honorific.isFocused) _honorific.SetTextWithoutNotify(honorific ?? string.Empty);
        }
    }
}
