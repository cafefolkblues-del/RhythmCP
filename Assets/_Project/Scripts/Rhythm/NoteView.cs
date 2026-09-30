using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 단발 노트(Tap·Heart) 표시.
    public class NoteView : NoteViewBase
    {
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] Color _missColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        [Tooltip("동시치기(같은 박자 위아래 Tap)일 때 색. 판별은 ChartLoader.")]
        [SerializeField] Color _geminiColor = new Color(0.9f, 0.6f, 0.25f);

        [Tooltip("특수능력 보너스 노트 색.")]
        [SerializeField] Color _bonusColor = new Color(1f, 0.85f, 0.3f);

        public override void Init(PlayNote note, SongClock clock, float judgeX, float laneY, float unitsPerSec, float despawnX)
        {
            base.Init(note, clock, judgeX, laneY, unitsPerSec, despawnX);
            if (note.IsBonus) _renderer.color = _bonusColor;
            else if (note.IsGemini) _renderer.color = _geminiColor;
        }

        /// Hit은 바로 사라지고, Miss는 흐리게 바꿔 판정선을 지나쳐 흘러가게 둔다(뮤즈대시식).
        /// 히트 연출(juice)은 ⑥에서 이 자리에 붙는다.
        public override void OnJudged(JudgeResult result)
        {
            if (result.IsHit) Destroy(gameObject);
            else _renderer.color = _missColor;
        }

        void Update()
        {
            float x = XAt(Note.Time);
            if (x < DespawnX) Destroy(gameObject);
            else SetX(x);
        }
    }
}
