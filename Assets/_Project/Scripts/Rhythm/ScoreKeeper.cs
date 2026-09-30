using System;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 점수 = 기본점 × 콤보 배수 × 피버 배수 × 외부 배수(캐릭터 특수능력 ④).
    public class ScoreKeeper : MonoBehaviour
    {
        public event Action<ScoreKeeper> Changed;

        ComboCounter _combo;
        FeverGauge _fever;
        ScoringRules _rules;

        public long Score { get; private set; }

        /// ④ 특수능력 훅 자리. 캐릭터가 점수 배수를 올리면 여기에 넣는다.
        public float ExternalMultiplier { get; set; } = 1f;

        public void Init(ComboCounter combo, FeverGauge fever, ScoringRules rules)
        {
            if (_combo != null) _combo.Advanced -= OnAdvanced;
            _combo = combo;
            _combo.Advanced += OnAdvanced;
            _fever = fever;
            _rules = rules;
            Score = 0;
            Changed?.Invoke(this);
        }

        void OnDestroy()
        {
            if (_combo != null) _combo.Advanced -= OnAdvanced;
        }

        void OnAdvanced(ComboSource source, Judgement judgement, int combo)
        {
            // Math.Round: 배수 곱으로 생긴 소수를 매 타격마다 정리해서 이론 최대 점수 계산과 같은 방식으로 맞춘다.
            double points = _rules.BasePoints(source, judgement) * _rules.ComboMultiplier(combo) * _fever.ScoreMultiplier * ExternalMultiplier;
            Score += (long)Math.Round(points);
            Changed?.Invoke(this);
        }
    }
}
