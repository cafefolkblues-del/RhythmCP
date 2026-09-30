using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 노트 표시 공통부. 위치는 매 프레임 SongTime에서 다시 계산한다(누적 이동 X — 오차가 쌓이지 않게).
    /// 종류별 표시(Tap·Heart / Hold / Mash)는 파생 클래스, 이벤트 전달은 NoteSpawner가 한다.
    public abstract class NoteViewBase : MonoBehaviour
    {
        protected PlayNote Note;
        protected SongClock Clock;
        protected float JudgeX;
        protected float UnitsPerSec;
        protected float DespawnX;

        public virtual void Init(PlayNote note, SongClock clock, float judgeX, float laneY, float unitsPerSec, float despawnX)
        {
            Note = note;
            Clock = clock;
            JudgeX = judgeX;
            UnitsPerSec = unitsPerSec;
            DespawnX = despawnX;
            transform.position = new Vector3(XAt(note.Time), laneY, 0f);
        }

        public abstract void OnJudged(JudgeResult result);

        public virtual void OnHoldBreakChanged(bool broken) { }

        public virtual void OnMashEnded() { }

        // VisualTime: 화면 오프셋이 적용된 표시용 시각. 판정은 SongTime 기준이라 여기만 다르다.
        protected float XAt(double time) => JudgeX + (float)((time - Clock.VisualTime) * UnitsPerSec);

        protected void SetX(float x)
        {
            var p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
        }
    }
}
