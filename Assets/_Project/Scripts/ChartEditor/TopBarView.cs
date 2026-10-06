using System.Collections.Generic;
using System.Linq;
using RhythmCP.Chart;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 상단 바: 곡 · 난이도 · 스냅 · 도구 · 빠른 노트 · 재생 · 녹음 · 테스트 플레이.
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] ChartEditorSession _session;
        [SerializeField] TMP_Dropdown _song;
        [SerializeField] Button _easy;
        [SerializeField] Button _hard;
        [SerializeField] TMP_Dropdown _snap;
        [SerializeField] Button _tap;
        [SerializeField] Button _hold;
        [SerializeField] Button _heart;
        [SerializeField] Button _mash;
        [SerializeField] Button _fast;
        [SerializeField] Button _play;
        [SerializeField] Button _record;
        [SerializeField] Button _testPlay;

        [SerializeField] Color _on = new Color(0.9f, 0.3f, 0.5f);
        [SerializeField] Color _off = new Color(1, 1, 1, 0.14f);

        bool _refreshing;

        void Start()
        {
            _snap.ClearOptions();
            _snap.AddOptions(BeatGrid.Divisions.Select(d => $"1/{d * 4}").ToList());
            _snap.onValueChanged.AddListener(i => { if (!_refreshing) _session.SetSnap(BeatGrid.Divisions[i]); });
            _song.onValueChanged.AddListener(i => { if (!_refreshing) _session.Open(_session.Songs[i], _session.Difficulty); });

            _easy.onClick.AddListener(() => _session.Open(_session.Song, Difficulty.Easy));
            _hard.onClick.AddListener(() => _session.Open(_session.Song, Difficulty.Hard));
            _tap.onClick.AddListener(() => _session.SetTool(EditTool.Tap));
            _hold.onClick.AddListener(() => _session.SetTool(EditTool.Hold));
            _heart.onClick.AddListener(() => _session.SetTool(EditTool.Heart));
            _mash.onClick.AddListener(() => _session.SetTool(EditTool.Mash));
            _fast.onClick.AddListener(_session.ToggleFast);
            _play.onClick.AddListener(_session.TogglePlay);
            _record.onClick.AddListener(_session.ToggleRecord);
            _testPlay.onClick.AddListener(_session.TestPlay);

            _session.StateChanged += Refresh;
            Refresh();
        }

        void OnDestroy() => _session.StateChanged -= Refresh;

        void Refresh()
        {
            _refreshing = true;
            _song.ClearOptions();
            _song.AddOptions(_session.Songs.Select(s => string.IsNullOrEmpty(s.Title) ? s.name : s.Title).ToList());
            _song.SetValueWithoutNotify(Mathf.Max(0, _session.Songs.IndexOf(_session.Song)));
            _snap.SetValueWithoutNotify(System.Array.IndexOf(BeatGrid.Divisions, _session.SnapDivision));
            _refreshing = false;

            Paint(_easy, _session.Difficulty == Difficulty.Easy);
            Paint(_hard, _session.Difficulty == Difficulty.Hard);
            Paint(_tap, _session.Tool == EditTool.Tap);
            Paint(_hold, _session.Tool == EditTool.Hold);
            Paint(_heart, _session.Tool == EditTool.Heart);
            Paint(_mash, _session.Tool == EditTool.Mash);
            Paint(_fast, _session.FastNotes);
            Paint(_play, _session.Playback.IsPlaying && !_session.IsRecording);
            Paint(_record, _session.IsRecording);
        }

        void Paint(Button b, bool on) => b.targetGraphic.color = on ? _on : _off;
    }
}
