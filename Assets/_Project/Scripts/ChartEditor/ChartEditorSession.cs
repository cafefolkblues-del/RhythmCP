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
        [SerializeField] AutoChartSettings _autoChart;

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

        /// 분석 사이드카(analysis.json). 없으면 null — 자동 채보 패널에서 [분석 실행].
        public AnalysisData Analysis { get; private set; }
        public AutoChartParams AutoParams => _autoChart.Params;

        /// 마지막 자동 생성 결과(마디별 패턴 ID·지표). 눈금 띠가 마디마다 패턴 ID를 보여준다. 다른 곡을 열면 지운다.
        public AutoChartResult LastAutoResult { get; private set; }
        public bool IsAnalyzing => _analysisRunner.IsRunning;
        public string AnalysisStage => _analysisRunner.Stage;

        /// BPM·오프셋을 고치면 다른 난이도 채보에도 같이 반영(파일을 난이도별로 나눈 포맷을 유지하는 대신 두는 장치, 2026-10-07).
        public bool SyncTimingAcrossDifficulties { get; set; } = true;

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
        readonly AnalysisRunner _analysisRunner = new AnalysisRunner();
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
            Analysis = AnalysisData.Read(ChartFileStore.AnalysisPathFor(song));
            LastAutoResult = null;
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

        /// Alt를 누르고 있으면 스냅 없이 그대로(자유 배치, 스펙 §6 "수동 = 스냅 기본 + 자유 토글").
        public double SnapBeat(double beat)
        {
            var kb = Keyboard.current;
            return kb != null && kb.altKey.isPressed ? beat : BeatGrid.Snap(beat, SnapDivision);
        }

        /// 선택한 노트들의 타입을 바꾼다(Shift+1~4). 홀드·연타로 바꾸면 기본 길이 1박.
        public void ChangeSelectionType(NoteType type)
        {
            if (Selection.Count == 0) return;
            var sel = Selection.ToList();
            Document.Edit(d =>
            {
                foreach (var n in sel)
                {
                    bool hadLength = n.type == NoteType.Hold || n.type == NoteType.Mash;
                    n.type = type;
                    bool hasLength = type == NoteType.Hold || type == NoteType.Mash;
                    if (hasLength && !hadLength) n.endBeat = n.beat + 1;
                    if (!hasLength) n.endBeat = 0;
                    if (type != NoteType.Tap && type != NoteType.Heart) n.speed = 1f;
                }
            });
        }

        /// 홀드·연타 끝 조정(꼬리 드래그).
        public void SetEndBeat(NoteData note, double endBeat) => Document.Edit(_ => note.endBeat = Math.Max(note.beat + 1.0 / SnapDivision, endBeat));

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

        public void SetTiming(List<BpmPoint> bpms, double offsetSec)
        {
            Document.Edit(d =>
            {
                d.bpms = bpms;
                d.offsetSec = offsetSec;
            });
            if (SyncTimingAcrossDifficulties) SyncOtherDifficultyTiming();
        }

        void SyncOtherDifficultyTiming()
        {
            var other = Difficulty == Difficulty.Easy ? Difficulty.Hard : Difficulty.Easy;
            var store = ChartFileStore.For(Song, other);
            if (!store.Exists) return;
            var data = store.Load();
            data.bpms = Document.Data.bpms.Select(b => new BpmPoint { beat = b.beat, bpm = b.bpm }).ToList();
            data.offsetSec = Document.Data.offsetSec;
            store.Save(data, Song, other);
            Message($"{other} 채보 타이밍도 같이 맞춤");
        }

        // ------------------------------------------------------------ 자동 채보

        public void RunAnalysis(bool force)
        {
            if (Song == null || Song.Clip == null || IsAnalyzing) return;
#if UNITY_EDITOR
            string audio = UnityEditor.AssetDatabase.GetAssetPath(Song.Clip);
            _analysisRunner.Start(_autoChart.PythonCommand, _autoChart.AnalyzerPath, audio, ChartFileStore.AnalysisPathFor(Song), force);
            Message("분석 중…");
#endif
        }

        /// 분석 BPM(또는 후보 BPM)·오프셋을 채보 타이밍으로. 노트는 박 단위라 시각이 같이 바뀐다 — 생성 전에 쓰는 버튼.
        public void ApplyAnalysisTiming(double bpm)
        {
            if (Analysis == null) return;
            double period = 60.0 / bpm;
            double offset = (Analysis.offsetMs / 1000.0) % period;
            SetTiming(new List<BpmPoint> { new BpmPoint { beat = 0, bpm = bpm } }, offset);
        }

        /// 마디 첫 박 맞추기: 분석은 박의 위상만 알고 어느 박이 마디 시작인지 모른다 — 0박 위치를 한 박씩 민다.
        public void ShiftDownbeat(int beats)
        {
            double period = 60.0 / Document.Tempo.BpmAtBeat(0);
            SetTiming(Document.Data.bpms.Select(b => new BpmPoint { beat = b.beat, bpm = b.bpm }).ToList(),
                Document.Data.offsetSec + beats * period);
        }

        public void GenerateAuto()
        {
            if (Analysis == null) { Message("먼저 분석을 실행하세요"); return; }
            var library = PatternLibrary.Load(_autoChart.EasyPatternsPath);
            if (library == null) { Message("패턴 라이브러리 없음: " + _autoChart.EasyPatternsPath); return; }
            var result = AutoCharter.Generate(Analysis, Document.Tempo, AutoParams, library);
            LastAutoResult = result;
            Document.Edit(d =>
            {
                d.notes = result.Notes;
                d.climax = result.Climax;
            });
            Selection.Clear();
            string bpmHint = Math.Abs(Document.Tempo.BpmAtBeat(0) - Analysis.bpm) > 0.5 ? $" · 채보 BPM이 분석({Analysis.bpm:0.##})과 다름" : "";
            string warn = result.Warnings.Count > 0 ? " · " + string.Join(" / ", result.Warnings) : "";
            Message($"자동 생성 (Ctrl+Z로 되돌리기) · {result.Metrics}{bpmHint}{warn}");
            NotifyAll();
        }

        /// 선택한 노트(한 마디 안)를 리듬·레인 패턴으로 라이브러리에 저장 — 손본 채보가 라이브러리를 키운다(경로 다).
        public void HarvestSelection()
        {
            if (Selection.Count == 0) { Message("저장할 노트를 먼저 선택하세요(한 마디 안)"); return; }
            var notes = Selection.OrderBy(n => n.beat).ToList();
            double barStart = Math.Floor(notes[0].beat / BeatGrid.BeatsPerBar) * BeatGrid.BeatsPerBar;
            if (notes[notes.Count - 1].beat >= barStart + BeatGrid.BeatsPerBar) { Message("패턴은 한 마디 안의 노트만 저장할 수 있어요"); return; }

            var beats = notes.Select(n => Math.Round((n.beat - barStart) * 1e6) / 1e6).ToList();
            var lanes = new string(notes.Select(n => n.lane == notes[0].lane ? 'A' : 'B').ToArray());
            var library = PatternLibrary.Load(_autoChart.EasyPatternsPath);
            if (library == null) { Message("패턴 라이브러리 없음"); return; }
            var (rhythmId, laneId, added) = library.Harvest(beats, lanes);
            if (added) System.IO.File.WriteAllText(_autoChart.EasyPatternsPath, library.ToJson());
            Message(added ? $"패턴 저장: 리듬 {rhythmId} · 레인 {laneId}" : $"이미 있는 패턴: 리듬 {rhythmId} · 레인 {laneId}");
        }

        void PollAnalysis()
        {
            _analysisRunner.Poll();
            if (!_analysisRunner.TryFinish(out bool ok, out string error))
            {
                if (_analysisRunner.IsRunning) StateChanged?.Invoke();
                return;
            }
            if (ok)
            {
                Analysis = AnalysisData.Read(ChartFileStore.AnalysisPathFor(Song));
                Message(Analysis != null ? $"분석 완료: BPM {Analysis.bpm:0.##} · 온셋 {Analysis.onsets.Count}개" : "분석 파일을 읽지 못함");
            }
            else Message("분석 실패: " + error);
            NotifyAll();
        }

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

            PollAnalysis();
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

            if (kb.shiftKey.isPressed)
            {
                // Shift+1~4: 선택한 노트의 타입 변경(스펙 §6 수동 편집 "타입 변경").
                if (kb.digit1Key.wasPressedThisFrame) ChangeSelectionType(NoteType.Tap);
                if (kb.digit2Key.wasPressedThisFrame) ChangeSelectionType(NoteType.Hold);
                if (kb.digit3Key.wasPressedThisFrame) ChangeSelectionType(NoteType.Heart);
                if (kb.digit4Key.wasPressedThisFrame) ChangeSelectionType(NoteType.Mash);
                return;
            }

            if (kb.digit1Key.wasPressedThisFrame) SetTool(EditTool.Tap);
            if (kb.digit2Key.wasPressedThisFrame) SetTool(EditTool.Hold);
            if (kb.digit3Key.wasPressedThisFrame) SetTool(EditTool.Heart);
            if (kb.digit4Key.wasPressedThisFrame) SetTool(EditTool.Mash);
            if (kb.fKey.wasPressedThisFrame) ToggleFast();
            if (kb.pKey.wasPressedThisFrame) HarvestSelection(); // P: 선택한 마디를 패턴 라이브러리에 저장
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
