using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Practice1.Editor
{
    public static class FinalProjectAssetBuilder
    {
        private const string Root = "Assets/Resources/FinalProject";
        private const string MaterialFolder = Root + "/Materials";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Final Project/Create Basic Models And Materials")]
        public static void CreateAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(Root);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            Material bomb = CreateMaterial("M_BombCore", new Color(0.75f, 0.05f, 0.03f));
            Material metal = CreateMaterial("M_DarkMetal", new Color(0.035f, 0.04f, 0.045f));
            Material hazard = CreateMaterial("M_HazardYellow", new Color(0.95f, 0.72f, 0.1f));
            Material cable = CreateMaterial("M_CableBlack", new Color(0.01f, 0.01f, 0.012f));
            Material disposal = CreateMaterial("M_DisposalCyan", new Color(0.08f, 0.68f, 0.95f));
            CreateMaterial("M_ArenaBackdrop", new Color(0.015f, 0.024f, 0.032f));
            CreateMaterial("M_ArenaTrim", new Color(0.52f, 0.58f, 0.62f));
            CreateMaterial("M_ArenaLightPanel", new Color(0.08f, 0.88f, 1f));
            CreateMaterial("M_PlayerSuit", new Color(0.08f, 0.23f, 0.18f));
            CreateMaterial("M_PlayerArmor", new Color(0.04f, 0.05f, 0.055f));
            CreateMaterial("M_PlayerVisor", new Color(0.12f, 0.65f, 0.9f));
            CreateMaterial("M_PlayerAccent", new Color(0.92f, 0.68f, 0.12f));

            SaveBombPrefab(bomb, metal, hazard, cable);
            SaveDisposalStationPrefab(disposal, metal, hazard);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FinalProject] Basic model prefabs and materials were created.");
        }

        [MenuItem("Final Project/Bake Editable Objects Into MainScene")]
        public static void BakeEditableObjectsIntoMainScene()
        {
            CreateAssets();

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            GameObject previousRoot = GameObject.Find(FinalProjectSceneBootstrap.SceneRootName);
            if (previousRoot != null)
            {
                Object.DestroyImmediate(previousRoot);
            }

            Material floor = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_DarkMetal.mat");
            Material wall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_DarkMetal.mat");
            Material cover = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_CableBlack.mat");
            Material hazard = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_HazardYellow.mat");
            Material disposal = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_DisposalCyan.mat");
            Material backdrop = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_ArenaBackdrop.mat");
            Material trim = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_ArenaTrim.mat");
            Material lightPanel = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_ArenaLightPanel.mat");

            GameObject root = new GameObject(FinalProjectSceneBootstrap.SceneRootName);
            Undo.RegisterCreatedObjectUndo(root, "Bake final project objects");

            CreateSceneCube(root.transform, "ArenaFloor", new Vector3(0f, -0.12f, 1f), new Vector3(20f, 0.2f, 24f), floor);
            CreateSceneCube(root.transform, "NorthWall", new Vector3(0f, 1.8f, 13f), new Vector3(21f, 3.6f, 0.5f), wall);
            CreateSceneCube(root.transform, "SouthWall", new Vector3(0f, 1.8f, -11f), new Vector3(21f, 3.6f, 0.5f), wall);
            CreateSceneCube(root.transform, "WestWall", new Vector3(-10f, 1.8f, 1f), new Vector3(0.5f, 3.6f, 24f), wall);
            CreateSceneCube(root.transform, "EastWall", new Vector3(10f, 1.8f, 1f), new Vector3(0.5f, 3.6f, 24f), wall);

            CreateSceneCube(root.transform, "OuterNorthBlastWall", new Vector3(0f, 4.8f, 15.2f), new Vector3(27f, 9.6f, 0.65f), backdrop);
            CreateSceneCube(root.transform, "OuterSouthBlastWall", new Vector3(0f, 4.8f, -13.2f), new Vector3(27f, 9.6f, 0.65f), backdrop);
            CreateSceneCube(root.transform, "OuterWestBlastWall", new Vector3(-12.7f, 4.8f, 1f), new Vector3(0.65f, 9.6f, 29f), backdrop);
            CreateSceneCube(root.transform, "OuterEastBlastWall", new Vector3(12.7f, 4.8f, 1f), new Vector3(0.65f, 9.6f, 29f), backdrop);
            CreateSceneCube(root.transform, "NorthHorizonBlocker", new Vector3(0f, 7.5f, 19.5f), new Vector3(40f, 15f, 0.7f), backdrop);

            CreateSceneCube(root.transform, "NorthLightStrip", new Vector3(0f, 3.75f, 12.68f), new Vector3(15.5f, 0.12f, 0.08f), lightPanel);
            CreateSceneCube(root.transform, "SouthLightStrip", new Vector3(0f, 3.75f, -10.68f), new Vector3(15.5f, 0.12f, 0.08f), lightPanel);
            CreateSceneCube(root.transform, "WestLightStrip", new Vector3(-9.68f, 3.75f, 1f), new Vector3(0.08f, 0.12f, 18f), lightPanel);
            CreateSceneCube(root.transform, "EastLightStrip", new Vector3(9.68f, 3.75f, 1f), new Vector3(0.08f, 0.12f, 18f), lightPanel);

            CreateSceneCube(root.transform, "NorthWallTopTrim", new Vector3(0f, 3.65f, 13f), new Vector3(21.4f, 0.22f, 0.7f), trim);
            CreateSceneCube(root.transform, "SouthWallTopTrim", new Vector3(0f, 3.65f, -11f), new Vector3(21.4f, 0.22f, 0.7f), trim);
            CreateSceneCube(root.transform, "WestWallTopTrim", new Vector3(-10f, 3.65f, 1f), new Vector3(0.7f, 0.22f, 24.4f), trim);
            CreateSceneCube(root.transform, "EastWallTopTrim", new Vector3(10f, 3.65f, 1f), new Vector3(0.7f, 0.22f, 24.4f), trim);

            CreateSceneCube(root.transform, "Cover_A", new Vector3(-4.5f, 0.55f, -2f), new Vector3(3f, 1.1f, 1.1f), cover);
            CreateSceneCube(root.transform, "Cover_B", new Vector3(4.5f, 0.55f, 4f), new Vector3(3f, 1.1f, 1.1f), cover);
            CreateSceneCube(root.transform, "Cover_C", new Vector3(-4f, 0.55f, 6.5f), new Vector3(1.2f, 1.1f, 3.2f), cover);
            CreateSceneCube(root.transform, "Cover_D", new Vector3(4f, 0.55f, -5f), new Vector3(1.2f, 1.1f, 3.2f), cover);

            CreateSceneCube(root.transform, "BombSpawnMark", new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.05f, 2.2f), hazard);
            CreateSceneCube(root.transform, "BombSpawnBeacon", new Vector3(0f, 0.7f, 0f), new Vector3(0.18f, 1.2f, 0.18f), hazard);

            GameObject disposalZone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disposalZone.name = "DisposalZoneVisual";
            disposalZone.transform.SetParent(root.transform);
            disposalZone.transform.position = new Vector3(0f, 0.04f, 8f);
            disposalZone.transform.localScale = new Vector3(2.8f, 0.05f, 2.8f);
            disposalZone.GetComponent<Renderer>().sharedMaterial = disposal;
            DestroyCollider(disposalZone);

            GameObject bombPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/BombModel.prefab");
            GameObject bomb = bombPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(bombPrefab, scene)
                : new GameObject("BombVisual");
            bomb.name = "BombVisual";
            bomb.transform.SetParent(root.transform);
            bomb.transform.position = new Vector3(0f, 1.35f, 0f);

            GameObject stationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/DisposalStation.prefab");
            GameObject station = stationPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(stationPrefab, scene)
                : new GameObject("DisposalStationModel");
            station.name = "DisposalStationModel";
            station.transform.SetParent(root.transform);
            station.transform.position = new Vector3(0f, 0.08f, 8f);

            Light light = new GameObject("ArenaKeyLight").AddComponent<Light>();
            light.transform.SetParent(root.transform);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            light.intensity = 1.25f;

            Camera sceneCamera = Object.FindFirstObjectByType<Camera>();
            if (sceneCamera != null)
            {
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = new Color(0.015f, 0.02f, 0.028f);
            }

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.11f, 0.12f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FinalProject] Editable scene objects were baked into MainScene.");
        }

        [MenuItem("Final Project/Bake Sapper Player Model Into Player Prefab")]
        public static void BakeSapperPlayerModelIntoPlayerPrefab()
        {
            CreateAssets();

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (prefabRoot == null)
            {
                throw new FileNotFoundException($"Player prefab not found: {PlayerPrefabPath}");
            }

            Transform previous = prefabRoot.transform.Find("SapperPlayerModel");
            if (previous != null)
            {
                Object.DestroyImmediate(previous.gameObject);
            }

            Material suit = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_PlayerSuit.mat");
            Material armor = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_PlayerArmor.mat");
            Material visor = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_PlayerVisor.mat");
            Material accent = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_PlayerAccent.mat");
            Material cable = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_CableBlack.mat");

            MeshRenderer rootRenderer = prefabRoot.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                rootRenderer.sharedMaterial = suit;
            }

            GameObject modelRoot = new GameObject("SapperPlayerModel");
            modelRoot.transform.SetParent(prefabRoot.transform, false);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;
            modelRoot.transform.localScale = Vector3.one;

            CreatePrimitive(modelRoot.transform, PrimitiveType.Sphere, "Helmet", new Vector3(0f, 1.22f, 0f), new Vector3(0.58f, 0.42f, 0.58f), armor);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "Visor", new Vector3(0f, 1.23f, 0.31f), new Vector3(0.42f, 0.18f, 0.08f), visor);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "ChestArmor", new Vector3(0f, 0.52f, 0.23f), new Vector3(0.78f, 0.72f, 0.16f), armor);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "BombSquadPatch", new Vector3(0f, 0.68f, 0.33f), new Vector3(0.34f, 0.13f, 0.04f), accent);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "Backpack", new Vector3(0f, 0.48f, -0.38f), new Vector3(0.74f, 0.82f, 0.28f), armor);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "LeftShoulderPad", new Vector3(-0.47f, 0.78f, 0f), new Vector3(0.22f, 0.24f, 0.38f), armor);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "RightShoulderPad", new Vector3(0.47f, 0.78f, 0f), new Vector3(0.22f, 0.24f, 0.38f), armor);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "LeftGlove", new Vector3(-0.54f, 0.12f, 0.05f), new Vector3(0.16f, 0.20f, 0.16f), cable);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "RightGlove", new Vector3(0.54f, 0.12f, 0.05f), new Vector3(0.16f, 0.20f, 0.16f), cable);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "LeftBoot", new Vector3(-0.22f, -0.92f, 0.08f), new Vector3(0.24f, 0.18f, 0.32f), cable);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cube, "RightBoot", new Vector3(0.22f, -0.92f, 0.08f), new Vector3(0.24f, 0.18f, 0.32f), cable);
            CreatePrimitive(modelRoot.transform, PrimitiveType.Cylinder, "DefuseTool", new Vector3(0.46f, 0.2f, -0.36f), new Vector3(0.05f, 0.42f, 0.05f), accent);

            StripColliders(modelRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FinalProject] Sapper player model was baked into Player.prefab.");
        }

        private static void SaveBombPrefab(Material bomb, Material metal, Material hazard, Material cable)
        {
            GameObject root = new GameObject("BombModel");

            GameObject body = CreatePrimitive(root.transform, PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(0.82f, 0.82f, 0.82f), bomb);
            DestroyCollider(body);

            GameObject belt = CreatePrimitive(root.transform, PrimitiveType.Cube, "MetalBelt", Vector3.zero, new Vector3(0.95f, 0.18f, 0.95f), metal);
            DestroyCollider(belt);

            GameObject timer = CreatePrimitive(root.transform, PrimitiveType.Cube, "TimerDisplay", new Vector3(0f, 0.12f, -0.45f), new Vector3(0.58f, 0.28f, 0.12f), hazard);
            DestroyCollider(timer);

            GameObject fuse = CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Fuse", new Vector3(0f, 0.58f, 0f), new Vector3(0.08f, 0.25f, 0.08f), cable);
            fuse.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            DestroyCollider(fuse);

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.25f, 0.1f);

            SavePrefab(root, PrefabFolder + "/BombModel.prefab");
        }

        private static void SaveDisposalStationPrefab(Material disposal, Material metal, Material hazard)
        {
            GameObject root = new GameObject("DisposalStation");

            GameObject basePlate = CreatePrimitive(root.transform, PrimitiveType.Cylinder, "BasePlate", new Vector3(0f, 0.05f, 0f), new Vector3(2.5f, 0.08f, 2.5f), disposal);
            DestroyCollider(basePlate);

            GameObject console = CreatePrimitive(root.transform, PrimitiveType.Cube, "Console", new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.8f, 0.9f), metal);
            DestroyCollider(console);

            GameObject screen = CreatePrimitive(root.transform, PrimitiveType.Cube, "Screen", new Vector3(0f, 0.78f, -0.48f), new Vector3(1.35f, 0.34f, 0.08f), disposal);
            DestroyCollider(screen);

            GameObject warning = CreatePrimitive(root.transform, PrimitiveType.Cube, "WarningPanel", new Vector3(0f, 0.48f, -0.51f), new Vector3(0.86f, 0.12f, 0.04f), hazard);
            DestroyCollider(warning);

            GameObject antenna = CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Antenna", new Vector3(0.72f, 1.3f, 0f), new Vector3(0.05f, 0.65f, 0.05f), disposal);
            DestroyCollider(antenna);

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 1.25f;
            light.color = new Color(0.15f, 0.85f, 1f);

            SavePrefab(root, PrefabFolder + "/DisposalStation.prefab");
        }

        private static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        private static GameObject CreateSceneCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                                Shader.Find("Universal Render Pipeline/Simple Lit") ??
                                Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void DestroyCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void StripColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
