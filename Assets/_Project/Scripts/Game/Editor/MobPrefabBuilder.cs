using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Game.EditorTools
{
    /// <summary>
    /// Blender에서 내보낸 몹 FBX → URP 머티리얼이 붙은 프리팹.
    /// FBX 임포터가 만든 머티리얼은 셰이더가 파이프라인과 맞지 않을 수 있어, 이름·색을 읽어 URP Lit 머티리얼을 새로 만들고 교체한다.
    /// </summary>
    public static class MobPrefabBuilder
    {
        private const string MatDir = "Assets/_Project/Materials/Mobs";
        // 세션이 Resources.Load("Creeps/<이름>")로 크립 종류별 모델을 찾는다 (EnemySpawner.ModelFor)
        private const string PrefabDir = "Assets/_Project/Resources/Creeps";

        /// <summary>Art/Models/Mobs의 FBX를 전부 프리팹으로 (Sheep, Chicken, Cow, Pig, Bull …)</summary>
        [MenuItem("TowerDefense/Mobs/Build All Mob Prefabs")]
        public static void BuildAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Art/Models/Mobs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Build(path, Path.GetFileNameWithoutExtension(path));
            }
        }

        public static GameObject Build(string fbxPath, string prefabName)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogError($"[MobPrefabBuilder] 모델을 찾을 수 없음: {fbxPath}");
                return null;
            }

            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = prefabName;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var cache = new Dictionary<string, Material>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    string key = src.name.Replace(" (Instance)", "");
                    if (!cache.TryGetValue(key, out var mat))
                    {
                        string path = $"{MatDir}/M_{prefabName}_{key}.mat";
                        mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (mat == null)
                        {
                            mat = new Material(shader) { name = $"M_{prefabName}_{key}" };
                            var color = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                                : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
                            mat.SetColor("_BaseColor", color);
                            mat.SetFloat("_Smoothness", key == "Dark" ? 0.25f : 0.08f);
                            AssetDatabase.CreateAsset(mat, path);
                        }
                        cache[key] = mat;
                    }
                    mats[i] = mat;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in instance.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            string prefabPath = $"{PrefabDir}/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MobPrefabBuilder] {prefabPath} 생성 (머티리얼 {cache.Count}개)");
            return prefab;
        }
    }
}
