using UnityEngine;

namespace RhythmCP.Chart
{
    /// 곡 하나 = 오디오 + 난이도별 채보 JSON.
    /// TODO(에셋): 곡별 배경 슬롯(640×360 1장) 추가 예정. 여러 겹 패럴랙스 배경은 나중에(2026-09-30).
    [CreateAssetMenu(menuName = "RhythmCP/Song Definition", fileName = "Song_")]
    public class SongDefinition : ScriptableObject
    {
        [SerializeField] string _songId;
        [SerializeField] string _title;
        [SerializeField] AudioClip _clip;
        [SerializeField] TextAsset _easyChart;
        [SerializeField] TextAsset _hardChart;

        public string SongId => _songId;
        public string Title => _title;
        public AudioClip Clip => _clip;

        public TextAsset GetChart(Difficulty difficulty) => difficulty == Difficulty.Hard ? _hardChart : _easyChart;
    }
}
