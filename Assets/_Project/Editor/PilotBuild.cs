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
        public static void Build() => BuildPlayer(false);

        public static void BuildRelease() => BuildPlayer(true);

        private static void BuildPlayer(bool release)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run the build from Unity batch mode.");

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string outputName = target switch
            {
                BuildTarget.WebGL => "web",
                BuildTarget.Android => release ? "FarmLandEmpire.aab" : "FarmLandPilot.apk",
                BuildTarget.iOS => "ios-xcode",
                _ => throw new InvalidOperationException("Pass -buildTarget WebGL, Android, or iOS."),
            };
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException($"Install Unity build support for {target}.");

            string apiOrigin = Required("FARMLAND_API_ORIGIN");
            if (!Uri.TryCreate(apiOrigin, UriKind.Absolute, out Uri origin))
                throw new InvalidOperationException("FARMLAND_API_ORIGIN must be an absolute origin.");
            _ = new StarterFarmApi(origin);
            if (release && (origin.Scheme != Uri.UriSchemeHttps || origin.IsLoopback))
                throw new InvalidOperationException("Release builds require a non-loopback HTTPS API origin.");
            string firebaseKey = Required("FARMLAND_FIREBASE_WEB_API_KEY");
            string mapsKey = target == BuildTarget.WebGL
                ? Environment.GetEnvironmentVariable("FARMLAND_GOOGLE_MAPS_BROWSER_KEY")?.Trim() ?? ""
                : "";

            string runId = Guid.NewGuid().ToString("N");
            string buildType = release ? "Release" : "Pilot";
            string generatedName = "_FarmLandBuild_" + runId;
            string generatedPath = "Assets/" + generatedName;
            string scenePath = generatedPath + "/FarmLand.unity";
            string buildRoot = Path.GetFullPath(Path.Combine("Builds", buildType, target.ToString(), runId));
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
                if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = release;

                string outputPath = Path.Combine(buildRoot, outputName);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    target = target,
                    scenes = new[] { scenePath },
                    locationPathName = outputPath,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"FarmLand {buildType} build failed for {target}: {report.summary.result}.");

                Debug.Log($"FarmLand {buildType} build ready at {outputPath}");
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
