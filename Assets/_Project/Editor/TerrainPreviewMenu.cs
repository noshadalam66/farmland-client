using System;
using System.Collections.Generic;
using System.IO;
using FarmLand.Empire.World;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FarmLand.Empire.Editor
{
    internal static class TerrainPreviewMenu
    {
        [MenuItem("FarmLand/Preview Terrain Package")]
        private static void Preview()
        {
            string path = EditorUtility.OpenFilePanel("Open terrain manifest", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            var meshes = new List<Mesh>();
            try
            {
                var package = TerrainPackage.Parse(File.ReadAllText(path));
                string directory = Path.GetDirectoryName(path);
                for (int y = 0; y < package.ChunksPerAxis; y++)
                    for (int x = 0; x < package.ChunksPerAxis; x++)
                        meshes.Add(package.CreateChunk(x, y,
                            File.ReadAllBytes(Path.Combine(directory, TerrainPackage.ChunkFilename(x, y)))));

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Terrain Preview");
                var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                foreach (Mesh mesh in meshes)
                {
                    var tile = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                    tile.transform.SetParent(root.transform, false);
                    mesh.hideFlags = HideFlags.DontSaveInEditor;
                    tile.GetComponent<MeshFilter>().sharedMesh = mesh;
                    tile.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                }
                meshes.Clear();

                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            catch (Exception error) when (error is IOException || error is JsonException ||
                                           error is FormatException || error is InvalidCastException ||
                                           error is OverflowException)
            {
                EditorUtility.DisplayDialog("Terrain preview failed", error.Message, "OK");
            }
            finally
            {
                foreach (Mesh mesh in meshes) Object.DestroyImmediate(mesh);
            }
        }
    }
}
