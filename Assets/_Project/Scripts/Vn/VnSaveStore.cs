using System;
using System.IO;
using Newtonsoft.Json;

namespace RhythmCP.Vn
{
    /// VN 세이브 1슬롯. 위치는 인덱스가 아니라 라인 id — 스크립트에 라인이 끼어들어도 같은 자리로 돌아온다.
    /// 게임 세이브(라우트 카운트 등)와의 결합은 M4/M5. 그때 이 객체를 통째로 게임 세이브 안에 넣는다.
    [Serializable]
    public class VnSaveData
    {
        public int version = 1;
        public string episodeId;
        public string lineId;
        public VnFlags flags;

        /// 그 라인을 적용하기 직전 무대(VnPlayer.StageBeforeCurrent).
        public VnStageState stage;

        /// 슬롯 목록 표시용.
        public string savedAt;
        public string speakerName;
        public string preview;
    }

    /// 슬롯 파일 저장소. 폴더 하나에 slot_01.json …. 경로를 밖에서 받아 테스트에서 임시 폴더를 쓴다.
    public class VnSaveStore
    {
        readonly string _dir;

        public int SlotCount { get; }

        public VnSaveStore(string dir, int slotCount)
        {
            _dir = dir;
            SlotCount = slotCount;
        }

        string PathOf(int slot) => Path.Combine(_dir, $"slot_{slot + 1:00}.json");

        public bool Exists(int slot) => File.Exists(PathOf(slot));

        /// 깨진 파일은 빈 슬롯처럼 null(덮어쓰기는 가능).
        public VnSaveData Load(int slot)
        {
            if (!Exists(slot)) return null;
            try
            {
                return JsonConvert.DeserializeObject<VnSaveData>(File.ReadAllText(PathOf(slot)));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public void Save(int slot, VnSaveData data)
        {
            Directory.CreateDirectory(_dir);
            // 임시 파일에 쓰고 교체 — 쓰는 도중 꺼져도 기존 세이브가 반쯤 지워지지 않게.
            string path = PathOf(slot), tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(data, Formatting.Indented));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public void Delete(int slot)
        {
            if (Exists(slot)) File.Delete(PathOf(slot));
        }
    }
}
