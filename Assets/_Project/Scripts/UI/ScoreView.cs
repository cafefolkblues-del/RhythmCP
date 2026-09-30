using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;

namespace RhythmCP.UI
{
    /// HUD 우상단 "히트율 · 점수".
    public class ScoreView : MonoBehaviour
    {
        [SerializeField] ScoreKeeper _score;
        [SerializeField] PlayStats _stats;
        [SerializeField] TMP_Text _hitRate;
        [SerializeField] TMP_Text _points;

        void OnEnable()
        {
            _score.Changed += OnScore;
            _stats.Changed += OnStats;
        }

        void OnDisable()
        {
            _score.Changed -= OnScore;
            _stats.Changed -= OnStats;
        }

        void OnScore(ScoreKeeper score) => _points.text = score.Score.ToString("N0");

        void OnStats(PlayStats stats) => _hitRate.text = $"{stats.LiveHitRate * 100f:0.0}%";
    }
}
