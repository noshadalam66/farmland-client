using System;
using System.IO;
using FarmLand.Empire.Networking;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarmLand.Empire.Editor
{
    public static class PilotBuild
    {
        public static void Build()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run the pilot build from Unity batch mode.");

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string outputName = target switch
            {
                BuildTarget.WebGL => "web",
                BuildTarget.Android => "FarmLandPilot.apk",
                BuildTarget.iOS => "ios-xcode",
                _ => throw new InvalidOperationException("Pass -buildTarget WebGL, Android, or iOS."),
            };
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException($"Install Unity build support for {target}.");

            string apiOrigin = Required("FARMLAND_API_ORIGIN");
            if (!Uri.TryCreate(apiOrigin, UriKind.Absolute, out Uri origin))
                throw new InvalidOperationException("FARMLAND_API_ORIGIN must be an absolute origin.");
            _ = new StarterFarmApi(origin);
            string firebaseKey = Required("FARMLAND_FIREBASE_WEB_API_KEY");
            string mapsKey = target == BuildTarget.WebGL
                ? Environment.GetEnvironmentVariable("FARMLAND_GOOGLE_MAPS_BROWSER_KEY")?.Trim() ?? ""
                : "";

            string runId = Guid.NewGuid().ToString("N");
            string generatedName = "_FarmLandPilotBuild_" + runId;
            string generatedPath = "Assets/" + generatedName;
            string scenePath = generatedPath + "/Pilot.unity";
            string buildRoot = Path.GetFullPath(Path.Combine("Builds", "Pilot", target.ToString(), runId));
            Directory.CreateDirectory(buildRoot);

            bool previousBundleSetting = EditorUserBuildSettings.buildAppBundle;
            try
            {
                string folderGuid = AssetDatabase.CreateFolder("Assets", generatedName);
                if (string.IsNullOrEmpty(folderGuid))
                    throw new IOException("Could not create a temporary scene folder.");

                StarterFarmSceneMenu.CreateAt(scenePath, controller =>
                {
                    controller.FindProperty("apiOrigin").stringValue = origin.GetLeftPart(UriPartial.Authority);
                    controller.FindProperty("firebaseWebApiKey").stringValue = firebaseKey;
                    controller.FindProperty("googleMapsBrowserKey").stringValue = mapsKey;
                    controller.ApplyModifiedPropertiesWithoutUndo();
                });
                if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = false;

                string outputPath = Path.Combine(buildRoot, outputName);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    target = target,
                    scenes = new[] { scenePath },
                    locationPathName = outputPath,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Pilot build failed for {target}: {report.summary.result}.");

                Debug.Log($"FarmLand pilot build ready at {outputPath}");
            }
            finally
            {
                if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = previousBundleSetting;
                if (AssetDatabase.IsValidFolder(generatedPath))
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.DeleteAsset(generatedPath);
                }
            }
        }

        private static string Required(string name)
        {
            string value = Environment.GetEnvironmentVariable(name)?.Trim();
            return !string.IsNullOrEmpty(value) ? value
                : throw new InvalidOperationException($"Set {name} before building the pilot.");
        }
    }
}
