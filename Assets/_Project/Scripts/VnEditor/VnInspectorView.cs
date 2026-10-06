using System.Collections.Generic;
using System.Linq;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 가운데 인스펙터(와이어프레임 02 ⑧~⑪): 선택한 줄의 화자·호칭·텍스트·연출·조건·선택지.
    /// 연출(표정·배경·CG·BGM)은 카탈로그 id 드롭다운, 조건은 행(플래그 드롭다운) — 승인 구조대로, 오타가 안 생기게.
    /// 아트 전에도 쓸 수 있도록 카탈로그 항목은 스프라이트 없이 id만 있어도 고를 수 있다.
    /// 글자 칸은 입력을 마칠 때(onEndEdit) 한 번 반영 — 글자마다 되돌리기 칸이 쌓이지 않게.
    /// 조건만 여러 줄 선택 시 전부에 적용(결정 5), 나머지는 Primary 한 줄.
    public class VnInspectorView : MonoBehaviour
    {
        [SerializeField] VnEditorSession _session;

        [Tooltip("선택한 줄이 없을 때 숨길 내용 루트.")]
        [SerializeField] GameObject _content;
        [SerializeField] TMP_Text _lineId;

        [Header("⑧ 화자 / 호칭")]
        [SerializeField] TMP_Dropdown _speaker;
        [SerializeField] GameObject _asRow;
        [SerializeField] TMP_Dropdown _as;
        [SerializeField] TMP_Text _nameplatePreview;

        [Header("텍스트")]
        [SerializeField] TMP_InputField _text;

        [Header("⑨ 연출")]
        [SerializeField] GameObject _actorRow;

        [Tooltip("화자 캐릭터의 표정 id(VnCharacter 표정 목록).")]
        [SerializeField] TMP_Dropdown _expr;
        [SerializeField] TMP_Dropdown _pos;

        [Tooltip("쉼표로 여러 명: yume, nemu")]
        [SerializeField] TMP_InputField _exit;
        [SerializeField] TMP_Dropdown _bg;
        [SerializeField] TMP_Dropdown _bgTransition;

        [Tooltip("— / off / 카탈로그 CG id")]
        [SerializeField] TMP_Dropdown _cg;

        [Tooltip("— / stop / 카탈로그 BGM id")]
        [SerializeField] TMP_Dropdown _bgm;
        [SerializeField] Toggle _fxFade;
        [SerializeField] Toggle _fxShake;
        [SerializeField] Toggle _fxColorBleed;

        [Header("⑩ 조건 (if)")]
        [Tooltip("조건 행 템플릿. 같은 부모 아래 복제, 추가 버튼은 맨 아래로 옮겨진다.")]
        [SerializeField] VnConditionRowView _conditionTemplate;
        [SerializeField] Button _addCondition;

        [Tooltip("적용 범위 안내(선택 N줄에 적용).")]
        [SerializeField] TMP_Text _conditionNote;

        [Header("⑪ 선택지")]
        [SerializeField] VnChoiceRowView _choiceTemplate;
        [SerializeField] Button _addChoice;
        [SerializeField] TMP_Text _choiceError;

        static readonly string[] PosOptions = { "", "left", "center", "right" };

        readonly List<string> _speakerIds = new List<string>();
        readonly List<string> _asIds = new List<string>();
        readonly List<VnChoiceRowView> _choiceRows = new List<VnChoiceRowView>();
        readonly List<VnConditionRowView> _conditionRows = new List<VnConditionRowView>();
        readonly List<string> _exprIds = new List<string>();
        readonly List<string> _bgIds = new List<string>();
        readonly List<string> _cgIds = new List<string>();
        readonly List<string> _bgmIds = new List<string>();

        void Start()
        {
            _choiceTemplate.gameObject.SetActive(false);
            _conditionTemplate.gameObject.SetActive(false);

            _speaker.onValueChanged.AddListener(OnSpeaker);
            _as.onValueChanged.AddListener(i => _session.EditPrimary(l => l.asCharacter = _asIds[i]));
            _text.onEndEdit.AddListener(v => EditString(l => l.text, (l, s) => l.text = s, v));
            _expr.onValueChanged.AddListener(i => _session.EditPrimary(l => l.expr = _exprIds[i]));
            _pos.ClearOptions();
            _pos.AddOptions(PosOptions.Select(p => p == "" ? "—" : p).ToList());
            _pos.onValueChanged.AddListener(i => _session.EditPrimary(l => l.pos = PosOptions[i] == "" ? null : PosOptions[i]));
            _exit.onEndEdit.AddListener(OnExit);
            _bg.onValueChanged.AddListener(_ => OnBg());
            _bgTransition.ClearOptions();
            _bgTransition.AddOptions(VnIds.Transitions.ToList());
            _bgTransition.onValueChanged.AddListener(_ => OnBg());
            _cg.onValueChanged.AddListener(i => _session.EditPrimary(l => l.cg = _cgIds[i]));
            _bgm.onValueChanged.AddListener(i => _session.EditPrimary(l => l.bgm = _bgmIds[i]));
            _fxFade.onValueChanged.AddListener(on => OnFx("fade", on));
            _fxShake.onValueChanged.AddListener(on => OnFx("shake", on));
            _fxColorBleed.onValueChanged.AddListener(on => OnFx("colorBleed", on));
            _addCondition.onClick.AddListener(OnAddCondition);
            _addChoice.onClick.AddListener(OnAddChoice);

            _session.Redraw += Refresh;
            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            _session.Redraw -= Refresh;
            _session.StateChanged -= Refresh;
        }

        // ------------------------------------------------------------ 표시

        void Refresh()
        {
            var line = _session.PrimaryLine;
            _content.SetActive(line != null);
            if (line == null) return;
            var ep = _session.Episode;

            _lineId.text = _session.Selection.Count > 1 ? $"{line.id}  (선택 {_session.Selection.Count}줄)" : line.id;

            // 화자: narration · mc · 카탈로그 캐릭터 · (카탈로그에 없지만 파일에 있는 id도 그대로 보이게)
            _speakerIds.Clear();
            _speakerIds.Add(VnIds.Narration);
            _speakerIds.Add(VnIds.Mc);
            _speakerIds.AddRange(_session.Catalog.Characters.Where(c => c != null).Select(c => c.Id));
            if (!string.IsNullOrEmpty(line.speaker) && !_speakerIds.Contains(line.speaker)) _speakerIds.Add(line.speaker);
            _speaker.ClearOptions();
            _speaker.AddOptions(_speakerIds.Select(SpeakerLabel).ToList());
            _speaker.SetValueWithoutNotify(Mathf.Max(0, _speakerIds.IndexOf(line.speaker)));

            bool mc = line.speaker == VnIds.Mc;
            bool character = !mc && line.speaker != VnIds.Narration;
            _asRow.SetActive(mc);
            _actorRow.SetActive(character);

            _asIds.Clear();
            _asIds.Add(null);
            _asIds.AddRange(ep.cast.Select(c => c.id));
            _as.ClearOptions();
            _as.AddOptions(_asIds.Select(id => id == null ? "(직전 화자)" : $"{_session.Catalog.DisplayName(id)} · \"{ep.HonorificOf(id)}\"").ToList());
            _as.SetValueWithoutNotify(Mathf.Max(0, _asIds.IndexOf(line.asCharacter)));
            _nameplatePreview.text = NameplatePreview(ep, _session.Primary);

            SetText(_text, line.text);
            var catalog = _session.Catalog;
            FillIds(_expr, _exprIds, catalog.Character(line.speaker)?.ExpressionIds ?? Enumerable.Empty<string>(), line.expr);
            _pos.SetValueWithoutNotify(Mathf.Max(0, System.Array.IndexOf(PosOptions, line.pos ?? "")));
            SetText(_exit, VnTextSyntax.FormatList(line.exit));

            SplitBg(line.bg, out string bgId, out string transition);
            FillIds(_bg, _bgIds, catalog.Backgrounds.Select(b => b.id), bgId);
            _bgTransition.SetValueWithoutNotify(Mathf.Max(0, System.Array.IndexOf(VnIds.Transitions, transition)));
            _bgTransition.interactable = bgId != null;
            FillIds(_cg, _cgIds, new[] { VnIds.Off }.Concat(catalog.Cgs.Select(c => c.id)), line.cg);
            FillIds(_bgm, _bgmIds, new[] { VnIds.Stop }.Concat(catalog.Bgms.Select(b => b.id)), line.bgm);
            _fxFade.SetIsOnWithoutNotify(line.fx != null && line.fx.Contains("fade"));
            _fxShake.SetIsOnWithoutNotify(line.fx != null && line.fx.Contains("shake"));
            _fxColorBleed.SetIsOnWithoutNotify(line.fx != null && line.fx.Contains("colorBleed"));

            RefreshConditions(line);
            int n = _session.Selection.Count;
            bool mixed = n > 1 && _session.Selection.Select(i => VnTextSyntax.FormatCondition(ep.lines[i].condition)).Distinct().Count() > 1;
            _conditionNote.text = n <= 1 ? "모두 만족할 때만 이 줄을 보여준다(AND)"
                : mixed ? $"선택 {n}줄에 적용 — 지금은 줄마다 다름(이 줄 값 표시, 고치면 전부 이 값으로)" : $"선택 {n}줄에 적용";

            RefreshChoices(line);
        }

        void RefreshConditions(VnLine line)
        {
            var pairs = line.condition?.ToList() ?? new List<KeyValuePair<string, string>>();
            while (_conditionRows.Count < pairs.Count)
            {
                var row = Instantiate(_conditionTemplate, _conditionTemplate.transform.parent);
                row.Changed += OnConditionRow;
                row.DeleteClicked += OnConditionDelete;
                _conditionRows.Add(row);
            }
            for (int i = 0; i < _conditionRows.Count; i++)
            {
                bool used = i < pairs.Count;
                _conditionRows[i].gameObject.SetActive(used);
                if (used) _conditionRows[i].Set(i, _session.Flags, pairs[i].Key, pairs[i].Value);
            }
            _addCondition.interactable = _session.Flags.flags.Count > 0;
            _addCondition.transform.SetAsLastSibling();
        }

        /// 드롭다운 = "—"(없음) + 고정 값(off/stop) + 카탈로그 id. 파일에 카탈로그에 없는 값이 있으면 "(카탈로그에 없음)"으로 남겨 보인다.
        static void FillIds(TMP_Dropdown dropdown, List<string> store, IEnumerable<string> ids, string current)
        {
            store.Clear();
            store.Add(null);
            store.AddRange(ids.Where(id => !string.IsNullOrEmpty(id)).Distinct());
            bool unknown = !string.IsNullOrEmpty(current) && !store.Contains(current);
            if (unknown) store.Add(current);
            dropdown.ClearOptions();
            dropdown.AddOptions(store.Select(id => id == null ? "—" : unknown && id == current ? $"{id} (카탈로그에 없음)" : id).ToList());
            dropdown.SetValueWithoutNotify(Mathf.Max(0, store.IndexOf(string.IsNullOrEmpty(current) ? null : current)));
        }

        void RefreshChoices(VnLine line)
        {
            int count = line.choice?.Count ?? 0;
            while (_choiceRows.Count < count)
            {
                var row = Instantiate(_choiceTemplate, _choiceTemplate.transform.parent);
                row.TextEdited += OnChoiceText;
                row.EffectsEdited += OnChoiceEffects;
                row.DeleteClicked += OnChoiceDelete;
                _choiceRows.Add(row);
            }
            for (int i = 0; i < _choiceRows.Count; i++)
            {
                bool used = i < count;
                _choiceRows[i].gameObject.SetActive(used);
                if (used) _choiceRows[i].Set(i, line.choice[i].text, VnTextSyntax.FormatEffects(line.choice[i]));
            }
            // 버튼을 맨 아래로(행이 템플릿 옆에 복제되므로).
            _addChoice.transform.SetAsLastSibling();
        }

        /// 이 줄이 재생될 때 실제로 뜰 이름표 — 직전 캐릭터 호칭 규칙을 눈으로 확인.
        string NameplatePreview(VnEpisode ep, int index)
        {
            var line = ep.lines[index];
            if (line.speaker == VnIds.Narration) return "이름표 없음";
            if (line.speaker != VnIds.Mc) return $"이름표: {_session.Catalog.DisplayName(line.speaker)}";
            string whose = line.asCharacter;
            for (int i = index - 1; whose == null && i >= 0; i--)
            {
                var s = ep.lines[i].speaker;
                if (s != VnIds.Mc && s != VnIds.Narration && !string.IsNullOrEmpty(s)) whose = s;
            }
            string honorific = whose != null ? ep.HonorificOf(whose) : null;
            return honorific != null ? $"이름표: \"{honorific}\" ({_session.Catalog.DisplayName(whose)}의 호칭)" : "이름표: ??? — as 지정 필요";
        }

        string SpeakerLabel(string id) => id == VnIds.Narration ? "나레이션" : id == VnIds.Mc ? "mc (남주)" : $"{_session.Catalog.DisplayName(id)} ({id})";

        static void SetText(TMP_InputField field, string value)
        {
            if (!field.isFocused) field.SetTextWithoutNotify(value ?? string.Empty);
        }

        static void SplitBg(string bg, out string id, out string transition)
        {
            id = bg;
            transition = VnIds.Transitions[0];
            if (string.IsNullOrEmpty(bg)) return;
            int dot = bg.LastIndexOf('.');
            if (dot <= 0) return;
            id = bg.Substring(0, dot);
            transition = bg.Substring(dot + 1);
        }

        // ------------------------------------------------------------ 편집

        void OnSpeaker(int index)
        {
            string id = _speakerIds[index];
            _session.EditPrimary(l =>
            {
                l.speaker = id;
                if (id != VnIds.Mc) l.asCharacter = null;
                if (id == VnIds.Mc || id == VnIds.Narration) { l.expr = null; l.pos = null; }
            });
        }

        /// 값이 그대로면 편집 기록을 남기지 않는다(포커스만 옮겨도 onEndEdit이 온다).
        void EditString(System.Func<VnLine, string> get, System.Action<VnLine, string> set, string value)
        {
            var line = _session.PrimaryLine;
            if (line == null) return;
            string v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            string current = string.IsNullOrEmpty(get(line)) ? null : get(line);
            if (current == v) return;
            _session.EditPrimary(l => set(l, v));
        }

        void OnExit(string value)
        {
            var list = VnTextSyntax.ParseList(value);
            if (VnTextSyntax.FormatList(list) == VnTextSyntax.FormatList(_session.PrimaryLine?.exit)) return;
            _session.EditPrimary(l => l.exit = list);
        }

        void OnBg()
        {
            string id = _bgIds[_bg.value];
            string transition = VnIds.Transitions[_bgTransition.value];
            // 컷은 생략형(id만)으로 저장 — 스펙 표기 "hall" / "hall.fade".
            string bg = id == null ? null : transition == VnIds.Transitions[0] ? id : $"{id}.{transition}";
            if (bg == _session.PrimaryLine?.bg) return;
            _session.EditPrimary(l => l.bg = bg);
        }

        void OnFx(string fx, bool on)
        {
            _session.EditPrimary(l =>
            {
                l.fx ??= new List<string>();
                l.fx.Remove(fx);
                if (on) l.fx.Add(fx);
                // 순서를 고정해 같은 내용이면 같은 JSON(diff가 깔끔하게).
                l.fx = VnIds.Effects.Where(l.fx.Contains).ToList();
                if (l.fx.Count == 0) l.fx = null;
            });
        }

        // 조건 행 편집 → 행 전체로 조건을 다시 만들어 선택한 모든 줄에(결정 5).
        Dictionary<string, string> CurrentCondition() =>
            _session.PrimaryLine?.condition != null ? new Dictionary<string, string>(_session.PrimaryLine.condition) : new Dictionary<string, string>();

        void OnConditionRow(int index, string flag, string expr)
        {
            var pairs = CurrentCondition().ToList();
            if (index >= pairs.Count) return;
            // 플래그만 바꾼 경우(expr null) 그 종류의 기본값.
            expr ??= _session.Flags.Find(flag)?.IsCounter == false ? "true" : ">=1";
            pairs[index] = new KeyValuePair<string, string>(flag, expr);
            ApplyCondition(pairs);
        }

        void OnConditionDelete(int index)
        {
            var pairs = CurrentCondition().ToList();
            if (index < pairs.Count) pairs.RemoveAt(index);
            ApplyCondition(pairs);
        }

        /// 아직 안 쓴 첫 플래그로 한 행 추가.
        void OnAddCondition()
        {
            var cond = CurrentCondition();
            var def = _session.Flags.flags.FirstOrDefault(f => !cond.ContainsKey(f.id));
            if (def == null) return;
            var pairs = cond.ToList();
            pairs.Add(new KeyValuePair<string, string>(def.id, def.IsCounter ? ">=1" : "true"));
            ApplyCondition(pairs);
        }

        void ApplyCondition(List<KeyValuePair<string, string>> pairs)
        {
            // 같은 플래그가 두 행이면 뒤 행이 이긴다(딕셔너리 키 하나).
            var condition = new Dictionary<string, string>();
            foreach (var kv in pairs) condition[kv.Key] = kv.Value;
            var ep = _session.Episode;
            string formatted = VnTextSyntax.FormatCondition(condition);
            if (_session.Selection.All(i => VnTextSyntax.FormatCondition(ep.lines[i].condition) == formatted)) return;
            _session.SetConditionOnSelection(condition);
        }

        void OnAddChoice()
        {
            _session.EditPrimary(l =>
            {
                l.choice ??= new List<VnChoice>();
                l.choice.Add(new VnChoice { text = "" });
            });
        }

        void OnChoiceText(int index, string value)
        {
            var line = _session.PrimaryLine;
            if (line?.choice == null || index >= line.choice.Count || line.choice[index].text == value) return;
            _session.EditPrimary(l => l.choice[index].text = value);
        }

        void OnChoiceEffects(int index, string value)
        {
            var line = _session.PrimaryLine;
            if (line?.choice == null || index >= line.choice.Count) return;
            if (!VnTextSyntax.TryParseEffects(value, out var add, out var set, out string error))
            {
                _choiceError.text = $"<color=#ff6b6b>{error}</color>";
                return;
            }
            _choiceError.text = string.Empty;
            var probe = new VnChoice { add = add, set = set };
            if (VnTextSyntax.FormatEffects(probe) == VnTextSyntax.FormatEffects(line.choice[index])) return;
            _session.EditPrimary(l =>
            {
                l.choice[index].add = add;
                l.choice[index].set = set;
            });
        }

        void OnChoiceDelete(int index)
        {
            _session.EditPrimary(l =>
            {
                l.choice.RemoveAt(index);
                if (l.choice.Count == 0) l.choice = null;
            });
        }
    }
}
