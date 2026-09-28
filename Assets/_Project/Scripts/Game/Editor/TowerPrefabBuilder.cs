using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Game.EditorTools
{
    /// <summary>
    /// 포탑 FBX(Tools/Blender/import_hitem_tower.py 결과) + 구운 색 텍스처(&lt;이름&gt;_Albedo.png) → URP 머티리얼이 붙은 프리팹.
    /// 세션은 Resources.Load("Towers/Tower&lt;종류&gt;")로 찾는다 (Tower.Create). 모델이 없는 종류는 색 상자로 남는다.
    /// </summary>
    public static class TowerPrefabBuilder
    {
        private const string ModelDir = "Assets/_Project/Art/Models/Towers";
        private const string MatDir = "Assets/_Project/Materials/Towers";
        private const string PrefabDir = "Assets/_Project/Resources/Towers";

        [MenuItem("TowerDefense/Towers/Build Tower Prefabs")]
        public static void BuildAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Build(path, Path.GetFileNameWithoutExtension(path));
            }
        }

        public static GameObject Build(string fbxPath, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogError($"[TowerPrefabBuilder] 모델을 찾을 수 없음: {fbxPath}");
                return null;
            }
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();

            string matPath = $"{MatDir}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"M_{name}" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelDir}/{name}_Albedo.png");
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in instance.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c); // 설치 레이캐스트가 지형에 닿도록

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TowerPrefabBuilder] {PrefabDir}/{name}.prefab 생성");
            return prefab;
        }
    }
}
