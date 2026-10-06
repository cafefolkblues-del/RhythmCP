using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 세이브/로드 슬롯 화면. 같은 화면을 모드만 바꿔 쓴다.
    // TODO(UI 다듬기): 지금은 차 있는 슬롯에 세이브하면 확인 없이 바로 덮어쓴다(2026-10-07 임시 결정).
    //   덮어쓰기 확인창("슬롯 03을 덮어쓸까요?")을 넣을 것 — Open의 onPicked 앞에서 data != null && _saving이면 확인을 거치게.
    public class VnSaveMenuView : MonoBehaviour
    {
        [SerializeField] GameObject _panel;
        [SerializeField] TMP_Text _heading;
        [SerializeField] RectTransform _list;
        [SerializeField] VnSaveSlotView _slotPrefab;
        [SerializeField] Button _close;

        readonly List<VnSaveSlotView> _slots = new List<VnSaveSlotView>();
        VnSaveStore _store;
        bool _saving;
        Action<int> _onPicked;

        public bool IsOpen => _panel.activeSelf;

        void Awake() => _panel.SetActive(false);
        void OnEnable() => _close.onClick.AddListener(Close);
        void OnDisable() => _close.onClick.RemoveListener(Close);

        /// saving = 세이브 모드(모든 칸 선택 가능), 아니면 로드 모드(빈 칸 비활성).
        public void Open(VnSaveStore store, bool saving, Action<int> onPicked)
        {
            _store = store;
            _saving = saving;
            _onPicked = onPicked;
            _heading.text = saving ? "세이브" : "로드";
            Refresh();
            _panel.SetActive(true);
        }

        /// 세이브 직후 목록 갱신용.
        public void Refresh()
        {
            while (_slots.Count < _store.SlotCount) _slots.Add(Instantiate(_slotPrefab, _list));
            for (int i = 0; i < _slots.Count; i++)
            {
                int slot = i;
                var data = _store.Load(i);
                _slots[i].gameObject.SetActive(i < _store.SlotCount);
                _slots[i].Set(i, data, _saving || data != null, () => _onPicked?.Invoke(slot));
            }
        }

        public void Close() => _panel.SetActive(false);
    }
}
