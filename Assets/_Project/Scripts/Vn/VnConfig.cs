using UnityEngine;

namespace RhythmCP.Vn
{
    /// VN 연출 수치. 아트 붙기 전 임시값 — 플레이해 보고 조정.
    [CreateAssetMenu(menuName = "RhythmCP/VN Config", fileName = "VnConfig")]
    public class VnConfig : ScriptableObject
    {
        [Header("텍스트")]
        [Tooltip("타자기 속도(글자/초). 0 = 즉시 전체 표시.")]
        [SerializeField] float _charsPerSec = 40f;

        [Header("오토·스킵")]
        [Tooltip("오토: 다 찍힌 뒤 기본 대기.")]
        [SerializeField] float _autoBaseSec = 1.2f;

        [Tooltip("오토: 글자당 추가 대기(긴 대사는 오래 보여준다).")]
        [SerializeField] float _autoPerCharSec = 0.05f;

        [Tooltip("스킵: 한 줄 넘기는 간격.")]
        [SerializeField] float _skipIntervalSec = 0.06f;

        [Header("세이브")]
        [SerializeField] int _saveSlotCount = 9;

        [Header("VN만 보기")]
        [Tooltip("켰을 때 하단 캐릭터 한마디가 떠 있는 시간.")]
        [SerializeField] float _oneLinerSec = 3f;

        [Header("캐릭터")]
        [Tooltip("말하지 않는 캐릭터 색(곱). 나레이션은 아무도 어둡게 안 함, mc는 전원 어둡게.")]
        [SerializeField] Color _dimTint = new Color(0.45f, 0.45f, 0.45f, 1f);
        [SerializeField] float _actorFadeSec = 0.25f;

        [Tooltip("퇴장 = 검은색으로 페이드아웃.")]
        [SerializeField] float _exitFadeSec = 0.4f;

        [Header("배경·CG")]
        [SerializeField] float _bgFadeSec = 0.6f;
        [SerializeField] float _cgFadeSec = 0.4f;

        [Header("화면 연출")]
        [Tooltip("fx fade: 검은 화면에서 밝아지는 시간.")]
        [SerializeField] float _screenFadeSec = 0.6f;
        [SerializeField] float _shakeSec = 0.35f;

        [Tooltip("UI 픽셀.")]
        [SerializeField] float _shakeAmplitude = 18f;

        [Tooltip("colorBleed 임시 연출: 흑백 → 컬러로 번지는 시간. M6 부분색 셰이더가 오면 교체.")]
        [SerializeField] float _colorBleedSec = 1.2f;

        [Tooltip("VN 기본 채도(-100 = 흑백). colorBleed 라인에서만 0까지 올라간다.")]
        [SerializeField, Range(-100f, 0f)] float _baseSaturation = -100f;

        [Header("BGM")]
        [SerializeField] float _bgmCrossfadeSec = 1f;
        [SerializeField, Range(0f, 1f)] float _bgmVolume = 0.7f;

        public float CharsPerSec => _charsPerSec;
        public float AutoBaseSec => _autoBaseSec;
        public float AutoPerCharSec => _autoPerCharSec;
        public float SkipIntervalSec => _skipIntervalSec;
        public int SaveSlotCount => _saveSlotCount;
        public float OneLinerSec => _oneLinerSec;
        public Color DimTint => _dimTint;
        public float ActorFadeSec => _actorFadeSec;
        public float ExitFadeSec => _exitFadeSec;
        public float BgFadeSec => _bgFadeSec;
        public float CgFadeSec => _cgFadeSec;
        public float ScreenFadeSec => _screenFadeSec;
        public float ShakeSec => _shakeSec;
        public float ShakeAmplitude => _shakeAmplitude;
        public float ColorBleedSec => _colorBleedSec;
        public float BaseSaturation => _baseSaturation;
        public float BgmCrossfadeSec => _bgmCrossfadeSec;
        public float BgmVolume => _bgmVolume;
    }
}
