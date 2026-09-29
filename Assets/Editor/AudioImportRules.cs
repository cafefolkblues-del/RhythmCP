using UnityEditor;
using UnityEngine;

namespace RhythmCP.EditorTools
{
    /// 폴더에 넣기만 하면 오디오 임포트 설정이 잡히게 하는 규칙.
    ///
    /// - Audio/Music : Compressed In Memory + Vorbis.
    ///   Streaming 은 메모리는 아끼지만 시작/탐색 지연이 들쭉날쭉해서 박자 기준이 흔들린다.
    ///   곡 하나 수 MB 수준이라 메모리에 두는 쪽을 택했다.
    /// - Audio/SFX   : Decompress On Load + PCM.
    ///   히트음은 디코딩 지연이 곧 체감 판정 오차라 아예 풀어 둔다.
    ///
    /// 폴더 규칙이 곧 설정이다 — 개별 파일을 인스펙터에서 바꿔도 재임포트 때 되돌아간다.
    /// 예외가 필요하면 이 폴더 밖에 두면 된다.
    public class AudioImportRules : AssetPostprocessor
    {
        public const string MusicFolder = "Assets/_Project/Audio/Music/";
        public const string SfxFolder = "Assets/_Project/Audio/SFX/";

        void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;

            if (assetPath.StartsWith(MusicFolder)) ApplyMusic(importer);
            else if (assetPath.StartsWith(SfxFolder)) ApplySfx(importer);
        }

        static void ApplyMusic(AudioImporter importer)
        {
            importer.forceToMono = false;
            importer.loadInBackground = true;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }

        static void ApplySfx(AudioImporter importer)
        {
            importer.loadInBackground = false;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("Tools/RhythmCP/오디오 임포트 규칙 다시 적용")]
        static void ReapplyAll()
        {
            int count = 0;
            foreach (string folder in new[] { MusicFolder.TrimEnd('/'), SfxFolder.TrimEnd('/') })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }

            Debug.Log($"[AudioImportRules] 오디오 {count}개 재임포트");
        }
    }
}
