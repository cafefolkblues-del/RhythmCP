using System;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 풀피 시작, Miss당 고정 데미지, 0이면 Depleted.
    /// 하트 회복·홀드 이탈 감소는 ②, 캐릭터별 최대 체력은 ④ 특수능력 훅에서 Init 인자로 들어온다.
    public class RhythmHealth : MonoBehaviour
    {
        public event Action<RhythmHealth> Changed;
        public event Action<RhythmHealth> Depleted;

        JudgementSystem _judgement;
        int _missDamage;

        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool IsDepleted => Current <= 0;

        public void Init(JudgementSystem judgement, int maxHp, int missDamage)
        {
            // 리트라이로 Init이 다시 불려도 이중 구독되지 않게.
            if (_judgement != null) _judgement.Judged -= OnJudged;
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            Max = maxHp;
            Current = maxHp;
            _missDamage = missDamage;
            Changed?.Invoke(this);
        }

        void OnDestroy()
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
        }

        void OnJudged(JudgeResult result)
        {
            if (result.Judgement != Judgement.Miss || IsDepleted) return;

            Current = Mathf.Max(0, Current - _missDamage);
            Changed?.Invoke(this);
            if (IsDepleted) Depleted?.Invoke(this);
        }
    }
}
