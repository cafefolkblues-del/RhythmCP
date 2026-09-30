using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.UI
{
    /// 결과창 오버레이. 같은 씬 위에 뜬다(씬 간 데이터 전달은 허브 생길 때 GameSession 싱글톤으로).
    /// 이 컴포넌트는 항상 켜진 오브젝트에 두고 _panel만 켜고 끈다 — 꺼진 오브젝트는 OnEnable 구독이 안 돼서.
    public class ResultView : MonoBehaviour
    {
        [SerializeField] RhythmSession _session;
        [SerializeField] GameObject _panel;

        [Header("헤더")]
        [SerializeField] TMP_Text _grade;
        [SerializeField] TMP_Text _subtitle;

        [Header("스탯")]
        [SerializeField] TMP_Text _score;
        [SerializeField] TMP_Text _hitRate;
        [SerializeField] TMP_Text _accuracy;
        [SerializeField] TMP_Text _maxCombo;
        [SerializeField] TMP_Text _judgeCounts;
        [SerializeField] TMP_Text _miss;
        [SerializeField] TMP_Text _climax;
        [SerializeField] TMP_Text _difficulty;

        [Header("버튼")]
        [SerializeField] Button _retry;

        [Tooltip("허브(M4) 전까지 비활성.")]
        [SerializeField] Button _next;

        [Header("S급 캐릭터 해금 알림 (클릭하면 닫힘)")]
        [SerializeField] GameObject _unlockBanner;
        [SerializeField] Button _unlockDismiss;

        [SerializeField] Color _gradeColor = new Color(0.9f, 0.3f, 0.5f);
        [SerializeField] Color _failedColor = new Color(0.6f, 0.6f, 0.6f);

        void Awake()
        {
            _panel.SetActive(false);
            _unlockBanner.SetActive(false);
            _next.interactable = false;
        }

        void OnEnable()
        {
            _session.Finished += Show;
            _retry.onClick.AddListener(_session.Retry);
            _unlockDismiss.onClick.AddListener(HideBanner);
        }

        void OnDisable()
        {
            _session.Finished -= Show;
            _retry.onClick.RemoveListener(_session.Retry);
            _unlockDismiss.onClick.RemoveListener(HideBanner);
        }

        void Show(PlayResult r)
        {
            _grade.text = r.Failed ? "FAILED" : r.Grade.ToString();
            _grade.color = r.Failed ? _failedColor : _gradeColor;
            _subtitle.text = r.Failed ? $"{r.SongTitle} · SCORE {r.Score:N0}" : $"GRADE {r.Grade} · SCORE {r.Score:N0}";

            _score.text = r.Score.ToString("N0");
            _hitRate.text = $"{r.HitRate * 100f:0.0}%";
            _accuracy.text = $"{r.Accuracy * 100f:0.0}%";
            _maxCombo.text = r.MaxCombo.ToString();
            _judgeCounts.text = $"{r.Perfect} / {r.Great} / {r.Good}";
            _miss.text = r.Miss.ToString();
            _climax.text = !r.HasClimax ? "-" : r.ClimaxClear ? "CLEAR" : "MISSED";
            _difficulty.text = r.Difficulty.ToString().ToUpperInvariant();

            _panel.SetActive(true);
            _unlockBanner.SetActive(r.UnlocksCharacter);
        }

        void HideBanner() => _unlockBanner.SetActive(false);
    }
}
