using System;
using System.IO;
using FarmLand.Empire.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Editor
{
    internal static class StarterFarmSceneMenu
    {
        [MenuItem("FarmLand/Create Starter Farm Scene")]
        private static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save the starter farm scene", "StarterFarm", "unity", "Choose where to save the prototype.");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                EditorUtility.DisplayDialog("Choose a new filename", "An existing scene has that name.", "OK");
                return;
            }

            CreateAt(path);
        }

        internal static void CreateAt(string path, Action<SerializedObject> configure = null)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string assetPrefix = path.Substring(0, path.Length - ".unity".Length);
            var camera = new GameObject("Isometric Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(8, 8, -8), Quaternion.Euler(35.264f, -45, 0));
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.45f, 0.36f);

            var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            var fixedFarm = new GameObject("Fixed Farm");
            GameObject ground = CreateTile("Ground", new Vector3(0, -0.35f, 0), new Vector3(8, 0.5f, 8),
                new Color(0.35f, 0.56f, 0.31f), assetPrefix + " Ground.mat");
            GameObject soil = CreateTile("Starter Plot", Vector3.zero, new Vector3(2.5f, 0.3f, 2.5f),
                new Color(0.47f, 0.28f, 0.17f), assetPrefix + " Soil.mat");

            GameObject cropMarker = CreateTile("Wheat", new Vector3(0, 0.42f, 0), new Vector3(1.6f, 0.5f, 1.6f),
                new Color(0.88f, 0.68f, 0.23f), assetPrefix + " Wheat.mat");
            ground.transform.SetParent(fixedFarm.transform);
            soil.transform.SetParent(fixedFarm.transform);
            cropMarker.transform.SetParent(fixedFarm.transform);
            cropMarker.SetActive(false);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/_Project/UI/StarterFarmTheme.tss");
            AssetDatabase.CreateAsset(settings, AssetDatabase.GenerateUniqueAssetPath(assetPrefix + " Panel.asset"));

            var screen = new GameObject("Starter Farm Screen");
            var document = screen.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var controller = new SerializedObject(screen.AddComponent<StarterFarmScreen>());
            controller.FindProperty("cropMarker").objectReferenceValue = cropMarker;
            controller.FindProperty("fixedFarm").objectReferenceValue = fixedFarm;
            controller.FindProperty("terrainMaterial").objectReferenceValue = ground.GetComponent<Renderer>().sharedMaterial;
            controller.FindProperty("waterMaterial").objectReferenceValue =
                CreateMaterial(new Color(0.12f, 0.41f, 0.64f), assetPrefix + " Water.mat");
            controller.ApplyModifiedPropertiesWithoutUndo();
            AddExpansionPreview(controller, cropMarker.transform.parent, assetPrefix);
            AddTownPreview(controller, fixedFarm.transform, assetPrefix);
            configure?.Invoke(controller);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new IOException("Could not save the generated starter farm scene.");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = screen;
        }

        private static void AddExpansionPreview(SerializedObject controller, Transform parent, string assetPrefix)
        {
            var expansion = new GameObject("Expansion Plot");
            expansion.transform.SetParent(parent, false);
            GameObject soil = CreateTile("Expansion Soil", new Vector3(2.65f, 0, 0), new Vector3(2.5f, 0.3f, 2.5f),
                new Color(0.47f, 0.28f, 0.17f), assetPrefix + " Expansion Soil.mat");
            GameObject crop = CreateTile("Expansion Crop", new Vector3(2.65f, 0.42f, 0), new Vector3(1.6f, 0.5f, 1.6f),
                new Color(0.88f, 0.68f, 0.23f), assetPrefix + " Expansion Crop.mat");
            soil.transform.SetParent(expansion.transform);
            crop.transform.SetParent(expansion.transform);
            crop.SetActive(false);
            expansion.SetActive(false);

            controller.FindProperty("expansionGeometry").objectReferenceValue = expansion;
            controller.FindProperty("expansionCropMarker").objectReferenceValue = crop;
            controller.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddTownPreview(SerializedObject controller, Transform fixedFarm, string assetPrefix)
        {
            var settlement = new GameObject("Settlement");
            settlement.transform.SetParent(fixedFarm, false);
            Material roadMaterial = CreateMaterial(new Color(0.27f, 0.31f, 0.28f), assetPrefix + " Town Road.mat");
            Material wallMaterial = CreateMaterial(new Color(0.83f, 0.68f, 0.43f), assetPrefix + " Cottage Walls.mat");
            Material roofMaterial = CreateMaterial(new Color(0.55f, 0.24f, 0.18f), assetPrefix + " Cottage Roof.mat");

            GameObject road = TownPiece(settlement.transform, "Road", new Vector3(0, -0.06f, 1.65f),
                new Vector3(7.5f, 0.08f, 0.55f), roadMaterial);
            road.SetActive(false);
            controller.FindProperty("roadGeometry").objectReferenceValue = road;

            SerializedProperty cottages = controller.FindProperty("cottageGeometry");
            cottages.arraySize = 4;
            for (int i = 0; i < cottages.arraySize; i++)
            {
                var cottage = new GameObject($"Cottage {i + 1}");
                cottage.transform.SetParent(settlement.transform, false);
                cottage.transform.localPosition = new Vector3(-2.7f + i * 1.8f, 0, 2.65f);
                TownPiece(cottage.transform, "Walls", new Vector3(0, 0.25f, 0),
                    new Vector3(1.0f, 0.7f, 0.85f), wallMaterial);
                TownPiece(cottage.transform, "Door", new Vector3(0, 0.1f, -0.44f),
                    new Vector3(0.23f, 0.39f, 0.04f), roadMaterial);
                GameObject westRoof = TownPiece(cottage.transform, "West Roof", new Vector3(-0.26f, 0.68f, 0),
                    new Vector3(0.63f, 0.09f, 1.04f), roofMaterial);
                westRoof.transform.localRotation = Quaternion.Euler(0, 0, 24);
                GameObject eastRoof = TownPiece(cottage.transform, "East Roof", new Vector3(0.26f, 0.68f, 0),
                    new Vector3(0.63f, 0.09f, 1.04f), roofMaterial);
                eastRoof.transform.localRotation = Quaternion.Euler(0, 0, -24);
                cottage.SetActive(false);
                cottages.GetArrayElementAtIndex(i).objectReferenceValue = cottage;
            }
            controller.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject TownPiece(Transform parent, string name, Vector3 localPosition,
            Vector3 size, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = size;
            if (material != null) piece.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
            return piece;
        }

        private static GameObject CreateTile(string name, Vector3 center, Vector3 size, Color color, string materialPath)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = name;
            tile.transform.position = center;
            tile.transform.localScale = size;

            Material material = CreateMaterial(color, materialPath);
            if (material != null) tile.GetComponent<Renderer>().sharedMaterial = material;
            return tile;
        }

        private static Material CreateMaterial(Color color, string materialPath)
        {
            Shader shader = GraphicsSettingsShader();
            if (shader == null) return null;
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(materialPath));
            return material;
        }

        private static Shader GraphicsSettingsShader()
        {
            return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                ? Shader.Find("Standard")
                : Shader.Find("Universal Render Pipeline/Lit");
        }
    }
}
