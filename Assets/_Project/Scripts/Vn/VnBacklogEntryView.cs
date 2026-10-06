using TMPro;
using UnityEngine;

namespace RhythmCP.Vn
{
    /// 백로그 한 줄(프리팹). 화자별 구분: 캐릭터 = 이름, mc = 호칭(흐린 색), 나레이션 = 이름 없음.
    public class VnBacklogEntryView : MonoBehaviour
    {
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_Text _text;
        [SerializeField] Color _characterColor = Color.white;
        [SerializeField] Color _mcColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] Color _narrationColor = new Color(0.8f, 0.8f, 0.8f);

        public void Set(VnBacklogEntry entry)
        {
            _name.text = entry.Name ?? string.Empty;
            _text.text = entry.Text ?? string.Empty;
            var color = entry.Kind switch
            {
                VnSpeakerKind.Mc => _mcColor,
                VnSpeakerKind.Narration => _narrationColor,
                _ => _characterColor,
            };
            _name.color = color;
            _text.color = color;
            _text.fontStyle = entry.Kind == VnSpeakerKind.Narration ? FontStyles.Italic : FontStyles.Normal;
        }
    }
}
