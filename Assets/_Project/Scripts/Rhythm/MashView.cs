using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 연타(Sandbag). 판정선까지 다가와 연타 구간 동안 그 자리에 머물고, 구간이 끝나면 사라진다.
    /// 타격 반응(흔들림·타수 표시)은 ⑥ juice.
    public class MashView : NoteViewBase
    {
        public override void OnJudged(JudgeResult result) { }

        public override void OnMashEnded() => Destroy(gameObject);

        void Update() => SetX(Mathf.Max(XAt(Note.Time), JudgeX));
    }
}
