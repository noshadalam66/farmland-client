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
            controller.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = screen;
        }

        private static GameObject CreateTile(string name, Vector3 center, Vector3 size, Color color, string materialPath)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = name;
            tile.transform.position = center;
            tile.transform.localScale = size;

            Shader shader = GraphicsSettingsShader();
            if (shader == null) return tile;
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(materialPath));
            tile.GetComponent<Renderer>().sharedMaterial = material;
            return tile;
        }

        private static Shader GraphicsSettingsShader()
        {
            return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                ? Shader.Find("Standard")
                : Shader.Find("Universal Render Pipeline/Lit");
        }
    }
}
