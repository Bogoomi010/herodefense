using System.IO;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>
    /// 저장 슬롯 3개 (docs/STAGE.md): persistentDataPath/save_{0..2}.json. 슬롯마다 완전히 다른 게임이다.
    /// 지금 플레이 중인 슬롯은 <see cref="Current"/> (PlayerPrefs "SaveSlot"에 기억).
    /// </summary>
    public static class ProfileStore
    {
        public const int SlotCount = 3;

        public static string PathOf(int slot) => System.IO.Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        /// <summary>지금 플레이 중인 슬롯</summary>
        public static int Current
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("SaveSlot", 0), 0, SlotCount - 1);
            set { PlayerPrefs.SetInt("SaveSlot", Mathf.Clamp(value, 0, SlotCount - 1)); PlayerPrefs.Save(); }
        }

        /// <summary>자동 플레이 테스트: 저장 파일을 읽지도 쓰지도 않는다 (실제 기록 보호)</summary>
        public static bool Sandbox;

        public static bool Exists(int slot) => !Sandbox && File.Exists(PathOf(slot));

        /// <summary>슬롯을 읽는다. 없으면 null.</summary>
        public static PlayerProfile Load(int slot)
        {
            string path = PathOf(slot);
            if (Sandbox || !File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
            }
            catch (System.Exception e)
            {
                // 깨진 파일은 덮어쓰기 전에 옆에 남겨 둔다
                Debug.LogWarning($"[ProfileStore] 슬롯 {slot + 1}을 읽지 못했습니다: {e.Message}");
                try { File.Copy(path, path + ".bad", true); } catch { /* 백업 실패는 무시 */ }
                return null;
            }
        }

        /// <summary>현재 슬롯. 파일이 없으면(에디터에서 스테이지 씬을 바로 실행한 경우 등) 보통 난이도 새 기록.</summary>
        public static PlayerProfile LoadCurrent() => Load(Current) ?? new PlayerProfile();

        public static void Save(int slot, PlayerProfile p)
        {
            if (Sandbox) return;
            // 임시 파일에 쓰고 교체 — 쓰는 도중 꺼져도 기존 파일이 남는다
            string path = PathOf(slot), tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(p, true));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public static void SaveCurrent(PlayerProfile p) => Save(Current, p);

        public static void Delete(int slot)
        {
            if (File.Exists(PathOf(slot))) File.Delete(PathOf(slot));
        }
    }
}
