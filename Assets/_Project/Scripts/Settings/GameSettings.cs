using System;
using UnityEngine;

namespace RhythmCP.Settings
{
    /// 기기 설정(오프셋 등). 세이브 데이터(M5)와 분리 — 슬롯과 무관하게 이 기기에서 공통으로 쓴다.
    /// 씬마다 오브젝트를 둘 필요가 없는 값 저장소라 싱글톤 MonoBehaviour 대신 static + PlayerPrefs.
    public static class GameSettings
    {
        public const float OffsetLimitMs = 300f;

        const string JudgeKey = "settings.judgeOffsetMs";
        const string VisualKey = "settings.visualOffsetMs";

        public static event Action Changed;

        static bool _loaded;
        static float _judgeOffsetMs;
        static float _visualOffsetMs;

        /// 판정 오프셋(ms). + = 입력이 늦게 들어오는 환경 → 그만큼 입력 시각을 앞당겨 판정.
        public static float JudgeOffsetMs
        {
            get { Load(); return _judgeOffsetMs; }
            set { Load(); _judgeOffsetMs = Clamp(value); PlayerPrefs.SetFloat(JudgeKey, _judgeOffsetMs); Save(); }
        }

        /// 화면 오프셋(ms). + = 노트를 그만큼 앞서 그림(화면 표시가 늦게 보이는 환경 보정). 판정에는 영향 없음.
        public static float VisualOffsetMs
        {
            get { Load(); return _visualOffsetMs; }
            set { Load(); _visualOffsetMs = Clamp(value); PlayerPrefs.SetFloat(VisualKey, _visualOffsetMs); Save(); }
        }

        public static double JudgeOffsetSec => JudgeOffsetMs / 1000.0;
        public static double VisualOffsetSec => VisualOffsetMs / 1000.0;

        static float Clamp(float ms) => Mathf.Clamp(Mathf.Round(ms), -OffsetLimitMs, OffsetLimitMs);

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _judgeOffsetMs = PlayerPrefs.GetFloat(JudgeKey, 0f);
            _visualOffsetMs = PlayerPrefs.GetFloat(VisualKey, 0f);
        }

        static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
