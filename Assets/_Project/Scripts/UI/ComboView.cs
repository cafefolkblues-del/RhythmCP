using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;

namespace RhythmCP.UI
{
    public class ComboView : MonoBehaviour
    {
        [SerializeField] ComboCounter _combo;
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _count;

        [Tooltip("이 콤보 미만이면 숨긴다.")]
        [SerializeField] int _showFrom = 2;

        void OnEnable() => _combo.Changed += Refresh;
        void OnDisable() => _combo.Changed -= Refresh;

        void Refresh(ComboCounter combo)
        {
            _root.SetActive(combo.Combo >= _showFrom);
            _count.text = combo.Combo.ToString();
        }
    }
}
