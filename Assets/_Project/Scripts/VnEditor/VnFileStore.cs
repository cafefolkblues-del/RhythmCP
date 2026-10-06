using System;
using System.Collections.Generic;
using System.IO;
using RhythmCP.Vn;
using UnityEngine;

namespace RhythmCP.VnEditing
{
    /// 에피소드 목록 한 줄(파일을 열지 않고 탭·드롭다운에 보이기 위한 요약).
    public class VnEpisodeEntry
    {
        public string Id;
        public string Route;
        public string Edition;
        public int EpIndex;
        public string AssetPath;
    }

    /// VN 파일 읽기/쓰기·백업·외부 변경 감지. 폴더: VN/{edition}/{id}.json, 등록부: VN/flags.json.
    /// adult 폴더는 .gitignore 대상(레포 제외) — 에디터는 똑같이 다룬다.
    public class VnFileStore
    {
        public const string Root = "Assets/_Project/VN";
        public static string FlagsPath => Root + "/flags.json";

        readonly Dictionary<string, DateTime> _lastKnownWrite = new Dictionary<string, DateTime>();

        public static string PathFor(string edition, string id) => $"{Root}/{edition}/{id}.json";

        /// 두 판본 폴더의 에피소드 전부. 깨진 파일은 목록에 이름만(route = "?") — 열면 검사기·로그로 원인을 본다.
        public List<VnEpisodeEntry> List()
        {
            var list = new List<VnEpisodeEntry>();
            foreach (var edition in new[] { VnIds.Base, VnIds.Adult })
            {
                string dir = Path.GetFullPath($"{Root}/{edition}");
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    var entry = new VnEpisodeEntry { Id = id, Edition = edition, Route = "?", AssetPath = PathFor(edition, id) };
                    try
                    {
                        var ep = VnSerializer.ReadEpisode(File.ReadAllText(file));
                        entry.Route = ep.meta.route;
                        entry.EpIndex = ep.meta.epIndex;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[VN 에디터] {entry.AssetPath} 읽기 실패: {e.Message}");
                    }
                    list.Add(entry);
                }
            }
            list.Sort((a, b) => a.EpIndex != b.EpIndex ? a.EpIndex.CompareTo(b.EpIndex) : string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        public VnEpisode Load(string assetPath)
        {
            string full = Path.GetFullPath(assetPath);
            var ep = VnSerializer.ReadEpisode(File.ReadAllText(full));
            _lastKnownWrite[assetPath] = File.GetLastWriteTimeUtc(full);
            return ep;
        }

        public void Save(string assetPath, VnEpisode episode) => Write(assetPath, VnSerializer.WriteEpisode(episode));

        public VnFlagRegistry LoadFlags()
        {
            string full = Path.GetFullPath(FlagsPath);
            if (!File.Exists(full)) return new VnFlagRegistry();
            _lastKnownWrite[FlagsPath] = File.GetLastWriteTimeUtc(full);
            return VnSerializer.ReadFlags(File.ReadAllText(full));
        }

        public void SaveFlags(VnFlagRegistry registry) => Write(FlagsPath, VnSerializer.WriteFlags(registry));

        /// 우리가 마지막으로 읽거나 쓴 뒤에 파일이 바뀌었으면 true(외부 편집).
        public bool ChangedOutside(string assetPath)
        {
            string full = Path.GetFullPath(assetPath);
            if (!File.Exists(full) || !_lastKnownWrite.TryGetValue(assetPath, out var known)) return false;
            return File.GetLastWriteTimeUtc(full) != known;
        }

        /// 열 때 한 번. Assets 밖(Library)이라 git·임포트에 섞이지 않는다.
        public void Backup(string assetPath)
        {
            string full = Path.GetFullPath(assetPath);
            if (!File.Exists(full)) return;
            string dir = Path.GetFullPath("Library/RhythmCP/VnBackups");
            Directory.CreateDirectory(dir);
            File.Copy(full, Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(full)}_{DateTime.Now:yyyyMMdd_HHmmss}.json"), true);
        }

        /// 새 에피소드 id: 공통 = ep_c01…, 분기 = ep_yume_01…. 판본과 무관하게 같은 번호 체계(성인판은 같은 id를 adult 폴더에).
        public static string NextEpisodeId(List<VnEpisodeEntry> existing, string route, out int epIndex)
        {
            string prefix = route == VnIds.Common ? "ep_c" : $"ep_{route}_";
            int n = 1;
            while (existing.Exists(e => e.Id == $"{prefix}{n:00}")) n++;
            epIndex = n;
            return $"{prefix}{n:00}";
        }

        void Write(string assetPath, string json)
        {
            string full = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, json);
            _lastKnownWrite[assetPath] = File.GetLastWriteTimeUtc(full);
#if UNITY_EDITOR
            // TextAsset이 디스크 내용과 맞도록(플레이 씬·카탈로그가 같은 TextAsset을 읽는다).
            UnityEditor.AssetDatabase.ImportAsset(assetPath);
#endif
        }
    }
}
