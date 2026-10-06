using System;
using System.Collections.Generic;
using System.Linq;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RhythmCP.VnEditing
{
    /// VN 에디터 조립 루트. 게임의 VnPlaySession과 무관하게 독립 — 공유하는 건 VnPlayer·뷰 부품·데이터뿐(채보 에디터와 같은 원칙).
    /// 편집 상태(파일·라우트/판본 탭·선택·미리보기 플래그)를 들고, 뷰들은 Redraw/StateChanged 이벤트로만 갱신한다.
    public class VnEditorSession : MonoBehaviour
    {
        [SerializeField] VnCatalog _catalog;
        [SerializeField] VnPreviewPlayer _preview;

        [Tooltip("마지막 편집 후 이만큼 지나면 자동 저장(채보 에디터와 같은 2초).")]
        [SerializeField] float _autosaveDelaySec = 2f;

        /// 문서 내용·선택이 바뀜 → 라인 목록·인스펙터 다시 그리기.
        public event Action Redraw;

        /// 파일·탭·플래그·검사 결과가 바뀜 → 상단 바·플래그 패널·검사기 갱신.
        public event Action StateChanged;

        public VnCatalog Catalog => _catalog;
        public VnFileStore Store { get; } = new VnFileStore();
        public List<VnEpisodeEntry> Episodes { get; private set; } = new List<VnEpisodeEntry>();
        public string Route { get; private set; } = VnIds.Common;
        public string Edition { get; private set; } = VnIds.Base;
        public VnEpisodeEntry Entry { get; private set; }
        public VnDocument Document { get; private set; }
        public VnEpisode Episode => Document?.Episode;
        public VnFlagRegistry Flags { get; private set; }
        public bool FlagsDirty { get; private set; }

        /// 미리보기·라인 목록 "가려짐" 표시가 쓰는 가상 플래그 값. 파일에 저장하지 않는다.
        public VnFlags PreviewFlags { get; } = new VnFlags();

        public List<VnIssue> Issues { get; private set; } = new List<VnIssue>();
        public VnKnownIds Known { get; private set; }
        public string LastMessage { get; private set; }

        /// 선택한 줄들(정렬). Primary = 인스펙터가 보여주는 줄.
        public List<int> Selection { get; } = new List<int>();
        public int Primary { get; private set; } = -1;
        public VnLine PrimaryLine => Episode != null && Primary >= 0 && Primary < Episode.lines.Count ? Episode.lines[Primary] : null;

        /// 탭 목록: common + 카탈로그 캐릭터 id.
        public IEnumerable<string> Routes => new[] { VnIds.Common }.Concat(_catalog.Characters.Where(c => c != null).Select(c => c.Id));
        public IEnumerable<VnEpisodeEntry> TabEpisodes => Episodes.Where(e => e.Route == Route && e.Edition == Edition);

        float _lastEditTime, _lastFlagEditTime, _nextExternalCheck;

        void Start()
        {
            Known = BuildKnown();
            Flags = Store.LoadFlags();
            RefreshList();
            var first = TabEpisodes.FirstOrDefault();
            if (first != null) Open(first);
            else Notify();
        }

        // ------------------------------------------------------------ 파일·탭

        public void SetRoute(string route)
        {
            if (route == Route) return;
            Route = route;
            OpenFirstInTab();
        }

        public void SetEdition(string edition)
        {
            if (edition == Edition) return;
            Edition = edition;
            OpenFirstInTab();
        }

        void OpenFirstInTab()
        {
            SaveIfDirty();
            var first = TabEpisodes.FirstOrDefault();
            if (first != null) Open(first);
            else Close();
        }

        public void Open(VnEpisodeEntry entry)
        {
            SaveIfDirty();
            VnEpisode ep;
            try
            {
                ep = Store.Load(entry.AssetPath);
            }
            catch (Exception e)
            {
                Message($"열기 실패 {entry.Id}: {e.Message}");
                return;
            }
            Store.Backup(entry.AssetPath);

            if (Document != null) Document.Changed -= OnDocumentChanged;
            Entry = entry;
            Route = entry.Route;
            Edition = entry.Edition;
            Document = new VnDocument(ep);
            Document.Changed += OnDocumentChanged;
            Selection.Clear();
            Primary = -1;
            if (Episode.lines.Count > 0) Select(0, false, false);
            if (Document.IsDirty) Message("id 없는 라인에 id를 붙임");
            Revalidate();
            Redraw?.Invoke();
        }

        void Close()
        {
            if (Document != null) Document.Changed -= OnDocumentChanged;
            Document = null;
            Entry = null;
            Selection.Clear();
            Primary = -1;
            Revalidate();
            Redraw?.Invoke();
        }

        /// 현재 탭(라우트·판본)에 새 에피소드. 성인판에 같은 id 본편이 있으면 그 내용에서 시작(본편 + 삽입 씬 작업용).
        public void NewEpisode()
        {
            SaveIfDirty();
            var inEdition = Episodes.Where(e => e.Edition == Edition).ToList();
            string id = VnFileStore.NextEpisodeId(inEdition, Route, out int epIndex);

            VnEpisode ep = null;
            var baseEntry = Edition == VnIds.Adult ? Episodes.Find(e => e.Edition == VnIds.Base && e.Id == id) : null;
            if (baseEntry != null) ep = Store.Load(baseEntry.AssetPath);
            if (ep == null)
            {
                ep = new VnEpisode { cast = DefaultCast() };
                ep.lines.Add(new VnLine { id = "L001", speaker = VnIds.Narration, text = "" });
            }
            ep.meta = new VnMeta { id = id, route = Route, epIndex = epIndex, edition = Edition };

            string path = VnFileStore.PathFor(Edition, id);
            Store.Save(path, ep);
            RefreshList();
            Open(Episodes.Find(e => e.AssetPath == path));
            Message(baseEntry != null ? $"{id} 성인판 — 본편 내용에서 시작" : $"{id} 만듦");
        }

        /// 같은 라우트의 직전 에피소드 호칭을 이어받는다(호칭은 이야기 시점 따라 바뀌므로 새 에피소드에서 고치면 된다).
        List<VnCastEntry> DefaultCast()
        {
            var prev = TabEpisodes.LastOrDefault();
            if (prev != null)
            {
                try { return Store.Load(prev.AssetPath).cast; }
                catch (Exception) { /* 깨진 파일이면 기본 출연진으로 */ }
            }
            return _catalog.Characters.Where(c => c != null).Select(c => new VnCastEntry { id = c.Id, honorific = "" }).ToList();
        }

        public void Save()
        {
            if (Document != null && Entry != null)
            {
                Store.Save(Entry.AssetPath, Episode);
                Document.MarkSaved();
            }
            if (FlagsDirty)
            {
                Store.SaveFlags(Flags);
                FlagsDirty = false;
            }
            Notify();
        }

        void SaveIfDirty()
        {
            if ((Document != null && Document.IsDirty) || FlagsDirty) Save();
        }

        void RefreshList() => Episodes = Store.List();

        // ------------------------------------------------------------ 선택

        /// additive = Ctrl(토글), range = Shift(Primary부터 범위).
        public void Select(int index, bool additive, bool range)
        {
            if (Episode == null || index < 0 || index >= Episode.lines.Count) return;
            if (range && Primary >= 0)
            {
                Selection.Clear();
                for (int i = Math.Min(Primary, index); i <= Math.Max(Primary, index); i++) Selection.Add(i);
            }
            else if (additive)
            {
                if (!Selection.Remove(index)) Selection.Add(index);
                Selection.Sort();
                Primary = Selection.Contains(index) ? index : Selection.Count > 0 ? Selection[0] : -1;
            }
            else SelectOnly(index);
            Redraw?.Invoke();
        }

        void SelectOnly(int index)
        {
            Selection.Clear();
            Primary = -1;
            if (Episode != null && index >= 0 && index < Episode.lines.Count)
            {
                Selection.Add(index);
                Primary = index;
            }
        }

        // ------------------------------------------------------------ 라인 조작

        public void InsertAfterSelection()
        {
            if (Document == null) return;
            int after = Selection.Count > 0 ? Selection[Selection.Count - 1] : Episode.lines.Count - 1;
            // 새 줄 화자는 바로 앞 줄과 같게(대화가 이어지는 경우가 많아서).
            string speaker = after >= 0 && after < Episode.lines.Count ? Episode.lines[after].speaker : VnIds.Narration;
            int index = Document.InsertLine(after, speaker);
            SelectOnly(index);
            Redraw?.Invoke();
        }

        public void DuplicateSelection()
        {
            if (Document == null || Primary < 0) return;
            SelectOnly(Document.DuplicateLine(Primary));
            Redraw?.Invoke();
        }

        public void DeleteSelection()
        {
            if (Document == null || Selection.Count == 0) return;
            int first = Selection[0];
            Document.DeleteLines(Selection.ToList());
            SelectOnly(Math.Min(first, Episode.lines.Count - 1));
            Redraw?.Invoke();
        }

        public void MoveSelection(int delta)
        {
            if (Document == null || Primary < 0) return;
            SelectOnly(Document.MoveLine(Primary, delta));
            Redraw?.Invoke();
        }

        public void EditPrimary(Action<VnLine> mutate)
        {
            if (Document == null || Primary < 0) return;
            Document.EditLine(Primary, mutate);
        }

        /// 조건만 선택한 모든 줄에(결정 5).
        public void SetConditionOnSelection(Dictionary<string, string> condition)
        {
            if (Document == null || Selection.Count == 0) return;
            Document.SetCondition(Selection, condition);
        }

        public void EditEpisode(Action<VnEpisode> mutate) => Document?.Edit(mutate);

        // ------------------------------------------------------------ 플래그 등록부

        public bool AddFlag(string id, string kind, string description)
        {
            id = id?.Trim();
            if (string.IsNullOrEmpty(id) || Flags.Find(id) != null)
            {
                Message(string.IsNullOrEmpty(id) ? "플래그 id가 비어 있음" : $"이미 있는 플래그: {id}");
                return false;
            }
            Flags.flags.Add(new VnFlagDef { id = id, kind = kind, description = description });
            Flags.flags.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            FlagsEdited();
            return true;
        }

        public void RemoveFlag(string id)
        {
            Flags.flags.RemoveAll(f => f.id == id);
            PreviewFlags.counters.Remove(id);
            PreviewFlags.bools.Remove(id);
            FlagsEdited();
        }

        public void SetFlagDescription(string id, string description)
        {
            var def = Flags.Find(id);
            if (def == null || def.description == description) return;
            def.description = description;
            FlagsEdited();
        }

        void FlagsEdited()
        {
            FlagsDirty = true;
            _lastFlagEditTime = Time.unscaledTime;
            Revalidate();
            Redraw?.Invoke();
        }

        public void SetPreviewCounter(string id, int value)
        {
            PreviewFlags.counters[id] = value;
            Redraw?.Invoke();
        }

        public void SetPreviewBool(string id, bool value)
        {
            PreviewFlags.bools[id] = value;
            Redraw?.Invoke();
        }

        // ------------------------------------------------------------ 미리보기

        /// 선택한 줄부터 씬 안에서 재생. 미리보기 플래그는 사본 — 미리보기 중 고른 선택지가 패널 값을 바꾸지 않게.
        public void StartPreview()
        {
            if (Episode == null) return;
            SaveIfDirty();
            _preview.Play(VnSerializer.Clone(Episode), VnSerializer.Clone(PreviewFlags), Math.Max(0, Primary));
        }

        // ------------------------------------------------------------ 프레임

        void Update()
        {
            if (Document != null && Document.IsDirty && Time.unscaledTime - _lastEditTime > _autosaveDelaySec) Save();
            else if (FlagsDirty && Time.unscaledTime - _lastFlagEditTime > _autosaveDelaySec) Save();

            if (Time.unscaledTime >= _nextExternalCheck)
            {
                _nextExternalCheck = Time.unscaledTime + 1f;
                CheckOutsideChanges();
            }

            if (!_preview.IsOpen) HandleShortcuts();
        }

        void CheckOutsideChanges()
        {
            if (Entry != null && Store.ChangedOutside(Entry.AssetPath))
            {
                // 외부(Claude 등)가 파일을 고쳤다 — 그 내용을 정본으로. 덮이기 전 상태는 되돌리기로 복구 가능.
                try
                {
                    Document.ReplaceFromOutside(Store.Load(Entry.AssetPath));
                    SelectOnly(Math.Min(Math.Max(Primary, 0), Episode.lines.Count - 1));
                    Message("파일이 밖에서 바뀌어 다시 불러옴 (Ctrl+Z로 이전 상태)");
                    Redraw?.Invoke();
                }
                catch (Exception e)
                {
                    Message($"밖에서 바뀐 파일을 못 읽음: {e.Message}");
                }
            }
            if (Store.ChangedOutside(VnFileStore.FlagsPath) && !FlagsDirty)
            {
                Flags = Store.LoadFlags();
                Message("flags.json이 밖에서 바뀌어 다시 불러옴");
                Revalidate();
                Redraw?.Invoke();
            }
        }

        void HandleShortcuts()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool ctrl = kb.ctrlKey.isPressed;

            // 저장은 입력 중에도.
            if (ctrl && kb.sKey.wasPressedThisFrame) { Save(); Message("저장됨"); }
            if (IsTyping() || Document == null) return;

            if (ctrl && kb.zKey.wasPressedThisFrame) { Document.Undo(); ClampSelection(); }
            if (ctrl && kb.yKey.wasPressedThisFrame) { Document.Redo(); ClampSelection(); }
            if (ctrl && kb.dKey.wasPressedThisFrame) DuplicateSelection();
            if (ctrl && kb.enterKey.wasPressedThisFrame) InsertAfterSelection();
            if (kb.f5Key.wasPressedThisFrame) StartPreview();
            if (kb.deleteKey.wasPressedThisFrame) DeleteSelection();

            if (kb.altKey.isPressed)
            {
                if (kb.upArrowKey.wasPressedThisFrame) MoveSelection(-1);
                if (kb.downArrowKey.wasPressedThisFrame) MoveSelection(1);
                return;
            }
            if (kb.upArrowKey.wasPressedThisFrame) Select(Math.Max(0, Primary - 1), false, false);
            if (kb.downArrowKey.wasPressedThisFrame) Select(Math.Min(Episode.lines.Count - 1, Primary + 1), false, false);
        }

        void ClampSelection()
        {
            SelectOnly(Math.Min(Math.Max(Primary, 0), Episode.lines.Count - 1));
            Redraw?.Invoke();
        }

        static bool IsTyping()
        {
            var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return go != null && go.GetComponent<TMP_InputField>() != null;
        }

        void OnDocumentChanged()
        {
            _lastEditTime = Time.unscaledTime;
            Revalidate();
            Redraw?.Invoke();
        }

        void Revalidate()
        {
            Issues = Episode != null ? VnValidator.Validate(Episode, Flags, Known) : new List<VnIssue>();
            Notify();
        }

        void Notify() => StateChanged?.Invoke();

        public void Message(string text)
        {
            LastMessage = text;
            Notify();
        }

        /// 인스펙터 드롭다운이 카탈로그에서 id를 고르므로 검사도 카탈로그 기준(아트 전엔 스프라이트 없이 id만 등록).
        VnKnownIds BuildKnown() => new VnKnownIds
        {
            Characters = new HashSet<string>(_catalog.Characters.Where(c => c != null).Select(c => c.Id)),
            Backgrounds = new HashSet<string>(_catalog.Backgrounds.Select(b => b.id)),
            Cgs = new HashSet<string>(_catalog.Cgs.Select(c => c.id)),
            Bgms = new HashSet<string>(_catalog.Bgms.Select(b => b.id)),
        };
    }
}
