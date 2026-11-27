using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        [SerializeField] private string googleMapsBrowserKey;

#if UNITY_WEBGL && !UNITY_EDITOR

        private bool mapOpen;
        private bool previousKeyboardCapture;

        [DllImport("__Internal")] private static extern void FarmlandMapOpen(string key, int areaKm2);
        [DllImport("__Internal")] private static extern int FarmlandMapRead(out double latitude, out double longitude);
        [DllImport("__Internal")] private static extern void FarmlandMapClose();
#endif

        private void AddMapButton()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(googleMapsBrowserKey))
                worldPanel.Add(new Button(OpenPilotMap) { text = "Choose on map" });
#endif
        }

        private void CheckPilotMap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!mapOpen) return;
            int result = FarmlandMapRead(out double lat, out double lon);
            if (result == 0) return;
            ClosePilotMap();
            if (result == 1)
            {
                latitude.value = lat.ToString("R", CultureInfo.InvariantCulture);
                longitude.value = lon.ToString("R", CultureInfo.InvariantCulture);
                worldStatus.text = "Location selected. Request terrain when ready.";
            }
            else if (result == 3)
            {
                worldStatus.text = "Map unavailable. Check the Maps key and connection, or enter coordinates.";
            }
#endif
        }

        private void ClosePilotMap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!mapOpen) return;
            FarmlandMapClose();
            WebGLInput.captureAllKeyboardInput = previousKeyboardCapture;
            mapOpen = false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void OpenPilotMap()
        {
            if (busy || mapOpen) return;
            previousKeyboardCapture = WebGLInput.captureAllKeyboardInput;
            WebGLInput.captureAllKeyboardInput = false;
            mapOpen = true;
            FarmlandMapOpen(googleMapsBrowserKey.Trim(), int.Parse(area.value, CultureInfo.InvariantCulture));
        }
#endif
    }
}
