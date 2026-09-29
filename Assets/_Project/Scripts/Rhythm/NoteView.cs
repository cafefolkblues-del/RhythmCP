using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 노트 한 개의 표시. 위치는 매 프레임 SongTime에서 다시 계산한다(누적 이동 X — 오차가 쌓이지 않게).
    public class NoteView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] Color _missColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        PlayNote _note;
        SongClock _clock;
        float _judgeX;
        float _unitsPerSec;
        float _despawnX;

        public void Init(PlayNote note, SongClock clock, float judgeX, float laneY, float unitsPerSec, float despawnX)
        {
            _note = note;
            _clock = clock;
            _judgeX = judgeX;
            _unitsPerSec = unitsPerSec;
            _despawnX = despawnX;
            transform.position = new Vector3(XAt(clock.SongTime), laneY, 0f);
        }

        /// Hit은 바로 사라지고, Miss는 흐리게 바꿔 판정선을 지나쳐 흘러가게 둔다(뮤즈대시식).
        /// 히트 연출(juice)은 ⑥에서 이 자리에 붙는다.
        public void OnJudged(Judgement judgement)
        {
            if (judgement == Judgement.Miss)
                _renderer.color = _missColor;
            else
                Destroy(gameObject);
        }

        void Update()
        {
            float x = XAt(_clock.SongTime);
            if (x < _despawnX)
            {
                Destroy(gameObject);
                return;
            }
            var p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
        }

        float XAt(double songTime) => _judgeX + (float)((_note.Time - songTime) * _unitsPerSec);
    }
}
