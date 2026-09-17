using UnityEditor;
using UnityEngine;

namespace TowerDefense.Map.EditorTools
{
    [CustomEditor(typeof(TileMap))]
    public sealed class TileMapEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var map = (TileMap)target;

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28)))
                {
                    Undo.RecordObject(map, "Generate TileMap");
                    map.Generate();
                    EditorUtility.SetDirty(map);
                }
                if (GUILayout.Button("New Seed", GUILayout.Height(28)))
                {
                    Undo.RecordObject(map, "Generate TileMap (new seed)");
                    map.GenerateNewSeed();
                    EditorUtility.SetDirty(map);
                }
            }

            if (map.Grid != null)
            {
                EditorGUILayout.HelpBox(
                    $"{map.Grid.Width}×{map.Grid.Height} 타일, 경로 {map.Grid.Path.Count}칸, " +
                    $"스폰 {map.Grid.Spawn} → 도착 {map.Grid.Goal}, 연결 {(map.Grid.IsConnected() ? "OK" : "끊김")}",
                    MessageType.Info);
            }
        }
    }

    /// <summary>맵 프로토타입 씬 구성: 머티리얼 에셋 생성 + TileMap/카메라/라이트 배치.</summary>
    public static class TileMapSetup
    {
        private const string MatDir = "Assets/_Project/Materials/Map";

        [MenuItem("TowerDefense/Map/Create Map Materials")]
        public static void CreateMaterials()
        {
            EnsureMaterial("M_Ground", new Color(0.45f, 0.64f, 0.30f));
            EnsureMaterial("M_Path", new Color(0.72f, 0.62f, 0.45f));
            EnsureMaterial("M_Spawn", new Color(0.85f, 0.32f, 0.28f));
            EnsureMaterial("M_Goal", new Color(0.28f, 0.52f, 0.88f));
            AssetDatabase.SaveAssets();
        }

        [MenuItem("TowerDefense/Map/Create TileMap In Scene")]
        public static TileMap CreateInScene()
        {
            CreateMaterials();
            var go = new GameObject("TileMap");
            Undo.RegisterCreatedObjectUndo(go, "Create TileMap");
            var map = go.AddComponent<TileMap>();
            map.groundMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Ground.mat");
            map.pathMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Path.mat");
            map.spawnMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Spawn.mat");
            map.goalMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Goal.mat");
            map.Generate();
            go.isStatic = true;
            Selection.activeGameObject = go;
            return map;
        }

        /// <summary>맵 전체가 보이는 3/4 시점 카메라를 맞춘다.</summary>
        public static void FrameCamera(Camera cam, TileMap map)
        {
            float w = map.width * map.TileSize, h = map.height * map.TileSize;
            var center = map.transform.TransformPoint(new Vector3(w * 0.5f, 0f, h * 0.5f));
            cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            float dist = Mathf.Max(w, h * 1.3f) * 0.9f;
            cam.transform.position = center - cam.transform.forward * dist;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Map");
            }
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
