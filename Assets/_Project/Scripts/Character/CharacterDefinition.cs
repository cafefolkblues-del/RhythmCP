using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmCP.Character
{
    /// 플레이어블 캐릭터 한 명. 최대 체력·반응 스프라이트·특수능력.
    [CreateAssetMenu(menuName = "RhythmCP/Character Definition", fileName = "Char_")]
    public class CharacterDefinition : ScriptableObject
    {
        [Serializable]
        public class ReactionSprite
        {
            public ReactionState state;
            public Sprite sprite;
        }

        [SerializeField] string _characterId;
        [SerializeField] string _displayName;

        [Tooltip("표준 100, 캐릭터별 상이(스펙 Q4).")]
        [SerializeField] int _maxHp = 100;

        [Tooltip("없는 상태는 CharacterReactionView가 임시 색 + 상태 이름으로 표시.")]
        [SerializeField] List<ReactionSprite> _reactions = new List<ReactionSprite>();

        [Tooltip("비워두면 특수능력 없음(char1·char3 TBD).")]
        [SerializeField] AbilityDefinition _ability;

        public string CharacterId => _characterId;
        public string DisplayName => _displayName;
        public int MaxHp => _maxHp;
        public AbilityDefinition Ability => _ability;

        public Sprite GetSprite(ReactionState state)
        {
            foreach (var r in _reactions)
                if (r.state == state) return r.sprite;
            return null;
        }
    }
}
