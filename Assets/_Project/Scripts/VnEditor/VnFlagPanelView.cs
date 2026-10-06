using System.Collections.Generic;
using System.Linq;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 오른쪽 패널(와이어프레임 02 ⑫): 전역 플래그 등록부(flags.json) + 미리보기 값, 이 에피소드 출연진 호칭, 메타.
    /// 미리보기 값은 파일에 저장하지 않는다 — 라인 목록의 "가려짐" 표시와 미리보기 시작 값에만 쓴다.
    /// 등록부 수정은 되돌리기 대상이 아니다(파일이 다르고 자주 바꾸지 않아서). 지우면 그 플래그를 쓰는 줄이 검사기 오류로 뜬다.
    public class VnFlagPanelView : MonoBehaviour
    {
        [SerializeField] VnEditorSession _session;

        [Header("플래그")]
        [SerializeField] VnFlagRowView _flagTemplate;

        [Tooltip("새 플래그 입력 줄(id·종류·설명·추가 버튼)은 한 부모 아래에, 그 부모는 플래그 행들과 같은 부모 아래에 — 목록 맨 아래로 옮긴다.")]
        [SerializeField] TMP_InputField _newId;
        [SerializeField] TMP_Dropdown _newKind;
        [SerializeField] TMP_InputField _newDescription;
        [SerializeField] Button _addFlag;

        [Header("출연진·호칭")]
        [SerializeField] VnCastRowView _castTemplate;

        [Tooltip("추가 드롭다운·버튼은 한 부모 아래에, 그 부모는 출연진 행들과 같은 부모 아래에.")]
        [SerializeField] TMP_Dropdown _addCastPick;
        [SerializeField] Button _addCast;

        [Header("메타")]
        [Tooltip("id · route · epIndex · edition (라우트는 플래그가 아니라 리듬 카운트 — M1/M5).")]
        [SerializeField] TMP_Text _meta;

        static readonly string[] Kinds = { VnFlagDef.Counter, VnFlagDef.Bool };

        readonly List<VnFlagRowView> _flagRows = new List<VnFlagRowView>();
        readonly List<VnCastRowView> _castRows = new List<VnCastRowView>();
        readonly List<string> _castCandidates = new List<string>();

        void Start()
        {
            _flagTemplate.gameObject.SetActive(false);
            _castTemplate.gameObject.SetActive(false);
            _newKind.ClearOptions();
            _newKind.AddOptions(new List<string> { "누적 (counter)", "단일 (bool)" });
            _addFlag.onClick.AddListener(OnAddFlag);
            _addCast.onClick.AddListener(OnAddCast);

            _session.Redraw += Refresh;
            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            _session.Redraw -= Refresh;
            _session.StateChanged -= Refresh;
        }

        void Refresh()
        {
            RefreshFlags();
            RefreshCast();
            var ep = _session.Episode;
            _meta.text = ep == null ? "" : $"{ep.meta.id}  ·  route {ep.meta.route}  ·  ep {ep.meta.epIndex}  ·  {ep.meta.edition}";
        }

        void RefreshFlags()
        {
            // 누적 먼저, 그 안에서 id 순 — 와이어프레임처럼 누적/단일 묶음으로 보이게.
            var flags = _session.Flags?.flags.OrderBy(f => f.IsCounter ? 0 : 1).ThenBy(f => f.id, System.StringComparer.Ordinal).ToList()
                        ?? new List<VnFlagDef>();
            while (_flagRows.Count < flags.Count)
            {
                var row = Instantiate(_flagTemplate, _flagTemplate.transform.parent);
                row.DescriptionEdited += _session.SetFlagDescription;
                row.CounterEdited += _session.SetPreviewCounter;
                row.BoolEdited += _session.SetPreviewBool;
                row.DeleteClicked += _session.RemoveFlag;
                _flagRows.Add(row);
            }
            var preview = _session.PreviewFlags;
            for (int i = 0; i < _flagRows.Count; i++)
            {
                bool used = i < flags.Count;
                _flagRows[i].gameObject.SetActive(used);
                if (used)
                {
                    var f = flags[i];
                    _flagRows[i].Set(f.id, f.IsCounter, f.description, preview.Counter(f.id), preview.Bool(f.id));
                }
            }
            // 추가 줄을 목록 맨 아래로(행이 템플릿 옆에 복제되므로).
            _newId.transform.parent.SetAsLastSibling();
        }

        void RefreshCast()
        {
            var ep = _session.Episode;
            var cast = ep?.cast ?? new List<VnCastEntry>();
            while (_castRows.Count < cast.Count)
            {
                var row = Instantiate(_castTemplate, _castTemplate.transform.parent);
                row.HonorificEdited += OnHonorific;
                row.RemoveClicked += OnRemoveCast;
                _castRows.Add(row);
            }
            for (int i = 0; i < _castRows.Count; i++)
            {
                bool used = i < cast.Count;
                _castRows[i].gameObject.SetActive(used);
                if (used) _castRows[i].Set(cast[i].id, _session.Catalog.DisplayName(cast[i].id), cast[i].honorific);
            }

            _castCandidates.Clear();
            _castCandidates.AddRange(_session.Catalog.Characters.Where(c => c != null && cast.All(e => e.id != c.Id)).Select(c => c.Id));
            _addCastPick.ClearOptions();
            _addCastPick.AddOptions(_castCandidates.Count == 0 ? new List<string> { "(전원 출연 중)" } : _castCandidates.Select(_session.Catalog.DisplayName).ToList());
            _addCastPick.interactable = _addCast.interactable = ep != null && _castCandidates.Count > 0;
            _addCast.transform.parent.SetAsLastSibling();
        }

        void OnAddFlag()
        {
            if (_session.AddFlag(_newId.text, Kinds[_newKind.value], string.IsNullOrWhiteSpace(_newDescription.text) ? null : _newDescription.text.Trim()))
            {
                _newId.SetTextWithoutNotify(string.Empty);
                _newDescription.SetTextWithoutNotify(string.Empty);
            }
        }

        void OnHonorific(string id, string value)
        {
            var entry = _session.Episode?.cast.Find(c => c.id == id);
            if (entry == null || entry.honorific == value) return;
            _session.EditEpisode(ep => ep.cast.Find(c => c.id == id).honorific = value);
        }

        void OnRemoveCast(string id) => _session.EditEpisode(ep => ep.cast.RemoveAll(c => c.id == id));

        void OnAddCast()
        {
            if (_castCandidates.Count == 0) return;
            string id = _castCandidates[Mathf.Clamp(_addCastPick.value, 0, _castCandidates.Count - 1)];
            _session.EditEpisode(ep => ep.cast.Add(new VnCastEntry { id = id, honorific = "" }));
        }
    }
}
