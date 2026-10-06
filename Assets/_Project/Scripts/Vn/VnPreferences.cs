using System;
using UnityEngine;

namespace RhythmCP.Vn
{
    /// VN 플레이어 설정(PlayerPrefs). "VN만 보기"는 허브(M4)가 읽어 리듬 파트를 건너뛴다 — M3는 켜고 끄기까지.
    public static class VnPreferences
    {
        const string VnOnlyKey = "vn.vnOnly";

        public static event Action Changed;

        public static bool VnOnly
        {
            get => PlayerPrefs.GetInt(VnOnlyKey, 0) == 1;
            set
            {
                if (value == VnOnly) return;
                PlayerPrefs.SetInt(VnOnlyKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
