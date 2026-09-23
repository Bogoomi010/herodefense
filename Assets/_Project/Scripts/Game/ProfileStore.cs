using System.IO;
using TowerDefense.Core;
using UnityEngine;

namespace TowerDefense.Game
{
    /// <summary>플레이어 프로필 저장·불러오기: persistentDataPath/profile.json (docs/PORTING_PLAN.md 세이브 항목).</summary>
    public static class ProfileStore
    {
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "profile.json");

        public static PlayerProfile Load()
        {
            try
            {
                if (File.Exists(Path)) return JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(Path)) ?? new PlayerProfile();
            }
            catch (System.Exception e)
            {
                // 깨진 파일은 덮어쓰기 전에 옆에 남겨 둔다
                Debug.LogWarning($"[ProfileStore] 프로필을 읽지 못해 새로 시작합니다: {e.Message}");
                try { File.Copy(Path, Path + ".bad", true); } catch { /* 백업 실패는 무시 */ }
            }
            return new PlayerProfile();
        }

        public static void Save(PlayerProfile p)
        {
            // 임시 파일에 쓰고 교체 — 쓰는 도중 꺼져도 기존 파일이 남는다
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(p, true));
            if (File.Exists(Path)) File.Replace(tmp, Path, null);
            else File.Move(tmp, Path);
        }
    }
}
