using System;
using UnityEngine;

namespace RhythmCP.ChartEditing
{
    /// 자동 배치 튜닝값. 밀도·간격은 채보 스펙 §8, 패턴 점수는 2단계 패턴 방식(2026-10-07) 기본값.
    /// 목표 수치는 실측으로 잡기로 해서(유저 결정) 전부 인스펙터에서 바꿔 가며 다시 생성한다.
    [Serializable]
    public class AutoChartParams
    {
        [Header("밀도")]
        public double targetNps = 1.5;
        public double segmentMinNps = 0.5;
        public double segmentMaxNps = 2.5;

        [Tooltip("최소 노트 간격 = max(이 박, 이 초) — 이보다 촘촘한 패턴은 그 곡 BPM에서 후보에서 빠진다.")]
        public double minGapBeats = 0.5;
        public double minGapSec = 0.3;

        [Header("리듬 패턴 점수")]
        [Tooltip("패턴 자리에 실제 온셋이 있는 만큼(세기 합, 첫 박·박 자리 가중).")]
        public double hitWeight = 1.0;

        [Tooltip("패턴 자리에 소리가 없으면(빈 박에 노트) 자리당 감점.")]
        public double emptyPenalty = 0.4;

        [Tooltip("강한 온셋을 패턴이 비워 두면 감점.")]
        public double missWeight = 0.6;
        public double strongOnset = 0.4;

        [Tooltip("마디 목표 노트 수(구간 에너지 기준)와의 차이 감점.")]
        public double densityWeight = 0.5;

        [Tooltip("직전 마디와 같은 패턴이면 감점. 3마디 연속은 금지.")]
        public double repeatPenalty = 0.4;

        [Tooltip("프레이즈 끝(4마디째)에 rest·fill·pickup 가점, 그 밖에서 fill 감점.")]
        public double phraseBonus = 0.3;

        [Tooltip("N마디마다 쉬는 마디(rest 역할) 가점 — osu!taiko Kantan '32~36박마다 3박 이상 쉼' 규칙.")]
        public int restEveryBars = 8;
        public double restBonus = 1.0;

        [Tooltip("패턴 자리와 온셋이 이 박 안이면 맞은 것으로.")]
        public double hitToleranceBeats = 0.125;

        [Header("레인")]
        [Tooltip("이 박 이하로 붙은 노트는 같은 레인(Q5 '연속 음 = 같은 레인'을 빠른 연타에만 적용, 2026-10-07).")]
        public double fastPairBeats = 0.5;
        public int maxSameLane = 4;

        [Tooltip("저음 = 하단, 고음 = 상단 일치 가점.")]
        public double bandWeight = 1.0;
        public double laneRepeatPenalty = 0.3;

        [Header("반복 구간")]
        [Tooltip("같은 라벨 구간(후렴 등)은 처음 고른 패턴을 다시 쓰도록 가점.")]
        public bool reuseSections = true;
        public double reuseBonus = 0.5;

        [Header("타입")]
        public double holdMinBeats = 1.0;
        public double heartIntervalSec = 20.0;

        [Header("스냅")]
        [Tooltip("셋잇단 패턴(triplet: true) 사용.")]
        public bool allowTriplets;

        [Header("클라이맥스")]
        public double climaxSec = 30.0;
    }
}
