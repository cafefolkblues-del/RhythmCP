using System;
using System.Collections.Generic;
using System.Linq;
using RhythmCP.Chart;
using RhythmCP.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RhythmCP.ChartEditing
{
    public enum EditTool
    {
        Tap,
        Hold,
        Heart,
        Mash,
    }

    public struct MovePreview
    {
        public bool Active;
        public double DeltaBeat;
        public int LaneShift;
    }

    /// 채보 에디터 조립 루트. 게임의 RhythmSession과 무관하게 독립 — 공유하는 건 SongClock·RhythmInput·채보 데이터 같은 부품뿐.
    /// 편집 상태(도구·스냅·선택·커서·미리보기)를 들고, 뷰들은 Redraw/StateChanged 이벤트로만 갱신한다.
    public class ChartEditorSession : MonoBehaviour
    {
        [SerializeField] TimelineView _timeline;
        [SerializeField] PlaybackController _playback;
        [SerializeField] RecordingController _recording;
        [SerializeField] RhythmConfig _config;

        [Tooltip("재생 중 위치 표시선(영역의 자식, 가로 앵커 0.5).")]
        [SerializeField] RectTransform _playhead;
        [SerializeField] RectTransform _cursorLine;

        [Tooltip("이 씬 경로 — 테스트 플레이 후 돌아올 곳.")]
        [SerializeField] string _editorScenePath = "Assets/_Project/Scenes/ChartEditor.unity";
        [SerializeField] string _playSceneName = "RhythmPlay";

        [Tooltip("마지막 편집 후 이만큼 지나면 자동 저장(2026-10-06 확정 2초).")]
        [SerializeField] float _autosaveDelaySec = 2f;

        /// 채보·선택·미리보기가 바뀜 → 타임라인 그림 다시 그리기.
        public event Action Redraw;

        /// 곡·난이도·도구·스냅·재생 상태가 바뀜 → 패널 갱신.
        public event Action StateChanged;

        public List<SongDefinition> Songs { get; } = new List<SongDefinition>();
        public SongDefinition Song { get; private set; }
        public Difficulty Difficulty { get; private set; }
        public ChartDocument Document { get; private set; }
        public List<ValidationIssue> Issues { get; private set; } = new List<ValidationIssue>();
        public WaveformData Waveform { get; private set; }

        public EditTool Tool { get; private set; } = EditTool.Tap;
        public bool FastNotes { get; private set; }
        public int SnapDivision { get; private set; } = 4;
        public bool RecordSnap { get; set; } = true;
        public double CursorSec { get; private set; }

        public HashSet<NoteData> Selection { get; } = new HashSet<NoteData>();
        public MovePreview MovePreview;
        public NoteData CreatePreview;
        public Rect? BoxSelect;

        public PlaybackController Playback => _playback;
        public bool IsRecording => _recording.IsRecording;
        public IReadOnlyList<NoteData> PendingNotes => _recording.Pending;
        public string LastMessage { get; private set; }

        ChartFileStore _store;
        float _lastEditTime;
        float _nextExternalCheck;
        double _playStartSec;

        void Start()
        {
            FindSongs();
            var song = PlaytestHandoff.LastSong != null ? PlaytestHandoff.LastSong : Songs.FirstOrDefault();
            if (song != null) Open(song, PlaytestHandoff.LastSong != null ? PlaytestHandoff.LastDifficulty : Difficulty.Easy);
            _playback.Stopped += OnPlaybackStopped;
        }

        void OnDestroy()
        {
            if (Document != null && Document.IsDirty) Save();
            _playback.Stopped -= OnPlaybackStopped;
        }

        void FindSongs()
        {
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SongDefinition"))
                Songs.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<SongDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
#endif
        }

        // ------------------------------------------------------------ 열기 / 저장

        public void Open(SongDefinition song, Difficulty difficulty)
        {
            StopAll();
            if (Document != null && Document.IsDirty) Save();

            Song = song;
            Difficulty = difficulty;
            _store = ChartFileStore.For(song, difficulty);

            ChartData data;
            if (_store.Exists)
            {
                _store.Backup();
                data = _store.Load();
            }
            else
            {
                // 새 채보: 다른 난이도가 있으면 BPM·오프셋을 가져온다(같은 곡이니 박자는 같다).
                var other = ChartFileStore.For(song, difficulty == Difficulty.Easy ? Difficulty.Hard : Difficulty.Easy);
                data = ChartDocument.CreateEmpty(song.SongId, difficulty, other.Exists ? other.Load() : null);
            }

            if (Document != null) Document.Changed -= OnDocumentChanged;
            Document = new ChartDocument(data);
            Document.Changed += OnDocumentChanged;
            Selection.Clear();
            Waveform = LoadWaveform(song.Clip);
            CursorSec = 0;
            _timeline.Follow(0);
            Revalidate();
            Message(_store.Exists ? $"열림: {_store.AssetPath}" : $"새 채보: {_store.AssetPath} (첫 저장 때 생성)");
            NotifyAll();
        }

        public void Save()
        {
            if (Document == null) return;
            _store.Save(Document.Data, Song, Difficulty);
            Document.MarkSaved();
            StateChanged?.Invoke();
        }

        static WaveformData LoadWaveform(AudioClip clip)
        {
#if UNITY_EDITOR
            if (clip == null) return null;
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(clip));
            return WaveformData.Read(WaveformData.CachePath(guid));
#else
            return null;
#endif
        }

        void OnDocumentChanged()
        {
            _lastEditTime = Time.unscaledTime;
            // 되돌리기로 채보가 통째로 바뀌면 선택했던 노트 객체가 사라지므로, 남아 있는 것만 유지.
            Selection.RemoveWhere(n => !Document.Data.notes.Contains(n));
            Revalidate();
            NotifyAll();
        }

        void Revalidate()
        {
            double length = Song != null && Song.Clip != null ? Song.Clip.length : 0;
            Issues = ChartValidator.Validate(Document.Data, length, _config.HoldTickBeats);
        }

        // ------------------------------------------------------------ 편집 명령 (뷰·입력이 부른다)

        public double SnapBeat(double beat) => BeatGrid.Snap(beat, SnapDivision);

        public void AddNote(NoteData note)
        {
            Document.Edit(d => d.notes.Add(note));
            Selection.Clear();
            Selection.Add(note);
            NotifyAll();
        }

        public void RemoveNotes(IEnumerable<NoteData> notes)
        {
            var list = notes.ToList();
            if (list.Count == 0) return;
            Document.Edit(d => d.notes.RemoveAll(list.Contains));
            Selection.Clear();
            NotifyAll();
        }

        public void MoveSelection(double deltaBeat, int laneShift)
        {
            if (Selection.Count == 0 || (deltaBeat == 0 && laneShift == 0)) return;
            var sel = Selection.ToList();
            Document.Edit(d =>
            {
                foreach (var n in sel)
                {
                    n.beat = Math.Max(0, n.beat + deltaBeat);
                    if (n.type == NoteType.Hold || n.type == NoteType.Mash) n.endBeat = Math.Max(n.beat, n.endBeat + deltaBeat);
                    if (laneShift != 0) n.lane = n.lane == Lane.Top ? Lane.Bottom : Lane.Top;
                }
            });
        }

        public NoteData NewNote(Lane lane, double beat) => new NoteData
        {
            type = Tool switch { EditTool.Hold => NoteType.Hold, EditTool.Heart => NoteType.Heart, EditTool.Mash => NoteType.Mash, _ => NoteType.Tap },
            lane = lane,
            beat = beat,
            endBeat = Tool == EditTool.Hold || Tool == EditTool.Mash ? beat + 1.0 / SnapDivision : 0,
            // 빠른 노트는 Tap·Heart만(빠른 홀드·연타 보류, ChartLoader에서도 막혀 있음)
            speed = FastNotes && (Tool == EditTool.Tap || Tool == EditTool.Heart) ? 1.5f : 1f,
        };

        public void SetClimaxStart(double beat) => Document.Edit(d =>
        {
            d.climax ??= new ClimaxMarker { endBeat = beat + 16 };
            d.climax.startBeat = beat;
        });

        public void SetClimaxEnd(double beat) => Document.Edit(d =>
        {
            d.climax ??= new ClimaxMarker { startBeat = Math.Max(0, beat - 16) };
            d.climax.endBeat = beat;
        });

        public void ClearClimax() => Document.Edit(d => d.climax = null);

        public void SetTiming(List<BpmPoint> bpms, double offsetSec) => Document.Edit(d =>
        {
            d.bpms = bpms;
            d.offsetSec = offsetSec;
        });

        // ------------------------------------------------------------ 상태

        public void SetTool(EditTool tool) { Tool = tool; StateChanged?.Invoke(); }
        public void ToggleFast() { FastNotes = !FastNotes; StateChanged?.Invoke(); }
        public void SetSnap(int division) { SnapDivision = division; NotifyAll(); }

        public void SetCursor(double sec)
        {
            CursorSec = Math.Max(Document != null ? Document.Tempo.BeatToSec(0) - 2 : 0, sec);
            _timeline.Reveal(CursorSec);
            StateChanged?.Invoke();
        }

        public void JumpToBeat(double beat)
        {
            StopAll();
            SetCursor(Document.Tempo.BeatToSec(beat));
            _timeline.Follow(CursorSec);
        }

        public void SetLoopStart() { _playback.LoopStartSec = CursorSec; UpdateLoop(); }
        public void SetLoopEnd() { _playback.LoopEndSec = CursorSec; UpdateLoop(); }

        public void ClearLoop()
        {
            _playback.LoopEnabled = false;
            _playback.LoopStartSec = _playback.LoopEndSec = 0;
            NotifyAll();
        }

        void UpdateLoop()
        {
            _playback.LoopEnabled = _playback.LoopEndSec > _playback.LoopStartSec;
            NotifyAll();
        }

        public void NotifyRedraw() => Redraw?.Invoke();

        void NotifyAll()
        {
            Redraw?.Invoke();
            StateChanged?.Invoke();
        }

        void Message(string text)
        {
            LastMessage = text;
            StateChanged?.Invoke();
        }

        // ------------------------------------------------------------ 재생 / 녹음 / 테스트 플레이

        public void TogglePlay()
        {
            if (_playback.IsPlaying) { StopAll(); return; }
            _playStartSec = CursorSec;
            _playback.Play(Song.Clip, Document, CursorSec);
            StateChanged?.Invoke();
        }

        public void ToggleRecord()
        {
            if (_recording.IsRecording) { StopAll(); return; }
            _playStartSec = CursorSec;
            _recording.Begin(Document.Tempo, SnapDivision, RecordSnap);
            _playback.Play(Song.Clip, Document, CursorSec);
            Message("녹음 중 — F·D(상단) / J·K(하단). R 또는 Space로 끝");
        }

        void StopAll()
        {
            if (_recording.IsRecording)
            {
                var notes = _recording.End();
                if (notes.Count > 0)
                {
                    Document.Edit(d => d.notes.AddRange(notes));
                    Message($"녹음 반영: 노트 {notes.Count}개 (Ctrl+Z로 한 번에 취소)");
                }
            }
            _playback.Stop();
        }

        void OnPlaybackStopped()
        {
            // 정지하면 재생을 시작했던 자리로 커서가 돌아간다 — 같은 구간을 반복해서 듣기 쉽게.
            if (_recording.IsRecording) StopAll();
            CursorSec = _playStartSec;
            _timeline.Reveal(CursorSec);
            NotifyAll();
        }

        public void TestPlay()
        {
            StopAll();
            Save();
            PlaytestHandoff.Begin(Song, Difficulty, _editorScenePath);
            SceneManager.LoadScene(_playSceneName);
        }

        // ------------------------------------------------------------ 프레임

        void Update()
        {
            if (Document == null) return;

            if (_playback.IsPlaying)
            {
                _timeline.Follow(_playback.SongTime);
                if (_recording.IsRecording) Redraw?.Invoke();
            }

            PlaceLine(_playhead, _playback.IsPlaying ? _playback.SongTime : (double?)null);
            PlaceLine(_cursorLine, _playback.IsPlaying ? (double?)null : CursorSec);

            if (Document.IsDirty && Time.unscaledTime - _lastEditTime > _autosaveDelaySec) Save();

            if (Time.unscaledTime >= _nextExternalCheck)
            {
                _nextExternalCheck = Time.unscaledTime + 1f;
                if (_store.ChangedOutside())
                {
                    // 외부(Claude 등)가 파일을 고쳤다 — 그 내용을 정본으로. 덮이기 전 상태는 되돌리기로 복구 가능.
                    Document.ReplaceFromOutside(_store.Load());
                    Message("파일이 밖에서 바뀌어 다시 불러옴 (Ctrl+Z로 이전 상태)");
                }
            }

            HandleShortcuts();
        }

        void PlaceLine(RectTransform line, double? sec)
        {
            if (line == null) return;
            line.gameObject.SetActive(sec.HasValue);
            if (sec.HasValue) line.anchoredPosition = new Vector2(_timeline.TimeToX(sec.Value), line.anchoredPosition.y);
        }

        void HandleShortcuts()
        {
            var kb = Keyboard.current;
            if (kb == null || IsTyping()) return;
            bool ctrl = kb.ctrlKey.isPressed;

            if (kb.spaceKey.wasPressedThisFrame) TogglePlay();
            if (kb.rKey.wasPressedThisFrame && !ctrl) ToggleRecord();

            // 녹음 중엔 F·D·J·K가 레인 키라 편집 단축키를 막는다.
            if (_recording.IsRecording) return;

            if (ctrl && kb.zKey.wasPressedThisFrame) Document.Undo();
            if (ctrl && kb.yKey.wasPressedThisFrame) Document.Redo();
            if (ctrl && kb.sKey.wasPressedThisFrame) { Save(); Message("저장됨"); }
            if (ctrl) return;

            if (kb.digit1Key.wasPressedThisFrame) SetTool(EditTool.Tap);
            if (kb.digit2Key.wasPressedThisFrame) SetTool(EditTool.Hold);
            if (kb.digit3Key.wasPressedThisFrame) SetTool(EditTool.Heart);
            if (kb.digit4Key.wasPressedThisFrame) SetTool(EditTool.Mash);
            if (kb.fKey.wasPressedThisFrame) ToggleFast();
            if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) RemoveNotes(Selection);

            double cursorBeat = SnapBeat(Document.Tempo.SecToBeat(CursorSec));
            if (kb.leftBracketKey.wasPressedThisFrame) SetClimaxStart(cursorBeat);
            if (kb.rightBracketKey.wasPressedThisFrame) SetClimaxEnd(cursorBeat);
            if (kb.aKey.wasPressedThisFrame) SetLoopStart();
            if (kb.bKey.wasPressedThisFrame) SetLoopEnd();
            if (kb.escapeKey.wasPressedThisFrame) { ClearLoop(); Selection.Clear(); NotifyAll(); }

            double step = 1.0 / SnapDivision;
            if (kb.leftArrowKey.wasPressedThisFrame) SetCursor(Document.Tempo.BeatToSec(cursorBeat - step));
            if (kb.rightArrowKey.wasPressedThisFrame) SetCursor(Document.Tempo.BeatToSec(cursorBeat + step));
            if (kb.homeKey.wasPressedThisFrame) { SetCursor(Document.Tempo.BeatToSec(0)); _timeline.Follow(CursorSec); }
        }

        /// BPM 입력칸 등에 타이핑 중이면 단축키를 받지 않는다(숫자 1~4·F가 도구 전환으로 먹히는 것 방지).
        static bool IsTyping()
        {
            var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return go != null && go.TryGetComponent<TMP_InputField>(out var field) && field.isFocused;
        }
    }
}
