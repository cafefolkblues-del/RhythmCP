using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;

namespace RhythmCP.UI
{
    /// 판정 텍스트를 잠깐 띄운다. 등장 연출(스케일·페이드)은 ⑥ juice.
    public class JudgementPopup : MonoBehaviour
    {
        [SerializeField] JudgementSystem _judgement;
        [SerializeField] TMP_Text _text;
        [SerializeField] float _showSec = 0.35f;

        [Tooltip("타이밍 확인용 — 빠름/느림 ms를 판정 아래에 표시.")]
        [SerializeField] bool _showDeltaMs = true;

        [SerializeField] Color _perfect = new Color(1f, 0.85f, 0.3f);
        [SerializeField] Color _great = new Color(0.45f, 0.85f, 1f);
        [SerializeField] Color _good = new Color(0.6f, 1f, 0.6f);
        [SerializeField] Color _miss = new Color(0.6f, 0.6f, 0.6f);

        float _hideAt;

        void OnEnable() => _judgement.Judged += Show;
        void OnDisable() => _judgement.Judged -= Show;

        void Start() => _text.enabled = false;

        void Show(JudgeResult result)
        {
            // 하트는 놓쳐도 벌칙이 없어서 MISS를 띄우면 벌받은 것처럼 보인다.
            if (result.Note.IsPenaltyFree && !result.IsHit) return;

            _text.text = result.Judgement.ToString().ToUpperInvariant();
            if (_showDeltaMs && result.Judgement != Judgement.Miss)
                _text.text += $"\n<size=50%>{result.Delta * 1000:+0;-0;0} ms</size>";

            _text.color = result.Judgement switch
            {
                Judgement.Perfect => _perfect,
                Judgement.Great => _great,
                Judgement.Good => _good,
                _ => _miss,
            };
            _text.enabled = true;
            _hideAt = Time.time + _showSec;
        }

        void Update()
        {
            if (_text.enabled && Time.time >= _hideAt) _text.enabled = false;
        }
    }
}
