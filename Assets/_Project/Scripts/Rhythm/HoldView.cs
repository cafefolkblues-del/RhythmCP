using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.Rhythm
{
    /// 홀드 = 머리(원) + 몸통(늘어나는 사각형) + 꼬리(원). 셋 다 이 오브젝트의 자식.
    /// 누르고 있는 동안 머리는 판정선에 고정되고 몸통이 줄어든다.
    public class HoldView : NoteViewBase
    {
        [SerializeField] SpriteRenderer _head;
        [SerializeField] SpriteRenderer _body;
        [SerializeField] SpriteRenderer _tail;

        [Tooltip("몸통 두께(월드 유닛). 몸통 스프라이트는 1유닛 정사각형 기준.")]
        [SerializeField] float _bodyThickness = 0.45f;

        [SerializeField] Color _missColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        [Tooltip("중간에 뗐을 때 몸통 알파.")]
        [SerializeField] float _brokenAlpha = 0.35f;

        bool _holding;
        Color _bodyColor;

        void Awake() => _bodyColor = _body.color;

        public override void Init(PlayNote note, SongClock clock, float judgeX, float laneY, float unitsPerSec, float despawnX)
        {
            base.Init(note, clock, judgeX, laneY, unitsPerSec, despawnX);
            Layout();
        }

        public override void OnJudged(JudgeResult result)
        {
            if (result.Part == NotePart.Head)
            {
                if (result.IsHit) _holding = true;
                else Tint(_missColor);
                return;
            }

            // 꼬리
            _holding = false;
            if (result.IsHit) Destroy(gameObject);
            else Tint(_missColor);
        }

        public override void OnHoldBreakChanged(bool broken)
        {
            var c = _bodyColor;
            if (broken) c.a *= _brokenAlpha;
            _body.color = c;
        }

        void Update() => Layout();

        void Layout()
        {
            float headX = XAt(Note.Time);
            float tailX = XAt(Note.EndTime);
            if (_holding)
            {
                headX = Mathf.Max(headX, JudgeX);
                tailX = Mathf.Max(tailX, JudgeX);
            }

            if (tailX < DespawnX)
            {
                Destroy(gameObject);
                return;
            }

            // 루트는 레인 높이만 들고 x=0에 두고, 자식을 월드 x로 배치한다.
            SetX(0f);
            _head.transform.localPosition = new Vector3(headX, 0f, 0f);
            _tail.transform.localPosition = new Vector3(tailX, 0f, 0f);
            _body.transform.localPosition = new Vector3((headX + tailX) * 0.5f, 0f, 0f);
            _body.transform.localScale = new Vector3(Mathf.Max(0f, tailX - headX), _bodyThickness, 1f);
        }

        void Tint(Color color)
        {
            _head.color = color;
            _tail.color = color;
            _body.color = color;
            _bodyColor = color;
        }
    }
}
