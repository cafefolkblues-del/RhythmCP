using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmCP.Character
{
    /// 반응 규칙 데이터. 행동 모션 길이와 "이벤트 → 감정" 매핑(스펙 Q3: 이벤트 사후 추가 가능하게 데이터 주도).
    [CreateAssetMenu(menuName = "RhythmCP/Reaction Table", fileName = "ReactionTable")]
    public class ReactionTable : ScriptableObject
    {
        [Serializable]
        public class EmotionRule
        {
            public EmotionTrigger trigger;
            public ReactionState state = ReactionState.Sad;
            public float durationSec = 0.8f;

            [Tooltip("행동 모션에 밀려 대기하는 최대 시간. 넘기면 버린다(오래된 감정이 뒤늦게 나오지 않게).")]
            public float queueTimeoutSec = 1.0f;
        }

        [Header("행동 모션 길이(초)")]
        [SerializeField] float _hitSec = 0.2f;
        [SerializeField] float _geminiSec = 0.3f;
        [SerializeField] float _holdStartSec = 0.2f;
        [SerializeField] float _holdEndSec = 0.3f;
        [SerializeField] float _missSec = 0.35f;

        [Header("감정")]
        [SerializeField] List<EmotionRule> _emotions = new List<EmotionRule>
        {
            new EmotionRule { trigger = EmotionTrigger.Miss, state = ReactionState.Sad },
            new EmotionRule { trigger = EmotionTrigger.FeverStart, state = ReactionState.Surprise },
            new EmotionRule { trigger = EmotionTrigger.MashStart, state = ReactionState.Cheer },
        };

        public float HitSec => _hitSec;
        public float GeminiSec => _geminiSec;
        public float HoldStartSec => _holdStartSec;
        public float HoldEndSec => _holdEndSec;
        public float MissSec => _missSec;

        public EmotionRule Find(EmotionTrigger trigger)
        {
            foreach (var rule in _emotions)
                if (rule.trigger == trigger) return rule;
            return null;
        }
    }
}
