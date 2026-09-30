namespace RhythmCP.Character
{
    /// 캐릭터 반응 상태. 스펙 §8의 11종 + 홀드 유지(HoldLoop, 2026-09-30 추가) + 대기.
    public enum ReactionState
    {
        Idle,

        // 행동 계열 — 입력·판정에 바로 붙는 모션. 감정보다 항상 우선.
        HitTop1,
        HitTop2,
        HitBottom1,
        HitBottom2,
        Gemini,
        HoldStart,
        HoldLoop,
        HoldEnd,
        Miss,

        // 감정 계열 — 행동 모션이 끝난 뒤에 나온다.
        Cheer,
        Surprise,
        Sad,
    }

    /// 감정 반응을 부르는 플레이 이벤트. 새 이벤트 = 여기 값 추가 + ReactionDirector에 감지 한 줄, 매핑은 ReactionTable 데이터.
    public enum EmotionTrigger
    {
        /// 노트 놓침 → 기본 매핑 슬픔.
        Miss,

        /// 피버 발동(연속 퍼펙트 누적) → 기본 매핑 놀람.
        FeverStart,

        /// 연타 시작 → 기본 매핑 응원.
        MashStart,
    }
}
