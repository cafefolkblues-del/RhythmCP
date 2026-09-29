using UnityEngine;

namespace RhythmCP.Chart
{
    /// 곡 하나 = 오디오 + 난이도별 채보 JSON.
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
