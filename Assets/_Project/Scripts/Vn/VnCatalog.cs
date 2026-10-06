using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmCP.Vn
{
    /// 스크립트 id(문자열) → 에셋. 스크립트는 에셋을 직접 참조하지 않고 id만 쓴다(JSON으로 읽고 쓰기 위해).
    /// 없는 id는 null — 뷰가 임시 표시(색 사각형 + id 글자)로 대신한다. 오타는 에디터 검사기가 잡는다.
    [CreateAssetMenu(menuName = "RhythmCP/VN Catalog", fileName = "VnCatalog")]
    public class VnCatalog : ScriptableObject
    {
        [Serializable]
        public class NamedSprite
        {
            public string id;
            public Sprite sprite;
        }

        [Serializable]
        public class NamedClip
        {
            public string id;
            public AudioClip clip;
        }

        [SerializeField] List<VnCharacter> _characters = new List<VnCharacter>();
        [SerializeField] List<NamedSprite> _backgrounds = new List<NamedSprite>();
        [SerializeField] List<NamedSprite> _cgs = new List<NamedSprite>();
        [SerializeField] List<NamedClip> _bgms = new List<NamedClip>();

        [Tooltip("에피소드 JSON들. 파일 이름 = meta.id. 세이브 불러오기가 다른 에피소드를 찾을 때 쓴다.")]
        [SerializeField] List<TextAsset> _episodes = new List<TextAsset>();

        public IReadOnlyList<VnCharacter> Characters => _characters;
        public IReadOnlyList<NamedSprite> Backgrounds => _backgrounds;
        public IReadOnlyList<NamedSprite> Cgs => _cgs;
        public IReadOnlyList<NamedClip> Bgms => _bgms;

        public VnCharacter Character(string id) => _characters.Find(c => c != null && c.Id == id);

        /// VnPlayer 이름표용. 카탈로그에 없으면 id 그대로(임시 캐릭터도 진행은 되게).
        public string DisplayName(string id) => Character(id)?.DisplayName ?? id;

        public TextAsset Episode(string id) => _episodes.Find(e => e != null && e.name == id);

        public Sprite Background(string id) => Find(_backgrounds, id);
        public Sprite Cg(string id) => Find(_cgs, id);

        public AudioClip Bgm(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _bgms.Find(b => b.id == id)?.clip;
        }

        static Sprite Find(List<NamedSprite> list, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return list.Find(s => s.id == id)?.sprite;
        }
    }
}
