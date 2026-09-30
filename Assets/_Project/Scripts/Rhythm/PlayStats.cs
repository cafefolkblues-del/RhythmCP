using System;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 판정 집계: 등급별 개수, 히트율, 클라이맥스 무미스.
    /// 집계 대상 = 노트 머리 + 홀드 꼬리. 하트·홀드 틱·연타는 제외(콤보 규칙과 같은 기준).
    public class PlayStats : MonoBehaviour
    {
        public event Action<PlayStats> Changed;

        JudgementSystem _judgement;
        PlayChart _chart;
        readonly int[] _counts = new int[4];

        public int Perfect => _counts[(int)Judgement.Perfect];
        public int Great => _counts[(int)Judgement.Great];
        public int Good => _counts[(int)Judgement.Good];
        public int Miss => _counts[(int)Judgement.Miss];
        public int Judged => Perfect + Great + Good + Miss;

        /// 채보 전체 판정 수. 체력 0으로 중간에 끝나도 결과는 이 분모로 낸다(안 친 노트 = 못 친 노트).
        public int TotalJudgements { get; private set; }

        /// 플레이 중 HUD용 — 지금까지 판정한 것 기준, 시작은 100%.
        public float LiveHitRate => Judged == 0 ? 1f : (float)(Judged - Miss) / Judged;

        public float FinalHitRate => TotalJudgements == 0 ? 0f : (float)(Judged - Miss) / TotalJudgements;

        public bool HasClimax => _chart != null && _chart.HasClimax;
        public bool ClimaxMissed { get; private set; }

        public void Init(JudgementSystem judgement, PlayChart chart)
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            _chart = chart;

            Array.Clear(_counts, 0, _counts.Length);
            ClimaxMissed = false;
            TotalJudgements = 0;
            foreach (var note in chart.Notes)
            {
                if (note.Type == NoteType.Tap) TotalJudgements += 1;
                else if (note.Type == NoteType.Hold) TotalJudgements += 2;
            }
            Changed?.Invoke(this);
        }

        void OnDestroy()
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
        }

        void OnJudged(JudgeResult result)
        {
            if (result.Note.IsPenaltyFree) return;

            _counts[(int)result.Judgement]++;

            // 기준 시각 = 그 판정이 속한 노트 시각(머리 = 시작, 꼬리 = 끝). 무미스엔 Good도 통과(캐주얼).
            if (result.Judgement == Judgement.Miss && HasClimax)
            {
                double t = result.Part == NotePart.Tail ? result.Note.EndTime : result.Note.Time;
                if (t >= _chart.ClimaxStartSec && t <= _chart.ClimaxEndSec) ClimaxMissed = true;
            }
            Changed?.Invoke(this);
        }
    }
}
