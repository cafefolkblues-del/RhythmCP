using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// M1 튜닝표 수치. 기본값 = 스펙 v2 §10.
    [CreateAssetMenu(menuName = "RhythmCP/Rhythm Config", fileName = "RhythmConfig")]
    public class RhythmConfig : ScriptableObject
    {
        [Header("판정 윈도우 (± 초)")]
        [SerializeField] double _perfectWindow = 0.050;
        [SerializeField] double _greatWindow = 0.130;
        [SerializeField] double _goodWindow = 0.200;

        [Header("스크롤")]
        [Tooltip("월드 유닛/초. 노트 speed 배율이 곱해진다.")]
        [SerializeField] float _scrollSpeed = 8f;

        [Tooltip("곡 시작 전 대기. 첫 노트가 화면 밖에서 들어올 시간.")]
        [SerializeField] double _leadInSec = 2.0;

        [Tooltip("마지막 노트 이후 곡 종료로 치기까지의 여유.")]
        [SerializeField] double _tailSec = 1.5;

        [Header("체력")]
        [SerializeField] int _maxHp = 100;
        [SerializeField] int _missDamage = 8;
        [SerializeField] int _heartHeal = 20;

        [Tooltip("홀드를 뗀 동안 초당 감소. 틱 단위로 (틱 길이 × 이 값)만큼 깎는다.")]
        [SerializeField] float _holdDrainPerSec = 6f;

        [Header("홀드")]
        [Tooltip("누르고 있는 동안 콤보가 오르는 간격(박). 0.25 = 16분음표.")]
        [SerializeField] double _holdTickBeats = 0.25;

        [Header("점수")]
        [SerializeField] int _perfectPoints = 300;
        [SerializeField] int _greatPoints = 150;
        [SerializeField] int _goodPoints = 50;
        [SerializeField] int _holdTickPoints = 50;
        [SerializeField] int _mashHitPoints = 50;

        [Tooltip("이 콤보마다 배수 +_comboStepBonus.")]
        [SerializeField] int _comboStep = 10;
        [SerializeField] float _comboStepBonus = 0.1f;
        [SerializeField] float _comboMaxMultiplier = 1.5f;

        [Tooltip("정확도식에서 Good 가중치 α. (P + G×0.5 + Good×α) / 총 판정.")]
        [SerializeField] float _accuracyGoodWeight = 0.25f;

        [Header("등급 (점수율 = 점수 / 이론 최대 점수)")]
        [SerializeField] float _gradeSRatio = 0.95f;
        [SerializeField] float _gradeARatio = 0.80f;

        [Tooltip("히트율이 이보다 낮으면 점수와 무관하게 B.")]
        [SerializeField] float _minHitRate = 0.70f;

        [Header("피버")]
        [SerializeField] float _feverMax = 100f;
        [SerializeField] float _feverGainPerfect = 2f;
        [SerializeField] float _feverGainGreat = 1f;
        [SerializeField] double _feverDurationSec = 5.0;
        [SerializeField] float _feverScoreMultiplier = 1.5f;

        public JudgeWindows Windows => new JudgeWindows(_perfectWindow, _greatWindow, _goodWindow);
        public float ScrollSpeed => _scrollSpeed;
        public double LeadInSec => _leadInSec;
        public double TailSec => _tailSec;
        public int MaxHp => _maxHp;
        public int MissDamage => _missDamage;
        public int HeartHeal => _heartHeal;
        public float HoldDrainPerSec => _holdDrainPerSec;
        public double HoldTickBeats => _holdTickBeats;

        public ScoringRules Scoring => new ScoringRules(
            _perfectPoints, _greatPoints, _goodPoints, _holdTickPoints, _mashHitPoints,
            _comboStep, _comboStepBonus, _comboMaxMultiplier, _accuracyGoodWeight,
            _gradeSRatio, _gradeARatio, _minHitRate);

        public float FeverMax => _feverMax;
        public float FeverGainPerfect => _feverGainPerfect;
        public float FeverGainGreat => _feverGainGreat;
        public double FeverDurationSec => _feverDurationSec;
        public float FeverScoreMultiplier => _feverScoreMultiplier;
    }
}
