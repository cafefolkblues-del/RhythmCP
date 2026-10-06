using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 플래그 패널 한 행(템플릿): id · 종류 · 설명 · 미리보기 값(누적 = 숫자 칸, 단일 = 체크) · 삭제.
    public class VnFlagRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text _id;
        [SerializeField] TMP_Text _kind;
        [SerializeField] TMP_InputField _description;
        [SerializeField] TMP_InputField _counterValue;
        [SerializeField] Toggle _boolValue;
        [SerializeField] Button _delete;

        public event Action<string, string> DescriptionEdited;
        public event Action<string, int> CounterEdited;
        public event Action<string, bool> BoolEdited;
        public event Action<string> DeleteClicked;

        string _flagId;

        void Awake()
        {
            _description.onEndEdit.AddListener(v => DescriptionEdited?.Invoke(_flagId, v));
            _counterValue.onEndEdit.AddListener(v =>
            {
                if (int.TryParse(v, out int n)) CounterEdited?.Invoke(_flagId, n);
            });
            _boolValue.onValueChanged.AddListener(on => BoolEdited?.Invoke(_flagId, on));
            _delete.onClick.AddListener(() => DeleteClicked?.Invoke(_flagId));
        }

        public void Set(string id, bool counter, string description, int counterValue, bool boolValue)
        {
            _flagId = id;
            _id.text = id;
            _kind.text = counter ? "누적" : "단일";
            if (!_description.isFocused) _description.SetTextWithoutNotify(description ?? string.Empty);
            _counterValue.gameObject.SetActive(counter);
            _boolValue.gameObject.SetActive(!counter);
            if (!_counterValue.isFocused) _counterValue.SetTextWithoutNotify(counterValue.ToString());
            _boolValue.SetIsOnWithoutNotify(boolValue);
        }
    }
}
