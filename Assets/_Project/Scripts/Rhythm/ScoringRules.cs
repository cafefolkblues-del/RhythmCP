using System;
using System.Collections.Generic;
using RhythmCP.Chart;

namespace RhythmCP.Rhythm
{
    public enum Grade
    {
        S,
        A,
        B,
    }

    /// 콤보가 오른 이유. 점수 기본점이 여기서 갈린다.
    public enum ComboSource
    {
        /// 노트 판정(Tap·Hold 머리/꼬리). 기본점 = 판정 등급별.
        Note,
        HoldTick,
        MashHit,
    }

    /// 점수·등급·정확도 계산식. 수치는 RhythmConfig에서 오고, 계산은 순수 함수라 EditMode 테스트로 고정한다.
    public readonly struct ScoringRules
    {
        readonly int _perfect;
        readonly int _great;
        readonly int _good;
        readonly int _holdTick;
        readonly int _mashHit;
        readonly int _comboStep;
        readonly float _comboStepBonus;
        readonly float _comboMax;
        readonly float _accuracyGoodWeight;
        readonly float _gradeS;
        readonly float _gradeA;
        readonly float _minHitRate;

        public ScoringRules(int perfect, int great, int good, int holdTick, int mashHit,
            int comboStep, float comboStepBonus, float comboMax, float accuracyGoodWeight,
            float gradeS, float gradeA, float minHitRate)
        {
            _perfect = perfect;
            _great = great;
            _good = good;
            _holdTick = holdTick;
            _mashHit = mashHit;
            _comboStep = Math.Max(1, comboStep);
            _comboStepBonus = comboStepBonus;
            _comboMax = comboMax;
            _accuracyGoodWeight = accuracyGoodWeight;
            _gradeS = gradeS;
            _gradeA = gradeA;
            _minHitRate = minHitRate;
        }

        public int BasePoints(ComboSource source, Judgement judgement) => source switch
        {
            ComboSource.HoldTick => _holdTick,
            ComboSource.MashHit => _mashHit,
            _ => judgement switch
            {
                Judgement.Perfect => _perfect,
                Judgement.Great => _great,
                Judgement.Good => _good,
                _ => 0,
            },
        };

        /// combo = 이번 타격을 포함한 콤보. 10콤보부터 ×1.1, 50콤보에 상한 ×1.5(기본값 기준).
        public float ComboMultiplier(int combo) => Math.Min(1f + combo / _comboStep * _comboStepBonus, _comboMax);

        public float Accuracy(int perfect, int great, int good, int total) =>
            total <= 0 ? 0f : (perfect + great * 0.5f + good * _accuracyGoodWeight) / total;

        /// 점수율로 S/A/B, 단 히트율이 최소치 미달이면 B(2026-09-30 확정: 바닥 = B).
        public Grade Evaluate(float scoreRatio, float hitRate)
        {
            if (hitRate < _minHitRate) return Grade.B;
            if (scoreRatio >= _gradeS) return Grade.S;
            if (scoreRatio >= _gradeA) return Grade.A;
            return Grade.B;
        }

        /// 이론 최대 점수 = 올퍼펙트·풀콤보, 피버·캐릭터 배수 1, 연타 제외.
        /// 피버·연타 점수는 이 분모를 넘기는 보너스가 된다(등급 점수율 100% 초과 가능).
        public long MaxScore(PlayChart chart, double holdTickBeats)
        {
            var steps = new List<(double time, ComboSource source)>();
            foreach (var note in chart.Notes)
            {
                switch (note.Type)
                {
                    case NoteType.Tap:
                        steps.Add((note.Time, ComboSource.Note));
                        break;
                    case NoteType.Hold:
                        steps.Add((note.Time, ComboSource.Note));
                        foreach (var t in HoldJudge.TickTimes(note, chart.Tempo, holdTickBeats))
                            steps.Add((t, ComboSource.HoldTick));
                        steps.Add((note.EndTime, ComboSource.Note));
                        break;
                }
            }

            // 콤보 배수가 순서에 따라 달라지므로 실제 플레이 순서(시각)대로 쌓는다.
            steps.Sort((a, b) => a.time.CompareTo(b.time));

            double total = 0;
            for (int i = 0; i < steps.Count; i++)
                total += Math.Round(BasePoints(steps[i].source, Judgement.Perfect) * ComboMultiplier(i + 1));
            return (long)total;
        }
    }
}
