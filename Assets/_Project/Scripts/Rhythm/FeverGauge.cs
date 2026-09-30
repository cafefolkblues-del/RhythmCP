using System;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// Perfect/Great로 충전 → 가득 차면 자동 발동 → 지속 시간 동안 점수 배수 → 비워지고 다시 충전.
    /// 시간은 SongClock 기준 — ⑤ 일시정지로 곡이 멈추면 피버도 같이 멈춘다.
    public class FeverGauge : MonoBehaviour
    {
        /// 발동 순간. 놀람 리액션(④)·캐릭터별 스윕 연출(⑥)·화면 테두리가 받는다.
        public event Action<FeverGauge> Started;
        public event Action<FeverGauge> Ended;
        public event Action<FeverGauge> Changed;

        JudgementSystem _judgement;
        SongClock _clock;
        float _max;
        float _gainPerfect;
        float _gainGreat;
        double _duration;
        float _multiplier;

        float _gauge;
        double _endTime;

        public bool IsActive { get; private set; }
        public int ActivationCount { get; private set; }

        /// 0~1. 충전 중엔 차오르고, 발동 중엔 남은 시간만큼 줄어든다.
        public float Fill => IsActive
            ? (float)Math.Max(0, (_endTime - _clock.SongTime) / _duration)
            : _gauge / _max;

        public float ScoreMultiplier => IsActive ? _multiplier : 1f;

        public void Init(JudgementSystem judgement, SongClock clock, RhythmConfig config)
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
            _judgement = judgement;
            _judgement.Judged += OnJudged;
            _clock = clock;
            _max = Mathf.Max(1f, config.FeverMax);
            _gainPerfect = config.FeverGainPerfect;
            _gainGreat = config.FeverGainGreat;
            _duration = Math.Max(0.1, config.FeverDurationSec);
            _multiplier = config.FeverScoreMultiplier;

            _gauge = 0f;
            IsActive = false;
            ActivationCount = 0;
            Changed?.Invoke(this);
        }

        void OnDestroy()
        {
            if (_judgement != null) _judgement.Judged -= OnJudged;
        }

        void OnJudged(JudgeResult result)
        {
            if (IsActive || result.Note.Type == NoteType.Heart) return;

            float gain = result.Judgement switch
            {
                Judgement.Perfect => _gainPerfect,
                Judgement.Great => _gainGreat,
                _ => 0f,
            };
            if (gain <= 0f) return;

            _gauge = Mathf.Min(_max, _gauge + gain);
            if (_gauge >= _max)
            {
                IsActive = true;
                ActivationCount++;
                _endTime = _clock.SongTime + _duration;
                Started?.Invoke(this);
            }
            Changed?.Invoke(this);
        }

        void Update()
        {
            if (!IsActive) return;

            if (_clock.SongTime >= _endTime)
            {
                IsActive = false;
                _gauge = 0f;
                Ended?.Invoke(this);
            }
            Changed?.Invoke(this);
        }
    }
}
