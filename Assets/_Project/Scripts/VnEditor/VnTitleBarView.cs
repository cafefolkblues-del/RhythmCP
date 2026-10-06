using System.Collections.Generic;
using System.Linq;
using RhythmCP.Vn;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.VnEditing
{
    /// 상단 바(와이어프레임 02): 파일 드롭다운 · route 탭 · edition 탭 · 새 에피소드 · 프리뷰 · 저장 · 상태.
    /// 탭 버튼은 템플릿을 복제해 만든다(라우트 수 = 카탈로그 캐릭터 수 + 공통).
    public class VnTitleBarView : MonoBehaviour
    {
        [SerializeField] VnEditorSession _session;
        [SerializeField] TMP_Dropdown _file;

        [Tooltip("route 탭 버튼 템플릿(비활성으로 둬도 됨). 자식 TMP_Text에 라우트 이름.")]
        [SerializeField] Button _routeTemplate;
        [SerializeField] Button _base;
        [SerializeField] Button _adult;
        [SerializeField] Button _new;
        [SerializeField] Button _preview;
        [SerializeField] Button _save;
        [SerializeField] TMP_Text _status;

        [SerializeField] Color _tabOn = new Color(1f, 0.85f, 0.35f);
        [SerializeField] Color _tabOff = new Color(1f, 1f, 1f, 0.6f);

        readonly Dictionary<string, Button> _routes = new Dictionary<string, Button>();
        readonly List<VnEpisodeEntry> _fileEntries = new List<VnEpisodeEntry>();

        void Start()
        {
            _routeTemplate.gameObject.SetActive(false);
            foreach (var route in _session.Routes)
            {
                var b = Instantiate(_routeTemplate, _routeTemplate.transform.parent);
                b.gameObject.SetActive(true);
                b.GetComponentInChildren<TMP_Text>().text = route;
                string r = route;
                b.onClick.AddListener(() => _session.SetRoute(r));
                _routes[route] = b;
            }

            _base.onClick.AddListener(() => _session.SetEdition(VnIds.Base));
            _adult.onClick.AddListener(() => _session.SetEdition(VnIds.Adult));
            _new.onClick.AddListener(_session.NewEpisode);
            _preview.onClick.AddListener(_session.StartPreview);
            _save.onClick.AddListener(() => { _session.Save(); _session.Message("저장됨"); });
            _file.onValueChanged.AddListener(OnFilePicked);

            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy() => _session.StateChanged -= Refresh;

        void Refresh()
        {
            foreach (var kv in _routes) Tint(kv.Value, kv.Key == _session.Route);
            Tint(_base, _session.Edition == VnIds.Base);
            Tint(_adult, _session.Edition == VnIds.Adult);

            _fileEntries.Clear();
            _fileEntries.AddRange(_session.TabEpisodes);
            _file.ClearOptions();
            _file.AddOptions(_fileEntries.Count == 0 ? new List<string> { "(에피소드 없음 — 새로 만들기)" } : _fileEntries.Select(e => e.Id + ".json").ToList());
            int current = _fileEntries.FindIndex(e => e == _session.Entry);
            _file.SetValueWithoutNotify(Mathf.Max(0, current));
            _file.interactable = _fileEntries.Count > 0;

            var doc = _session.Document;
            string save = doc == null ? "" : doc.IsDirty || _session.FlagsDirty ? "저장 대기" : "저장됨";
            string lines = doc == null ? "" : $"{doc.Episode.lines.Count}줄";
            _status.text = $"{lines}   {save}   {_session.LastMessage}";
        }

        void OnFilePicked(int index)
        {
            if (index >= 0 && index < _fileEntries.Count) _session.Open(_fileEntries[index]);
        }

        void Tint(Button b, bool on)
        {
            if (b.targetGraphic != null) b.targetGraphic.color = on ? _tabOn : _tabOff;
        }
    }
}
