using System;
using System.Collections.Generic;
using RhythmCP.Chart;
using RhythmCP.Rhythm;
using UnityEngine;

namespace RhythmCP.Character
{
    /// 수치형 특수능력. 기본값 = 효과 없음. RhythmSession이 각 시스템 Init 전에 설정값 위에 얹는다.
    [Serializable]
    public class RhythmModifiers
    {
        [Header("판정 보조 (± ms 추가)")]
        public float perfectWindowAddMs;
        public float greatWindowAddMs;
        public float goodWindowAddMs;

        [Header("체력")]
        public float missDamageMultiplier = 1f;
        public float heartHealMultiplier = 1f;
        public float holdDrainMultiplier = 1f;

        [Header("점수·피버")]
        public float scoreMultiplier = 1f;
        public float comboMaxMultiplierAdd;
        public float feverGainMultiplier = 1f;
        public float feverDurationAddSec;
    }

    /// 조건형 능력이 쓰는 런타임 접근점. 능력 SO는 상태를 들지 않고 Activate에서 런타임 객체를 만든다.
    public class AbilityContext
    {
        public PlayChart Chart;
        public SongClock Clock;
        public JudgementSystem Judgement;
        public FeverGauge Fever;
        public NoteSpawner Spawner;

        /// 보너스 노트 주입 — 판정과 표시 양쪽에 같이 넣어야 해서 한 곳으로 모은다.
        public void AddBonusNotes(List<PlayNote> notes)
        {
            if (notes.Count == 0) return;
            Judgement.AddNotes(notes);
            Spawner.AddNotes(notes);
        }
    }

    /// 캐릭터 특수능력. 수치형은 Modifiers만 채우고, 조건형(이벤트 반응·노트 추가)은 파생 클래스에서 Activate를 오버라이드한다.
    [CreateAssetMenu(menuName = "RhythmCP/Ability (Modifiers Only)", fileName = "Ability_")]
    public class AbilityDefinition : ScriptableObject
    {
        [TextArea] [SerializeField] string _description;
        [SerializeField] RhythmModifiers _modifiers = new RhythmModifiers();

        public string Description => _description;
        public RhythmModifiers Modifiers => _modifiers;

        /// 반환한 IDisposable은 세션이 끝날 때(OnDestroy) 해제된다. 수치형 능력은 null.
        public virtual IDisposable Activate(AbilityContext context) => null;
    }
}
