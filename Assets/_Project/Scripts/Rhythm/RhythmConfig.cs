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

        public JudgeWindows Windows => new JudgeWindows(_perfectWindow, _greatWindow, _goodWindow);
        public float ScrollSpeed => _scrollSpeed;
        public double LeadInSec => _leadInSec;
        public double TailSec => _tailSec;
        public int MaxHp => _maxHp;
        public int MissDamage => _missDamage;
        public int HeartHeal => _heartHeal;
        public float HoldDrainPerSec => _holdDrainPerSec;
        public double HoldTickBeats => _holdTickBeats;
    }
}
