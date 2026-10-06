using System;
using System.Linq;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 에디터 씬 안의 미리보기(결정: 씬 전환 없이). 게임과 같은 VnPlayer·뷰 부품을 쓰고 세이브·메뉴·기읽은 뺀다.
    /// VnPlaySession을 그대로 쓰지 않는 이유: 그쪽은 세이브·메뉴까지 묶인 게임용 조립 — 에디터는 배선을 따로 둔다.
    public class VnPreviewPlayer : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [SerializeField] VnCatalog _catalog;
        [SerializeField] VnConfig _config;

        [Header("게임과 같은 뷰 부품")]
        [SerializeField] VnStageView _stage;
        [SerializeField] VnDialogueView _dialogue;
        [SerializeField] VnChoiceView _choices;
        [SerializeField] VnScreenFx _fx;
        [SerializeField] VnBgmPlayer _bgm;
        [SerializeField] VnInput _input;

        [Tooltip("미리보기 화면 전체 투명 버튼. 클릭 = 진행.")]
        [SerializeField] Button _advanceArea;
        [SerializeField] Button _close;

        [Tooltip("지금 라인 id · 플래그 값.")]
        [SerializeField] TMP_Text _status;

        public event Action Closed;

        VnPlayer _player;
        bool _firstLine;

        public bool IsOpen => _root.activeSelf;

        void Awake() => _root.SetActive(false);

        void OnEnable()
        {
            _input.Advance += OnAdvance;
            _input.ChoiceKey += OnChoiceKey;
            _input.Cancel += Close;
            _advanceArea.onClick.AddListener(OnAdvance);
            _close.onClick.AddListener(Close);
        }

        void OnDisable()
        {
            _input.Advance -= OnAdvance;
            _input.ChoiceKey -= OnChoiceKey;
            _input.Cancel -= Close;
            _advanceArea.onClick.RemoveListener(OnAdvance);
            _close.onClick.RemoveListener(Close);
        }

        public void Play(VnEpisode episode, VnFlags flags, int startIndex)
        {
            _root.SetActive(true);
            if (_player != null) _player.LineShown -= OnLineShown;
            _player = new VnPlayer(episode, flags, _catalog.DisplayName, null);
            _player.LineShown += OnLineShown;
            _player.Finished += ShowStatus;
            _firstLine = true;
            _player.Begin(startIndex);
        }

        public void Close()
        {
            if (!IsOpen) return;
            _choices.Hide();
            _bgm.Play(null, true);
            _root.SetActive(false);
            Closed?.Invoke();
        }

        void Update()
        {
            if (!IsOpen || _player == null) return;
            if (_player.AwaitingChoice && !_dialogue.IsTyping && !_choices.IsOpen)
                _choices.Show(_player.Current.Line.choice, OnChosen);
        }

        void OnLineShown(VnShownLine shown)
        {
            bool instant = _firstLine;
            _firstLine = false;
            _choices.Hide();
            _stage.Show(_player.Stage, shown, instant);
            _fx.Play(shown.Line.fx, instant);
            _bgm.Play(_catalog.Bgm(_player.Stage.bgm), instant);
            _dialogue.Show(shown, _config.CharsPerSec);
            ShowStatus();
        }

        void OnAdvance()
        {
            if (!IsOpen || _player == null) return;
            if (_player.Ended) { Close(); return; }
            if (_dialogue.IsTyping) { _dialogue.Complete(); return; }
            if (_player.AwaitingChoice) return;
            _player.Advance();
        }

        void OnChoiceKey(int index)
        {
            if (IsOpen) _choices.Choose(index);
        }

        void OnChosen(int index)
        {
            _player.Choose(index);
            ShowStatus();
            _player.Advance();
        }

        void ShowStatus()
        {
            if (_status == null || _player == null) return;
            string where = _player.Ended ? "끝 (클릭·Esc로 닫기)" : _player.Current?.Line.id;
            var f = _player.Flags;
            string flags = string.Join("  ", f.counters.Select(kv => $"{kv.Key}={kv.Value}").Concat(f.bools.Select(kv => $"{kv.Key}={(kv.Value ? "T" : "F")}")));
            _status.text = $"미리보기 {where}   {flags}";
        }
    }
}
