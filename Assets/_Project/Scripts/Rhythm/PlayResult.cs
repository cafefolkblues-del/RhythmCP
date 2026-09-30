using RhythmCP.Chart;

namespace RhythmCP.Rhythm
{
    /// 한 판의 결과. 결과창(③)이 표시하고, 나중에 M5 저장·진행(스토리/관계/컬러화)이 이걸 받는다.
    public class PlayResult
    {
        public string SongId;
        public string SongTitle;
        public Difficulty Difficulty;

        /// 체력 0으로 끝남 — 등급 대신 FAILED, 해금·컬러화 없음.
        public bool Failed;

        public long Score;
        public long MaxScore;
        public float ScoreRatio;
        public Grade Grade;

        public float HitRate;
        public float Accuracy;
        public int MaxCombo;
        public int Perfect;
        public int Great;
        public int Good;
        public int Miss;

        public bool HasClimax;

        /// 클라이맥스 무미스 달성 = 카세트 컬러화 조건(M6). 실패한 판은 false.
        public bool ClimaxClear;

        /// S급 = 추가 캐릭터 해금. 이미 해금됐는지는 M5 저장 기록이 생기면 거기서 거른다.
        public bool UnlocksCharacter => !Failed && Grade == Grade.S;
    }
}
