using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.ChartEditing
{
    /// 타임라인 그림 공통부: 화면 이동·채보 변경 때만 메시를 다시 만든다.
    /// 격자선·노트가 수백~수천 개라 오브젝트를 하나씩 두지 않고 Graphic 하나의 메시로 그린다(드로우콜 1).
    public abstract class TimelineGraphic : MaskableGraphic
    {
        [SerializeField] protected ChartEditorSession Session;
        [SerializeField] protected TimelineView Timeline;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Timeline != null) Timeline.ViewChanged += SetVerticesDirty;
            if (Session != null) Session.Redraw += SetVerticesDirty;
        }

        protected override void OnDisable()
        {
            if (Timeline != null) Timeline.ViewChanged -= SetVerticesDirty;
            if (Session != null) Session.Redraw -= SetVerticesDirty;
            base.OnDisable();
        }

        protected bool Ready => Application.isPlaying && Session != null && Session.Document != null && Timeline != null;

        protected static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c, Vector2 uv0, Vector2 uv1)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c, new Vector2(uv0.x, uv0.y));
            vh.AddVert(new Vector3(x0, y1), c, new Vector2(uv0.x, uv1.y));
            vh.AddVert(new Vector3(x1, y1), c, new Vector2(uv1.x, uv1.y));
            vh.AddVert(new Vector3(x1, y0), c, new Vector2(uv1.x, uv0.y));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        /// 단색 사각형(텍스처 중앙 한 점의 UV — 원 텍스처를 써도 꽉 찬 사각형이 된다).
        protected static void Rect(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c) =>
            Quad(vh, x0, y0, x1, y1, c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        protected static void Circle(VertexHelper vh, float x, float y, float r, Color32 c) =>
            Quad(vh, x - r, y - r, x + r, y + r, c, Vector2.zero, Vector2.one);
    }
}
