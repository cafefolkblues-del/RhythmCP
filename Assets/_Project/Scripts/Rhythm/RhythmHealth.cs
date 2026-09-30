using System;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 풀피 시작, Miss당 고정 데미지, 하트 회복, 홀드 이탈은 틱 단위 감소, 0이면 Depleted.
    /// 캐릭터별 최대 체력은 ④ 특수능력 훅에서 Init 인자로 들어온다.
    /// 홀드 감소가 (틱 길이 × 초당 감소)라 소수가 나오므로 내부는 float, 표시는 올림.
    public class RhythmHealth : MonoBehaviour
    {
        public event Action<RhythmHealth> Changed;
        public event Action<RhythmHealth> Depleted;

        JudgementSystem _judgement;
        int _missDamage;
        int _heartHeal;
        float _holdDrainPerSec;

        public float Current { get; private set; }
        public int Max { get; private set; }
        public bool IsDepleted => Current <= 0f;

        public void Init(JudgementSystem judgement, int maxHp, int missDamage, int heartHeal, float holdDrainPerSec)
        {
            // 리트라이로 Init이 다시 불려도 이중 구독되지 않게.
            Unsubscribe();
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            _judgement.HoldTicked += OnHoldTicked;
            Max = maxHp;
            Current = maxHp;
            _missDamage = missDamage;
            _heartHeal = heartHeal;
            _holdDrainPerSec = holdDrainPerSec;
            Changed?.Invoke(this);
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_judgement == null) return;
            _judgement.Judged -= OnJudged;
            _judgement.HoldTicked -= OnHoldTicked;
        }

        void OnJudged(JudgeResult result)
        {
            if (result.Note.Type == NoteType.Heart)
            {
                // 하트는 놓쳐도 벌칙 없음.
                if (result.IsHit) Apply(_heartHeal);
                return;
            }

            if (!result.IsHit && !result.Note.IsBonus) Apply(-_missDamage);
        }

        void OnHoldTicked(PlayNote note, bool held, double seconds)
        {
            if (!held) Apply(-(float)(_holdDrainPerSec * seconds));
        }

        void Apply(float amount)
        {
            if (IsDepleted) return;

            Current = Mathf.Clamp(Current + amount, 0f, Max);
            Changed?.Invoke(this);
            if (IsDepleted) Depleted?.Invoke(this);
        }
    }
}
