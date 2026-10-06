using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 세이브 슬롯 한 칸(프리팹): 번호 · 에피소드/라인 · 저장 시각 · 대사 미리보기.
    public class VnSaveSlotView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _detail;

        public void Set(int slot, VnSaveData data, bool interactable, Action onClick)
        {
            _title.text = data == null ? $"{slot + 1:00}  빈 슬롯" : $"{slot + 1:00}  {data.episodeId} · {data.lineId}";
            _detail.text = data == null ? string.Empty
                : string.IsNullOrEmpty(data.speakerName) ? $"{data.savedAt}\n{data.preview}"
                : $"{data.savedAt}\n{data.speakerName}: {data.preview}";
            _button.interactable = interactable;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick());
        }
    }
}
