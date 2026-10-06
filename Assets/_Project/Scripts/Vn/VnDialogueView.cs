using TMPro;
using UnityEngine;

namespace RhythmCP.Vn
{
    /// 하단 대사창. 캐릭터 = 이름표 + 기본 상자, mc(비가시 남주) = 호칭 이름표 + 점선 상자·초상화 없음, 나레이션 = 이름표 없음.
    /// 타자기는 TMP maxVisibleCharacters로 — 글자를 잘라 넣으면 리치 텍스트 태그가 깨지고 줄바꿈 위치가 흔들려서.
    public class VnDialogueView : MonoBehaviour
    {
        [Tooltip("캐릭터·나레이션 대사 상자 배경.")]
        [SerializeField] GameObject _characterFrame;

        [Tooltip("mc 대사 상자 배경(점선 테두리).")]
        [SerializeField] GameObject _mcFrame;

        [SerializeField] GameObject _nameplate;
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_Text _body;

        [Tooltip("다 찍히면 깜빡이는 ▼. 선택지 라인에서는 숨긴다.")]
        [SerializeField] GameObject _nextIndicator;

        float _cps;
        float _visible;
        int _total;
        bool _choiceLine;

        public bool IsTyping => _visible < _total;

        public void Show(VnShownLine line, float charsPerSec)
        {
            bool mc = line.Kind == VnSpeakerKind.Mc;
            _characterFrame.SetActive(!mc);
            _mcFrame.SetActive(mc);
            _nameplate.SetActive(line.Kind != VnSpeakerKind.Narration);
            _name.text = line.Name ?? string.Empty;

            _body.text = line.Line.text ?? string.Empty;
            _body.ForceMeshUpdate();
            _total = _body.textInfo.characterCount;
            _cps = charsPerSec;
            _visible = charsPerSec > 0f ? 0f : _total;
            _choiceLine = line.Line.IsChoice;
            Refresh();
        }

        /// 찍는 중에 진행 입력이 오면 나머지를 한 번에.
        public void Complete()
        {
            _visible = _total;
            Refresh();
        }

        void Update()
        {
            if (!IsTyping) return;
            _visible = Mathf.Min(_total, _visible + _cps * Time.unscaledDeltaTime);
            Refresh();
        }

        void Refresh()
        {
            _body.maxVisibleCharacters = Mathf.FloorToInt(_visible);
            if (_nextIndicator != null) _nextIndicator.SetActive(!IsTyping && !_choiceLine);
        }
    }
}
