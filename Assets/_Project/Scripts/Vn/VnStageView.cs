using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmCP.Vn
{
    /// 무대(배경·CG·캐릭터) 표시. VnPlayer가 계산한 VnStageState와 지금 화면을 비교해 달라진 것만 연출한다.
    /// 비교 방식이라 처음 시작·중간 시작·세이브 불러오기가 같은 경로로 그려진다(instant면 연출 없이 즉시).
    public class VnStageView : MonoBehaviour
    {
        [SerializeField] VnCatalog _catalog;
        [SerializeField] VnConfig _config;

        [Header("배경 — 뒤/앞 두 장으로 크로스페이드")]
        [SerializeField] Image _bgBack;
        [SerializeField] Image _bgFront;

        [Tooltip("배경 아트가 없을 때 id를 띄우는 글자.")]
        [SerializeField] TMP_Text _bgLabel;

        [Header("CG — 캐릭터 위, 대사창 아래")]
        [SerializeField] Image _cg;
        [SerializeField] TMP_Text _cgLabel;

        [Header("캐릭터")]
        [SerializeField] VnActorView _actorPrefab;
        [SerializeField] RectTransform _actorRoot;

        [Tooltip("left / center / right 기준점. 캐릭터는 이 위치의 anchoredPosition으로 간다.")]
        [SerializeField] RectTransform _left;
        [SerializeField] RectTransform _center;
        [SerializeField] RectTransform _right;

        readonly Dictionary<string, VnActorView> _actors = new Dictionary<string, VnActorView>();
        VnColorTween _bgTween, _cgTween;
        string _bgShown, _cgShown;
        bool _bgShownOnce;

        void Awake()
        {
            _bgTween = new VnColorTween(_bgFront);
            _cgTween = new VnColorTween(_cg);
            _bgBack.color = Color.black;
            _bgFront.color = Color.clear;
            _cg.color = Color.clear;
            SetLabel(_bgLabel, null);
            SetLabel(_cgLabel, null);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _bgTween.Tick(dt);
            _cgTween.Tick(dt);
        }

        /// shown = 지금 띄우는 라인(화자 기준 어둡게 처리·배경 전환 방식). instant = 연출 생략(첫 표시·스킵).
        public void Show(VnStageState stage, VnShownLine shown, bool instant)
        {
            ShowBackground(stage.bg, instant ? VnIds.Transitions[0] : shown?.BgTransition);
            ShowCg(stage.cg, instant);
            ShowActors(stage, shown, instant);
        }

        void ShowBackground(string id, string transition)
        {
            if (_bgShownOnce && id == _bgShown) return;
            _bgShown = id;
            _bgShownOnce = true;

            var sprite = _catalog.Background(id);
            Color color = id == null ? Color.black : sprite != null ? Color.white : PlaceholderGray(id);
            SetLabel(_bgLabel, id != null && sprite == null ? "bg: " + id : null);

            // 페이드: 지금 앞장을 뒷장으로 옮기고 새 배경을 앞장에서 투명 → 불투명.
            if (transition == "fade")
            {
                _bgBack.sprite = _bgFront.sprite;
                _bgBack.color = _bgFront.color.a > 0f ? _bgFront.color : _bgBack.color;
                _bgFront.sprite = sprite;
                _bgFront.color = new Color(color.r, color.g, color.b, 0f);
                _bgTween.To(color, _config.BgFadeSec);
            }
            else
            {
                _bgFront.sprite = sprite;
                _bgTween.To(color, 0f);
            }
        }

        void ShowCg(string id, bool instant)
        {
            if (id == _cgShown) return;
            _cgShown = id;
            float dur = instant ? 0f : _config.CgFadeSec;

            if (id == null)
            {
                SetLabel(_cgLabel, null);
                _cgTween.To(Color.clear, dur);
                return;
            }

            var sprite = _catalog.Cg(id);
            _cg.sprite = sprite;
            var color = sprite != null ? Color.white : PlaceholderGray(id);
            _cg.color = new Color(color.r, color.g, color.b, 0f);
            SetLabel(_cgLabel, sprite == null ? "cg: " + id : null);
            _cgTween.To(color, dur);
        }

        void ShowActors(VnStageState stage, VnShownLine shown, bool instant)
        {
            // 퇴장: 무대 상태에 없는데 화면에 남은 캐릭터.
            var gone = new List<string>();
            foreach (var kv in _actors)
                if (stage.Actor(kv.Key) == null) gone.Add(kv.Key);
            foreach (var id in gone)
            {
                _actors[id].Exit(instant ? 0f : _config.ExitFadeSec);
                _actors.Remove(id);
            }

            foreach (var actor in stage.actors)
            {
                if (!_actors.TryGetValue(actor.id, out var view))
                {
                    view = Instantiate(_actorPrefab, _actorRoot);
                    view.Setup(actor.id, _catalog.Character(actor.id));
                    _actors.Add(actor.id, view);
                }
                view.SetExpression(actor.expr);
                var anchor = Anchor(actor.pos);
                if (anchor != null) view.Rect.anchoredPosition = anchor.anchoredPosition;
                view.SetTint(TintFor(actor.id, shown), instant ? 0f : _config.ActorFadeSec);
            }
        }

        /// 캐릭터 대사 = 화자만 밝게. 나레이션 = 전원 밝게. mc = 전원 어둡게(남주가 말하는 동안 모두 듣는 쪽).
        Color TintFor(string id, VnShownLine shown)
        {
            if (shown == null || shown.Kind == VnSpeakerKind.Narration) return Color.white;
            if (shown.Kind == VnSpeakerKind.Mc) return _config.DimTint;
            return shown.SpeakerId == id ? Color.white : _config.DimTint;
        }

        RectTransform Anchor(string pos) => pos switch
        {
            "left" => _left,
            "center" => _center,
            "right" => _right,
            _ => null,
        };

        /// 아트 없는 배경·CG는 id마다 다른 회색으로 — 전환이 일어났는지 눈으로 구분되게.
        static Color PlaceholderGray(string id)
        {
            // string.GetHashCode는 런타임마다 달라질 수 있어 글자 합으로 고정.
            int h = 0;
            foreach (char c in id) h = h * 31 + c;
            float v = 0.25f + (Mathf.Abs(h) % 1000) / 1000f * 0.5f;
            return new Color(v, v, v, 1f);
        }

        static void SetLabel(TMP_Text label, string text)
        {
            if (label == null) return;
            label.text = text ?? string.Empty;
        }
    }
}
