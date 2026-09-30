using System;
using System.Collections.Generic;
using RhythmCP.Chart;
using RhythmCP.Rhythm;
using UnityEngine;

namespace RhythmCP.Character
{
    /// ⚠️ 임시 능력(2026-09-30): 피버 발동마다 피버 구간 안에 보너스 노트를 추가한다. char2 실제 능력이 정해지면 교체.
    /// 보너스 노트 = 점수·콤보에는 들어가고, 놓쳐도 벌칙 없음, 정확도·히트율·등급 분모에서 제외.
    [CreateAssetMenu(menuName = "RhythmCP/Ability/Fever Bonus Notes (temp)", fileName = "Ability_FeverBonusNotes")]
    public class FeverBonusNotesAbility : AbilityDefinition
    {
        [SerializeField] int _notesPerFever = 4;
        [SerializeField] double _beatSpacing = 1.0;

        [Tooltip("노트가 화면 오른쪽 끝에서 들어올 시간 외의 여유. 화면 한가운데에서 갑자기 나타나지 않게.")]
        [SerializeField] double _extraLeadSec = 0.1;

        public override IDisposable Activate(AbilityContext context) => new Runtime(this, context);

        class Runtime : IDisposable
        {
            readonly FeverBonusNotesAbility _def;
            readonly AbilityContext _ctx;
            readonly List<PlayNote> _added = new List<PlayNote>();

            public Runtime(FeverBonusNotesAbility def, AbilityContext ctx)
            {
                _def = def;
                _ctx = ctx;
                _ctx.Fever.Started += OnFeverStarted;
            }

            public void Dispose() => _ctx.Fever.Started -= OnFeverStarted;

            void OnFeverStarted(FeverGauge fever)
            {
                // 지금 출발시켜도 판정선까지 이동 시간이 걸리므로 그 이후 ~ 피버 종료 사이에만 배치한다.
                double from = _ctx.Clock.SongTime + _ctx.Spawner.TravelTime(1f) + _def._extraLeadSec;
                var existing = new List<PlayNote>(_ctx.Chart.Notes);
                existing.AddRange(_added);

                var notes = BonusNotePlanner.Plan(existing, _ctx.Chart.Tempo, from, fever.EndTime, _def._notesPerFever, _def._beatSpacing);
                _added.AddRange(notes);
                _ctx.AddBonusNotes(notes);
            }
        }
    }
}
