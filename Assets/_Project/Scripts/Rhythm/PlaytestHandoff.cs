using RhythmCP.Chart;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmCP.Rhythm
{
    /// 채보 에디터 → 플레이 씬 → 에디터 왕복용 전달 상자(에디터 전용 흐름).
    /// 허브(M4)가 생기면 곡 선택 전달은 GameSession 싱글톤이 맡고, 이건 테스트 플레이 표식으로만 남는다.
    public static class PlaytestHandoff
    {
        public static bool Active { get; private set; }
        public static SongDefinition Song { get; private set; }
        public static Difficulty Difficulty { get; private set; }

        /// 에디터가 다시 열릴 때 같은 곡·난이도로 돌아오도록 남겨 두는 마지막 값(Active와 별개).
        public static SongDefinition LastSong { get; private set; }
        public static Difficulty LastDifficulty { get; private set; }

        static string _returnScenePath;

        public static void Begin(SongDefinition song, Difficulty difficulty, string returnScenePath)
        {
            Active = true;
            Song = LastSong = song;
            Difficulty = LastDifficulty = difficulty;
            _returnScenePath = returnScenePath;
        }

        public static void ReturnToEditor()
        {
            Active = false;
            Time.timeScale = 1f;
#if UNITY_EDITOR
            // 에디터 씬은 Build Settings에 없어서 이름 로드가 안 된다 — 에디터 전용 경로 로드를 쓴다.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(_returnScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogWarning("[PlaytestHandoff] 채보 에디터는 에디터 전용");
#endif
        }
    }
}
