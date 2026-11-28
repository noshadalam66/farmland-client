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

            ground.transform.SetParent(fixedFarm.transform);
            soil.transform.SetParent(fixedFarm.transform);
            GameObject cropMarker = CreateCropVisual(fixedFarm.transform, Vector3.zero, assetPrefix);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/_Project/UI/StarterFarmTheme.tss");
            AssetDatabase.CreateAsset(settings, AssetDatabase.GenerateUniqueAssetPath(assetPrefix + " Panel.asset"));

            var screen = new GameObject("Starter Farm Screen");
            var document = screen.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var controller = new SerializedObject(screen.AddComponent<StarterFarmScreen>());
            controller.FindProperty("plotCamera").objectReferenceValue = camera;
            controller.FindProperty("starterPlotCollider").objectReferenceValue = soil.GetComponent<Collider>();
            controller.FindProperty("cropMarker").objectReferenceValue = cropMarker;
            controller.FindProperty("fixedFarm").objectReferenceValue = fixedFarm;
            controller.FindProperty("terrainMaterial").objectReferenceValue = ground.GetComponent<Renderer>().sharedMaterial;
            controller.FindProperty("waterMaterial").objectReferenceValue =
                CreateMaterial(new Color(0.12f, 0.41f, 0.64f), assetPrefix + " Water.mat");
            controller.ApplyModifiedPropertiesWithoutUndo();
            AddExpansionPreview(controller, cropMarker.transform.parent, assetPrefix);
            AddTownPreview(controller, fixedFarm.transform, assetPrefix);
            AddServicePreview(controller, fixedFarm.transform, assetPrefix);
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
            GameObject crop = CreateCropVisual(expansion.transform, new Vector3(2.65f, 0, 0), assetPrefix);
            soil.transform.SetParent(expansion.transform);
            expansion.SetActive(false);

            controller.FindProperty("expansionPlotCollider").objectReferenceValue = soil.GetComponent<Collider>();
            controller.FindProperty("expansionGeometry").objectReferenceValue = expansion;
            controller.FindProperty("expansionCropMarker").objectReferenceValue = crop;
            controller.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateCropVisual(Transform parent, Vector3 center, string assetPrefix)
        {
            var bed = new GameObject("Crops");
            bed.transform.SetParent(parent, false);
            bed.transform.localPosition = center + Vector3.up * 0.15f;

            var wheat = new GameObject("Wheat");
            var corn = new GameObject("Corn");
            var rice = new GameObject("Rice");
            wheat.transform.SetParent(bed.transform, false);
            corn.transform.SetParent(bed.transform, false);
            rice.transform.SetParent(bed.transform, false);
            var wheatMaterial = CreateMaterial(new Color(0.91f, 0.69f, 0.24f), assetPrefix + " Wheat.mat");
            var cornMaterial = CreateMaterial(new Color(0.27f, 0.52f, 0.19f), assetPrefix + " Corn.mat");
            var riceMaterial = CreateMaterial(new Color(0.47f, 0.68f, 0.32f), assetPrefix + " Rice.mat");
            for (int row = -1; row <= 1; row++)
            for (int column = -1; column <= 1; column++)
            {
                var position = new Vector3(column * 0.54f, 0, row * 0.54f);
                TownPiece(wheat.transform, "Wheat head", position + Vector3.up * 0.26f,
                    new Vector3(0.10f, 0.52f, 0.10f), wheatMaterial, PrimitiveType.Cylinder);
                TownPiece(corn.transform, "Corn stalk", position + Vector3.up * 0.33f,
                    new Vector3(0.11f, 0.66f, 0.11f), cornMaterial);
                TownPiece(corn.transform, "Corn ear", position + new Vector3(0.08f, 0.49f, 0),
                    new Vector3(0.16f, 0.24f, 0.12f), wheatMaterial, PrimitiveType.Sphere);
                TownPiece(rice.transform, "Rice cluster", position + Vector3.up * 0.16f,
                    new Vector3(0.30f, 0.32f, 0.24f), riceMaterial, PrimitiveType.Sphere);
            }

            var ready = TownPiece(bed.transform, "Ready to harvest", Vector3.up * 0.91f,
                Vector3.one * 0.16f, wheatMaterial);
            ready.transform.localRotation = Quaternion.Euler(0, 45, 45);
            bed.AddComponent<PlotCropVisual>().Configure(wheat, corn, rice, ready);
            bed.SetActive(false);
            return bed;
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

        private static void AddServicePreview(SerializedObject controller, Transform fixedFarm, string assetPrefix)
        {
            var services = new GameObject("Town Services");
            services.transform.SetParent(fixedFarm, false);
            Material walls = CreateMaterial(new Color(0.75f, 0.73f, 0.63f), assetPrefix + " Service Walls.mat");
            Material water = CreateMaterial(new Color(0.21f, 0.53f, 0.74f), assetPrefix + " Water Tower.mat");
            Material workshop = CreateMaterial(new Color(0.64f, 0.37f, 0.21f), assetPrefix + " Workshop.mat");
            Material school = CreateMaterial(new Color(0.37f, 0.51f, 0.73f), assetPrefix + " School.mat");
            Material clinic = CreateMaterial(new Color(0.60f, 0.26f, 0.30f), assetPrefix + " Clinic.mat");

            Transform tower = ServiceSlot(services.transform, "water_tower", new Vector3(-2.7f, 0, -2.25f));
            TownPiece(tower, "Support", new Vector3(0, 0.27f, 0), new Vector3(0.28f, 0.36f, 0.28f),
                walls, PrimitiveType.Cylinder);
            TownPiece(tower, "Tank", new Vector3(0, 0.84f, 0), new Vector3(0.75f, 0.23f, 0.75f),
                water, PrimitiveType.Cylinder);

            for (int i = 0; i < 2; i++)
            {
                float depth = -2.25f - 1.15f * i;
                AddServiceBuilding(services.transform, "workshop", new Vector3(-0.9f, 0, depth),
                    new Vector3(0.92f, 0.52f, 0.70f), walls, workshop);
                AddServiceBuilding(services.transform, "school", new Vector3(0.9f, 0, depth),
                    new Vector3(0.92f, 0.68f, 0.70f), walls, school);
                AddServiceBuilding(services.transform, "clinic", new Vector3(2.7f, 0, depth),
                    new Vector3(0.92f, 0.56f, 0.70f), walls, clinic);
            }
            controller.FindProperty("townServiceGeometry").objectReferenceValue = services.transform;
            controller.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform ServiceSlot(Transform parent, string kind, Vector3 position)
        {
            Transform group = parent.Find(kind);
            if (group == null)
            {
                group = new GameObject(kind).transform;
                group.SetParent(parent, false);
            }
            var slot = new GameObject($"Building {group.childCount + 1}");
            slot.transform.SetParent(group, false);
            slot.transform.localPosition = position;
            slot.SetActive(false);
            return slot.transform;
        }

        private static void AddServiceBuilding(Transform parent, string kind, Vector3 position,
            Vector3 size, Material walls, Material trim)
        {
            Transform slot = ServiceSlot(parent, kind, position);
            TownPiece(slot, "Walls", new Vector3(0, size.y / 2 - 0.1f, 0), size, walls);
            TownPiece(slot, "Roof", new Vector3(0, size.y - 0.05f, 0),
                new Vector3(size.x + 0.07f, 0.1f, size.z + 0.08f), trim);
            TownPiece(slot, "Door", new Vector3(0, 0.06f, -size.z / 2),
                new Vector3(0.23f, 0.32f, 0.04f), trim);
        }

        private static GameObject TownPiece(Transform parent, string name, Vector3 localPosition,
            Vector3 size, Material material, PrimitiveType primitive = PrimitiveType.Cube)
        {
            GameObject piece = GameObject.CreatePrimitive(primitive);
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
