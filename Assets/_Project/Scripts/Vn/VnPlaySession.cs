using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// VN 한 에피소드 재생의 조립. VnPlayer(진행 규칙)와 뷰들은 서로를 모르고 여기서만 연결한다.
    /// 에피소드는 지금은 인스펙터 지정 — 허브(M4)가 생기면 GameSession에서 받는다.
    public class VnPlaySession : MonoBehaviour
    {
        [SerializeField] TextAsset _episode;
        [SerializeField] VnCatalog _catalog;
        [SerializeField] VnConfig _config;

        [Tooltip("시작 라인 인덱스(0부터). 앞 라인들의 무대 지시는 다시 쌓아서 시작한다.")]
        [SerializeField] int _startIndex;

        [Header("뷰")]
        [SerializeField] VnStageView _stage;
        [SerializeField] VnDialogueView _dialogue;
        [SerializeField] VnChoiceView _choices;
        [SerializeField] VnScreenFx _fx;
        [SerializeField] VnBgmPlayer _bgm;
        [SerializeField] VnInput _input;

        [Tooltip("화면 전체 투명 버튼(대사창 포함, 선택지·메뉴보다 아래). 클릭 = 진행.")]
        [SerializeField] Button _advanceArea;

        [Header("메뉴")]
        [SerializeField] VnMenuView _menu;
        [SerializeField] VnBacklogView _backlog;
        [SerializeField] VnSaveMenuView _saveMenu;
        [SerializeField] VnOneLinerView _oneLiner;

        public event Action Finished;

        VnPlayer _player;
        VnReadLog _readLog;
        VnSaveStore _saves;
        VnAutoAdvance _auto;
        bool _firstLine;

        public VnPlayer Player => _player;

        bool OverlayOpen => _backlog.IsOpen || _saveMenu.IsOpen;

        void Awake()
        {
            // 기읽 = 전역 파일 하나, 세이브 = 슬롯 폴더. 게임 세이브와의 결합은 M4/M5.
            _readLog = new VnReadLog(Path.Combine(Application.persistentDataPath, "vn_read.json"));
            _saves = new VnSaveStore(Path.Combine(Application.persistentDataPath, "vn_saves"), _config.SaveSlotCount);
            _auto = new VnAutoAdvance(_config.AutoBaseSec, _config.AutoPerCharSec, _config.SkipIntervalSec);
        }

        void Start()
        {
            _menu.ShowMode(_auto.Mode);
            _menu.ShowVnOnly(VnPreferences.VnOnly);
            StartEpisode(VnSerializer.ReadEpisode(_episode.text), new VnFlags(), _startIndex, null);
        }

        void OnEnable()
        {
            _input.Advance += OnAdvanceInput;
            _input.ChoiceKey += OnChoiceKey;
            _input.Backlog += OpenBacklog;
            _input.Cancel += CloseOverlay;
            _advanceArea.onClick.AddListener(OnAdvanceInput);
            _auto.ModeChanged += _menu.ShowMode;
            _menu.ModeClicked += ToggleMode;
            _menu.BacklogClicked += OpenBacklog;
            _menu.SaveClicked += OpenSave;
            _menu.LoadClicked += OpenLoad;
            _menu.VnOnlyClicked += ToggleVnOnly;
        }

        void OnDisable()
        {
            _input.Advance -= OnAdvanceInput;
            _input.ChoiceKey -= OnChoiceKey;
            _input.Backlog -= OpenBacklog;
            _input.Cancel -= CloseOverlay;
            _advanceArea.onClick.RemoveListener(OnAdvanceInput);
            _auto.ModeChanged -= _menu.ShowMode;
            _menu.ModeClicked -= ToggleMode;
            _menu.BacklogClicked -= OpenBacklog;
            _menu.SaveClicked -= OpenSave;
            _menu.LoadClicked -= OpenLoad;
            _menu.VnOnlyClicked -= ToggleVnOnly;
        }

        // 기읽 기록은 매 라인 쓰지 않고 종료·세이브·에피소드 끝에 한 번에.
        void OnApplicationQuit() => _readLog.Flush();
        void OnDestroy() => _readLog?.Flush();

        /// 처음 시작·세이브 불러오기 공용. stage가 있으면(세이브) 그 무대에서 해당 라인을 다시 적용한다.
        void StartEpisode(VnEpisode episode, VnFlags flags, int index, VnStageState stage)
        {
            if (_player != null)
            {
                _player.LineShown -= OnLineShown;
                _player.Finished -= OnFinished;
            }
            _choices.Hide();
            _player = new VnPlayer(episode, flags, _catalog.DisplayName, _readLog);
            _player.LineShown += OnLineShown;
            _player.Finished += OnFinished;
            _firstLine = true;
            _player.Begin(index, stage);
        }

        void Update()
        {
            if (_player == null || _player.Ended || OverlayOpen) return;

            bool held = _input.SkipHeld;
            if (_auto.Tick(Time.unscaledDeltaTime, _dialogue.IsTyping, _player.AwaitingChoice, held))
            {
                _dialogue.Complete();
                _player.Advance();
                return;
            }

            // 선택지는 대사가 다 찍힌 뒤에 띄운다(질문을 읽고 고르게).
            if (_player.AwaitingChoice && !_dialogue.IsTyping && !_choices.IsOpen)
                _choices.Show(_player.Current.Line.choice, OnChosen);
        }

        void OnLineShown(VnShownLine shown)
        {
            // 첫 라인(처음·중간 시작·불러오기)과 스킵 중에는 연출 없이 바로.
            bool skipping = _auto.IsSkipping || _input.SkipHeld;
            bool instant = _firstLine || skipping;
            _firstLine = false;

            _choices.Hide();
            _stage.Show(_player.Stage, shown, instant);
            _fx.Play(shown.Line.fx, instant);
            _bgm.Play(_catalog.Bgm(_player.Stage.bgm), instant);
            _dialogue.Show(shown, _config.CharsPerSec);
            if (skipping) _dialogue.Complete();
            _auto.OnLineShown(shown, (shown.Line.text ?? string.Empty).Length);
        }

        void OnAdvanceInput()
        {
            if (_player == null || _player.Ended || OverlayOpen) return;
            // 스킵 중 직접 누르면 스킵을 끈다(오토는 유지 — 누르면 한 줄 먼저 넘어갈 뿐).
            if (_auto.IsSkipping)
            {
                _auto.Mode = VnAdvanceMode.Manual;
                return;
            }
            if (_dialogue.IsTyping)
            {
                _dialogue.Complete();
                return;
            }
            if (_player.AwaitingChoice) return;
            _player.Advance();
        }

        void OnChoiceKey(int index)
        {
            if (!OverlayOpen) _choices.Choose(index);
        }

        void OnChosen(int index)
        {
            _player.Choose(index);
            _player.Advance();
        }

        void OnFinished()
        {
            _readLog.Flush();
            _auto.Mode = VnAdvanceMode.Manual;
            Debug.Log($"[VN] {_player.Episode.meta.id} 끝");
            Finished?.Invoke();
        }

        // ---------------- 메뉴

        /// 같은 버튼을 다시 누르면 끈다.
        void ToggleMode(VnAdvanceMode mode) => _auto.Mode = _auto.Mode == mode ? VnAdvanceMode.Manual : mode;

        void OpenBacklog()
        {
            if (OverlayOpen || _player == null) return;
            _auto.Mode = VnAdvanceMode.Manual;
            _backlog.Open(_player.Backlog);
        }

        void CloseOverlay()
        {
            if (_saveMenu.IsOpen) _saveMenu.Close();
            else if (_backlog.IsOpen) _backlog.Close();
        }

        void OpenSave()
        {
            if (OverlayOpen || _player == null || _player.Ended) return;
            _auto.Mode = VnAdvanceMode.Manual;
            _saveMenu.Open(_saves, true, SaveTo);
        }

        void OpenLoad()
        {
            if (OverlayOpen) return;
            _auto.Mode = VnAdvanceMode.Manual;
            _saveMenu.Open(_saves, false, LoadFrom);
        }

        // TODO(UI 다듬기): 덮어쓰기 확인 없이 바로 저장 중 — 확인창은 VnSaveMenuView 쪽 TODO 참고.
        void SaveTo(int slot)
        {
            var cur = _player.Current;
            _saves.Save(slot, new VnSaveData
            {
                episodeId = _player.Episode.meta.id,
                lineId = cur.Line.id,
                flags = VnSerializer.Clone(_player.Flags),
                stage = VnSerializer.Clone(_player.StageBeforeCurrent),
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                speakerName = cur.Name,
                preview = Preview(cur.Line.text),
            });
            _readLog.Flush();
            _saveMenu.Refresh();
        }

        void LoadFrom(int slot)
        {
            var data = _saves.Load(slot);
            if (data == null) return;

            var asset = data.episodeId == _player?.Episode.meta.id ? null : _catalog.Episode(data.episodeId);
            var episode = asset != null ? VnSerializer.ReadEpisode(asset.text) : _player?.Episode;
            if (episode == null || episode.meta.id != data.episodeId)
            {
                Debug.LogWarning($"[VN] 세이브의 에피소드 {data.episodeId}를 카탈로그에서 못 찾음");
                return;
            }

            int index = episode.lines.FindIndex(l => l.id == data.lineId);
            if (index < 0)
            {
                // 라인이 스크립트에서 지워진 세이브 — 무대 재구성으로 처음부터 대신.
                Debug.LogWarning($"[VN] 세이브 라인 {data.lineId}가 {data.episodeId}에 없음 — 처음부터");
                index = 0;
                data.stage = null;
            }
            _saveMenu.Close();
            StartEpisode(episode, data.flags ?? new VnFlags(), index, data.stage);
        }

        static string Preview(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= 24 ? text : text.Substring(0, 24) + "…";
        }

        // ---------------- VN만 보기

        void ToggleVnOnly()
        {
            VnPreferences.VnOnly = !VnPreferences.VnOnly;
            _menu.ShowVnOnly(VnPreferences.VnOnly);
            if (VnPreferences.VnOnly) ShowOneLiner();
        }

        /// 지금 말하는 캐릭터의 한마디, 없으면 이 에피소드 출연진 중 한마디가 있는 캐릭터 아무나.
        void ShowOneLiner()
        {
            if (_player == null) return;
            var pool = new List<VnCharacter>();
            var speaker = _catalog.Character(_player.Current?.SpeakerId);
            if (speaker != null && speaker.MetaLines.Count > 0) pool.Add(speaker);
            else
                foreach (var c in _player.Episode.cast)
                {
                    var ch = _catalog.Character(c.id);
                    if (ch != null && ch.MetaLines.Count > 0) pool.Add(ch);
                }
            if (pool.Count == 0) return;

            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            _oneLiner.Show(pick.DisplayName, pick.MetaLines[UnityEngine.Random.Range(0, pick.MetaLines.Count)], _config.OneLinerSec);
        }
    }
}
