using System.Collections.Generic;
using RhythmCP.Chart;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 타임라인 위 눈금 띠: 마디 번호, BPM 변경점, 클라이맥스 구간, A-B 반복 구간.
    /// 가로 좌표는 레인 영역과 같다(같은 가로 앵커·피벗으로 배치해 TimeToX를 그대로 쓴다).
    public class RulerView : TimelineGraphic
    {
        [SerializeField] Sprite _white;
        [SerializeField] TMP_Text _labelTemplate;
        [SerializeField] Color _tick = new Color(1, 1, 1, 0.5f);
        [SerializeField] Color _climax = new Color(0.9f, 0.3f, 0.5f, 0.45f);
        [SerializeField] Color _loop = new Color(0.35f, 0.6f, 0.9f, 0.4f);
        [SerializeField] Color _bpmMark = new Color(1f, 0.8f, 0.3f, 0.9f);

        readonly List<TMP_Text> _labels = new List<TMP_Text>();
        bool _labelsDirty;

        public override Texture mainTexture => _white != null ? _white.texture : s_WhiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Timeline != null) Timeline.ViewChanged += MarkLabels;
            if (Session != null) Session.Redraw += MarkLabels;
        }

        protected override void OnDisable()
        {
            if (Timeline != null) Timeline.ViewChanged -= MarkLabels;
            if (Session != null) Session.Redraw -= MarkLabels;
            base.OnDisable();
        }

        void MarkLabels() => _labelsDirty = true;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Ready) return;
            var r = rectTransform.rect;
            var data = Session.Document.Data;
            var tempo = Session.Document.Tempo;

            if (data.climax != null)
                Rect(vh, Timeline.TimeToX(tempo.BeatToSec(data.climax.startBeat)), r.yMin, Timeline.TimeToX(tempo.BeatToSec(data.climax.endBeat)), r.center.y, _climax);

            var pb = Session.Playback;
            if (pb.LoopEnabled)
                Rect(vh, Timeline.TimeToX(pb.LoopStartSec), r.center.y, Timeline.TimeToX(pb.LoopEndSec), r.yMax, _loop);

            foreach (var line in BeatGrid.Lines(tempo, Timeline.ViewStartSec, Timeline.ViewEndSec, 1))
            {
                float x = Timeline.TimeToX(line.Time);
                float top = line.Kind == GridLineKind.Bar ? r.yMax : r.yMin + r.height * 0.3f;
                Rect(vh, x - 0.75f, r.yMin, x + 0.75f, top, _tick);
            }

            foreach (var b in data.bpms)
            {
                float x = Timeline.TimeToX(tempo.BeatToSec(b.beat));
                Rect(vh, x - 2f, r.yMin, x + 2f, r.yMax, _bpmMark);
            }
        }

        void LateUpdate()
        {
            if (!_labelsDirty || !Ready) return;
            _labelsDirty = false;
            int used = 0;
            var tempo = Session.Document.Tempo;

            foreach (var line in BeatGrid.Lines(tempo, Timeline.ViewStartSec, Timeline.ViewEndSec, 1))
                if (line.Kind == GridLineKind.Bar)
                    Label(ref used, Timeline.TimeToX(line.Time) + 4f, -2f, ((long)(line.Beat / BeatGrid.BeatsPerBar) + 1).ToString(), Color.white);

            foreach (var b in Session.Document.Data.bpms)
                // 마디 번호와 같은 자리(0박 등)에 겹치지 않게 BPM은 아래 칸.
                Label(ref used, Timeline.TimeToX(tempo.BeatToSec(b.beat)) + 4f, -26f, $"BPM {b.bpm:0.##}", _bpmMark);

            for (int i = used; i < _labels.Count; i++) _labels[i].gameObject.SetActive(false);
        }

        void Label(ref int used, float x, float y, string text, Color color)
        {
            var r = rectTransform.rect;
            if (x < r.xMin - 50 || x > r.xMax) return;
            if (used >= _labels.Count)
            {
                var t = Instantiate(_labelTemplate, _labelTemplate.transform.parent);
                _labels.Add(t);
            }
            var label = _labels[used++];
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            label.rectTransform.anchoredPosition = new Vector2(x, y);
        }
    }
}
