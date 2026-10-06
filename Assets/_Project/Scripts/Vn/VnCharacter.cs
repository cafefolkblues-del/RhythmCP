using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmCP.Vn
{
    /// VN 캐릭터 한 명: 이름표·표정 스프라이트·"VN만 보기" 한마디.
    /// 리듬 쪽 CharacterDefinition과 분리 — VN은 표정 시트, 리듬은 반응 상태로 축이 달라서. id만 같게 맞춘다.
    [CreateAssetMenu(menuName = "RhythmCP/VN Character", fileName = "VnChar_")]
    public class VnCharacter : ScriptableObject
    {
        [Serializable]
        public class Expression
        {
            public string id;
            public Sprite sprite;
        }

        [Tooltip("스크립트 speaker 값. yume / nemu / madoromi.")]
        [SerializeField] string _id;
        [SerializeField] string _displayName;

        [Tooltip("첫 항목 = 기본 표정. 스크립트 expr이 목록에 없으면 기본 표정으로.")]
        [SerializeField] List<Expression> _expressions = new List<Expression>();

        [Tooltip("스프라이트가 없을 때 임시 사각형 색.")]
        [SerializeField] Color _placeholderColor = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("\"VN만 보기\" 선택 시 화면 하단에 띄울 한마디 후보(무작위 1개).")]
        [SerializeField, TextArea] List<string> _metaLines = new List<string>();

        public string Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
        public Color PlaceholderColor => _placeholderColor;
        public IReadOnlyList<string> MetaLines => _metaLines;

        /// 에디터 표정 드롭다운용. 스프라이트가 비어 있어도 id만 있으면 고를 수 있다(아트 전 작성).
        public IEnumerable<string> ExpressionIds
        {
            get
            {
                foreach (var e in _expressions)
                    if (!string.IsNullOrEmpty(e.id)) yield return e.id;
            }
        }

        public Sprite GetSprite(string expr)
        {
            foreach (var e in _expressions)
                if (e.id == expr && e.sprite != null) return e.sprite;
            return _expressions.Count > 0 ? _expressions[0].sprite : null;
        }
    }
}
