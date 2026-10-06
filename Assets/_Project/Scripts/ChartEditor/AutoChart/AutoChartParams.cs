using System;
using UnityEngine;

namespace RhythmCP.ChartEditing
{
    /// 자동 배치 튜닝값(채보 스펙 §8). 기본값 = 스펙 기본값. 에디터에서 바꿔 가며 다시 생성한다.
    [Serializable]
    public class AutoChartParams
    {
        [Header("밀도")]
        public double targetNps = 1.5;
        public double segmentMinNps = 0.5;
        public double segmentMaxNps = 2.5;

        [Tooltip("최소 노트 간격 = max(이 박, 이 초) — 고BPM에서 더블타임 방지.")]
        public double minGapBeats = 0.5;
        public double minGapSec = 0.3;

        [Header("레인")]
        [Tooltip("직전 노트와 이 박 이하(또는 이 초 이하)면 같은 연속 구간(런).")]
        public double runGapBeats = 1.0;
        public double runGapSec = 0.4;

        [Tooltip("런 도중 반대 대역이 이만큼 연속돼야 레인을 바꾼다(1노트 깜빡임 금지).")]
        public int laneSwitchMinRun = 2;

        [Header("타입")]
        public double holdMinBeats = 1.0;
        public double heartIntervalSec = 20.0;

        [Header("스냅")]
        [Tooltip("1/1·1/2·1/4박(스펙 확정). 셋잇단(1/3박)은 토글.")]
        public bool allowTriplets;

        [Tooltip("가장 가까운 격자와 이 박보다 멀면 경고(BPM·오프셋 오류 신호).")]
        public double offGridFlagBeats = 0.125;

        [Header("클라이맥스")]
        public double climaxSec = 30.0;
    }
}
