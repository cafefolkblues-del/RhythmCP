using System;
using System.IO;
using RhythmCP.Chart;
using UnityEngine;

namespace RhythmCP.ChartEditing
{
    /// 채보 JSON 파일 읽기/쓰기 + 백업 + 외부 변경 감지(Claude가 파일을 직접 고치는 HARD 공동 작업용).
    /// 에셋 갱신·SongDefinition 칸 연결은 에디터 전용이라 UNITY_EDITOR로 감싼다(채보 에디터 자체가 에디터 전용 도구).
    public class ChartFileStore
    {
        public string AssetPath { get; }
        readonly string _fullPath;
        DateTime _lastKnownWrite;

        public bool Exists => File.Exists(_fullPath);

        ChartFileStore(string assetPath)
        {
            AssetPath = assetPath;
            _fullPath = Path.GetFullPath(assetPath);
        }

        /// SongDefinition에 채보가 연결돼 있으면 그 파일, 없으면 Charts/{songId}_{easy|hard}.json(저장할 때 연결).
        public static ChartFileStore For(SongDefinition song, Difficulty difficulty)
        {
            string path = null;
#if UNITY_EDITOR
            var asset = song.GetChart(difficulty);
            if (asset != null) path = UnityEditor.AssetDatabase.GetAssetPath(asset);
#endif
            path ??= $"{ChartFolderFor(song)}/{song.SongId}_{difficulty.ToString().ToLowerInvariant()}.json";
            return new ChartFileStore(path);
        }

        /// 곡 에셋이 _Local 폴더(로컬 전용 테스트 곡, git 제외)에 있으면 채보도 Charts/_Local로 — 따로 gitignore를 안 만져도 되게.
        static string ChartFolderFor(SongDefinition song)
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.GetAssetPath(song).Contains("/_Local/")) return "Assets/_Project/Charts/_Local";
#endif
            return "Assets/_Project/Charts";
        }

        public ChartData Load()
        {
            var data = ChartSerializer.FromJson(File.ReadAllText(_fullPath));
            _lastKnownWrite = File.GetLastWriteTimeUtc(_fullPath);
            return data;
        }

        public void Save(ChartData data, SongDefinition song, Difficulty difficulty)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_fullPath));
            File.WriteAllText(_fullPath, ChartSerializer.ToJson(data));
            _lastKnownWrite = File.GetLastWriteTimeUtc(_fullPath);
#if UNITY_EDITOR
            // ImportAsset: TextAsset이 디스크 내용과 맞도록(플레이 씬이 같은 TextAsset을 읽는다).
            UnityEditor.AssetDatabase.ImportAsset(AssetPath);
            LinkToSong(song, difficulty);
#endif
        }

        /// 우리가 마지막으로 읽거나 쓴 뒤에 파일이 바뀌었으면 true(외부 편집).
        public bool ChangedOutside()
        {
            if (!Exists) return false;
            var write = File.GetLastWriteTimeUtc(_fullPath);
            return write != _lastKnownWrite;
        }

        /// 열 때 한 번 백업. Assets 밖(Library)이라 git·임포트에 섞이지 않는다.
        public void Backup()
        {
            if (!Exists) return;
            string dir = Path.GetFullPath("Library/RhythmCP/ChartBackups");
            Directory.CreateDirectory(dir);
            string name = $"{Path.GetFileNameWithoutExtension(_fullPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            File.Copy(_fullPath, Path.Combine(dir, name), true);
        }

#if UNITY_EDITOR
        void LinkToSong(SongDefinition song, Difficulty difficulty)
        {
            if (song.GetChart(difficulty) != null) return;
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            var so = new UnityEditor.SerializedObject(song);
            so.FindProperty(difficulty == Difficulty.Hard ? "_hardChart" : "_easyChart").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
    }
}
