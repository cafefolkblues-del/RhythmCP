using System.IO;
using RhythmCP.Chart;
using RhythmCP.ChartEditing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmCP.EditorTools
{
    /// 채보 에디터 실행: 파형 캐시를 만들고 → ChartEditor 씬을 열고 → 플레이 모드 진입.
    /// 채보 에디터는 플레이 모드에서 도는 저작 도구(게임과 같은 오디오 시계·입력 경로를 쓰려고, 2026-10-06 결정).
    public static class ChartEditorLauncher
    {
        const string ScenePath = "Assets/_Project/Scenes/ChartEditor.unity";
        const string TempFolder = "Assets/__WaveformTemp";

        [MenuItem("Tools/RhythmCP/채보 에디터")]
        static void Open()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildWaveforms();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/RhythmCP/파형 캐시 다시 만들기")]
        static void RebuildWaveforms()
        {
            if (Directory.Exists(WaveformData.CacheDir)) Directory.Delete(WaveformData.CacheDir, true);
            BuildWaveforms();
        }

        /// 곡은 압축 상태로 메모리에 올라가 GetData가 실패한다. 원본 임포트 설정은 폴더 규칙(AudioImportRules)이
        /// 재임포트마다 되돌리므로 건드리지 않고, 규칙 밖 임시 폴더에 복사본을 풀어서 읽은 뒤 지운다.
        static void BuildWaveforms()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SongDefinition"))
            {
                var song = AssetDatabase.LoadAssetAtPath<SongDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (song == null || song.Clip == null) continue;

                string audioPath = AssetDatabase.GetAssetPath(song.Clip);
                string cache = WaveformData.CachePath(AssetDatabase.AssetPathToGUID(audioPath));
                if (File.Exists(cache) && File.GetLastWriteTimeUtc(cache) >= File.GetLastWriteTimeUtc(audioPath)) continue;

                EditorUtility.DisplayProgressBar("채보 에디터", $"파형 계산: {song.Title}", 0.5f);
                try { Build(audioPath, cache); }
                finally { EditorUtility.ClearProgressBar(); }
            }
        }

        static void Build(string audioPath, string cache)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "__WaveformTemp");
            string temp = $"{TempFolder}/{Path.GetFileName(audioPath)}";
            try
            {
                AssetDatabase.CopyAsset(audioPath, temp);
                var importer = (AudioImporter)AssetImporter.GetAtPath(temp);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                // 원본의 백그라운드 로딩 설정까지 복사되므로 끈다 — 켜져 있으면 아직 안 올라온 상태로 GetData가 실패한다.
                importer.loadInBackground = false;
                importer.SaveAndReimport();

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(temp);
                clip.LoadAudioData();
                var samples = new float[clip.samples * clip.channels];
                if (clip.GetData(samples, 0))
                    WaveformData.FromSamples(samples, clip.channels, clip.frequency).Write(cache);
                else
                    Debug.LogWarning($"[ChartEditorLauncher] 파형 계산 실패: {audioPath}");
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }
    }
}
