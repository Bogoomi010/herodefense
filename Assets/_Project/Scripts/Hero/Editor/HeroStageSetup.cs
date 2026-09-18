using TowerDefense.Game;
using TowerDefense.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.Hero.Editor
{
    /// <summary>절벽 위 영웅 Perch + 카메라 앵커 씬 구성 (멱등).</summary>
    public static class HeroStageSetup
    {
        private const float CliffHeight = 4.5f;

        [MenuItem("TowerDefense/Setup Hero Stage")]
        public static void Setup()
        {
            var map = Object.FindFirstObjectByType<TileMap>();
            if (map == null)
            {
                Debug.LogError("HeroStageSetup: 씬에 TileMap이 없습니다.");
                return;
            }

            if (map.Grid == null)
            {
                map.Generate();
            }

            // 기존 루트의 위치/회전은 유지한다 (에디터에서 손으로 옮긴 배치 보존)
            var existing = GameObject.Find("HeroStage");
            Vector3 keepPos = Vector3.zero;
            Quaternion keepRot = Quaternion.identity;
            Transform oldView = existing != null ? existing.transform.Find("PerchView") : null;
            bool keepView = oldView != null;
            Vector3 keepViewPos = keepView ? oldView.position : Vector3.zero;
            Quaternion keepViewRot = keepView ? oldView.rotation : Quaternion.identity;
            if (existing != null)
            {
                keepPos = existing.transform.position;
                keepRot = existing.transform.rotation;
                Object.DestroyImmediate(existing);
            }

            Vector3 min = map.transform.position;
            Vector3 size = new Vector3(map.width * map.TileSize, 0f, map.height * map.TileSize);
            Vector3 center = min + size * 0.5f;

            var root = new GameObject("HeroStage");
            Undo.RegisterCreatedObjectUndo(root, "Setup Hero Stage");

            Vector3 cliffTop = min + new Vector3(size.x + 2.5f, 0f, -3.5f);

            var cliffMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cliffMat.SetColor("_BaseColor", new Color(0.42f, 0.42f, 0.45f));

            CreateCliffCube(root.transform, cliffMat, "CliffMain",
                new Vector3(6f, CliffHeight, 6f),
                new Vector3(cliffTop.x, CliffHeight * 0.5f, cliffTop.z),
                Quaternion.Euler(0f, 15f, 0f));

            CreateCliffCube(root.transform, cliffMat, "CliffChunkA",
                new Vector3(3.5f, 3f, 4f),
                new Vector3(cliffTop.x, 1.5f, cliffTop.z - 4f),
                Quaternion.Euler(0f, 30f, 0f));

            CreateCliffCube(root.transform, cliffMat, "CliffChunkB",
                new Vector3(2.5f, 2f, 3f),
                new Vector3(cliffTop.x + 3f, 1f, cliffTop.z),
                Quaternion.Euler(0f, -20f, 0f));

            var perchGo = new GameObject("Perch");
            perchGo.transform.SetParent(root.transform, false); // 루트 오프셋 적용
            Vector3 perchPos = cliffTop + new Vector3(-2.2f, CliffHeight, 2.2f);
            perchGo.transform.position = perchPos;
            Vector3 toCenter = center - perchPos;
            toCenter.y = 0f;
            perchGo.transform.rotation = toCenter.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCenter.normalized)
                : Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(perchGo, "Setup Hero Stage");
            Transform perch = perchGo.transform;

            var heroGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            heroGo.name = "Hero";
            heroGo.transform.SetParent(root.transform, false); // 루트 오프셋 적용
            heroGo.transform.SetPositionAndRotation(perch.position + Vector3.up, perch.rotation);
            var heroMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            heroMat.SetColor("_BaseColor", new Color(0.36f, 0.30f, 0.75f));
            heroGo.GetComponent<Renderer>().sharedMaterial = heroMat;
            Undo.RegisterCreatedObjectUndo(heroGo, "Setup Hero Stage");

            var hero = heroGo.AddComponent<TowerDefense.Hero.Hero>();
            hero.perch = perch;
            hero.cam = Camera.main;
            hero.map = map;
            hero.session = Object.FindFirstObjectByType<EnemySpawner>();

            var perchViewGo = new GameObject("PerchView");
            perchViewGo.transform.SetParent(root.transform, false); // 루트 오프셋 적용
            Vector3 perchViewPos = perch.position - perch.forward * 8f + Vector3.up * 5.5f;
            perchViewPos -= perch.right * 4.5f;
            perchViewGo.transform.position = perchViewPos;
            Vector3 lookDir = (center + Vector3.up * 0.5f) - perchViewPos;
            perchViewGo.transform.rotation = lookDir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDir.normalized)
                : Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(perchViewGo, "Setup Hero Stage");
            Transform perchView = perchViewGo.transform;

            // 자식은 원점 기준으로 만든 뒤 루트를 옮겨 이전 배치(손으로 옮긴 위치)를 그대로 잇는다
            root.transform.SetPositionAndRotation(keepPos, keepRot);
            if (keepView) perchView.SetPositionAndRotation(keepViewPos, keepViewRot); // 손으로 맞춘 인게임 시점 보존

            Camera cam = Camera.main;
            if (cam != null)
            {
                var heroCam = cam.GetComponent<TowerDefense.Hero.HeroCamera>();
                if (heroCam == null)
                {
                    heroCam = cam.gameObject.AddComponent<TowerDefense.Hero.HeroCamera>();
                    cam.fieldOfView = 55f; // 처음 붙일 때만. 이후엔 씬에서 맞춘 FOV 유지
                }
                heroCam.hero = hero;
                heroCam.perchView = perchView;
                heroCam.SnapToPerch();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log("HeroStageSetup: HeroStage 생성 완료 (Cliff/Perch/Hero/PerchView).");
        }

        private static void CreateCliffCube(Transform parent, Material mat, string name, Vector3 scale, Vector3 position, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 절벽 그림자가 맵을 덮지 않게
            Undo.RegisterCreatedObjectUndo(go, "Setup Hero Stage");
        }
    }
}
