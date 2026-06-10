using System.IO;
using UnityEditor;
using UnityEngine;

namespace Practice1.Editor
{
    public static class FinalProjectAssetBuilder
    {
        private const string Root = "Assets/Resources/FinalProject";
        private const string MaterialFolder = Root + "/Materials";
        private const string PrefabFolder = Root + "/Prefabs";

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

            SaveBombPrefab(bomb, metal, hazard, cable);
            SaveDisposalStationPrefab(disposal, metal, hazard);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FinalProject] Basic model prefabs and materials were created.");
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
