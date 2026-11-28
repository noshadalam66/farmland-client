using System;
using FarmLand.Empire.Prototype;
using UnityEditor;
using UnityEngine;

namespace FarmLand.Empire.Editor
{
    internal static class PhaseThreeAcceptance
    {
        internal static void VerifyFarmScene(SerializedObject controller)
        {
            var camera = Required<Camera>(controller, "plotCamera");
            var starter = Required<Collider>(controller, "starterPlotCollider");
            var expansion = Required<Collider>(controller, "expansionPlotCollider");
            var expansionRoot = Required<GameObject>(controller, "expansionGeometry");
            bool expansionWasActive = expansionRoot.activeSelf;
            try
            {
                expansionRoot.SetActive(true);
                Physics.SyncTransforms();
                VerifyPlot(camera, starter);
                VerifyPlot(camera, expansion);
                VerifyCrops(Required<GameObject>(controller, "cropMarker"));
                VerifyCrops(Required<GameObject>(controller, "expansionCropMarker"));
            }
            finally
            {
                expansionRoot.SetActive(expansionWasActive);
            }
        }

        private static void VerifyPlot(Camera camera, Collider plot)
        {
            Vector3 point = camera.WorldToViewportPoint(plot.bounds.center);
            if (point.z <= 0 || point.x <= 0 || point.x >= 1 || point.y <= 0 || point.y >= 1 ||
                !plot.Raycast(camera.ViewportPointToRay(point), out _, camera.farClipPlane))
                throw new InvalidOperationException($"Phase 3 plot is not selectable: {plot.name}.");
        }

        private static void VerifyCrops(GameObject marker)
        {
            if (!marker.TryGetComponent(out PlotCropVisual visual))
                throw new InvalidOperationException($"Phase 3 crop visuals are missing from {marker.name}.");

            var crop = new SerializedObject(visual);
            var wheat = Required<GameObject>(crop, "wheat");
            var corn = Required<GameObject>(crop, "corn");
            var rice = Required<GameObject>(crop, "rice");
            var ready = Required<GameObject>(crop, "readyIndicator");
            if (wheat == corn || wheat == rice || corn == rice)
                throw new InvalidOperationException("Each crop needs its own model.");

            foreach (var part in new[] { wheat, corn, rice, ready })
            {
                if (!part.transform.IsChildOf(marker.transform) ||
                    part.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException($"Missing geometry for {part.name}.");
            }

            // Use a disposable copy so the generated scene keeps its starting state.
            var copy = UnityEngine.Object.Instantiate(marker);
            try
            {
                var copyVisual = copy.GetComponent<PlotCropVisual>();
                var copyState = new SerializedObject(copyVisual);
                var indicator = Required<GameObject>(copyState, "readyIndicator");
                foreach (string kind in new[] { "wheat", "corn", "rice" })
                {
                    var model = Required<GameObject>(copyState, kind);
                    copyVisual.Show(kind, 0);
                    if (indicator.activeSelf || !model.activeSelf || model.transform.localScale.y >= 1)
                        throw new InvalidOperationException($"Invalid growing state for {kind}.");
                    copyVisual.Show(kind, 1);
                    if (!indicator.activeSelf || model.transform.localScale.y != 1)
                        throw new InvalidOperationException($"Invalid harvest state for {kind}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        private static T Required<T>(SerializedObject target, string field) where T : UnityEngine.Object
        {
            if (target.FindProperty(field)?.objectReferenceValue is T value)
                return value;
            throw new InvalidOperationException($"Phase 3 scene is missing {field}.");
        }
    }
}
