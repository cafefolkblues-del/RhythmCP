using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace RhythmCP.Vn
{
    /// 기읽 기록(전역, 세이브 슬롯과 무관). 에피소드 id → 읽은 라인 id 집합. 파일 하나에 저장.
    /// 라인 id를 다시 매기지 않는 이유가 이것 — 스크립트를 고쳐도 읽은 기록이 유지된다.
    public class VnReadLog : IVnReadLog
    {
        readonly string _path;
        Dictionary<string, HashSet<string>> _read = new Dictionary<string, HashSet<string>>();

        public bool Dirty { get; private set; }

        /// path null = 메모리 전용(에디터 프리뷰).
        public VnReadLog(string path)
        {
            _path = path;
            if (_path == null || !File.Exists(_path)) return;
            _read = JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(File.ReadAllText(_path))
                    ?? new Dictionary<string, HashSet<string>>();
        }

        public bool IsRead(string episodeId, string lineId) =>
            _read.TryGetValue(episodeId, out var set) && set.Contains(lineId);

        public void MarkRead(string episodeId, string lineId)
        {
            if (!_read.TryGetValue(episodeId, out var set)) _read[episodeId] = set = new HashSet<string>();
            if (set.Add(lineId)) Dirty = true;
        }

        /// 매 라인마다 쓰지 않고 세이브·에피소드 끝·종료 때 한 번에.
        public void Flush()
        {
            if (!Dirty || _path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, JsonConvert.SerializeObject(_read, Formatting.Indented));
            Dirty = false;
        }
    }
}
