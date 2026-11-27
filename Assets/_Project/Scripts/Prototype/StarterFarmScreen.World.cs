using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FarmLand.Empire.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        [SerializeField] private GameObject fixedFarm;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material waterMaterial;

        private VisualElement worldPanel;
        private TextField latitude;
        private TextField longitude;
        private DropdownField area;
        private Label worldStatus;
        private GameObject terrainView;
        private string selectionKey;
        private Guid selectionId;
        private string failedWorldRequestId;
        private const double WorldPollSeconds = 5;
        private const double WorldRetrySeconds = 15;
        private double? nextWorldCheck;

        private void BuildWorldControls()
        {
            if (fixedFarm == null || terrainMaterial == null) return;
            controls.Add(new Button(ToggleWorld) { text = "Pilot world" });
            worldPanel = new VisualElement();
            worldPanel.style.display = DisplayStyle.None;
            controls.Add(worldPanel);
            latitude = new TextField("Latitude");
            longitude = new TextField("Longitude");
            area = new DropdownField("Area (km²)", new List<string> { "1", "4", "9" }, 0);
            worldPanel.Add(latitude);
            worldPanel.Add(longitude);
            worldPanel.Add(area);
            AddMapButton();
            worldPanel.Add(new Button(SubmitWorld) { text = "Request terrain" });
            worldPanel.Add(new Button(() => StartCoroutine(RunWorld(null))) { text = "Check world" });
            worldStatus = new Label();
            worldStatus.style.whiteSpace = WhiteSpace.Normal;
            worldPanel.Add(worldStatus);
        }

        private void ToggleWorld()
        {
            if (busy) return;
            if (worldPanel.style.display.value == DisplayStyle.None)
            {
                worldPanel.style.display = DisplayStyle.Flex;
                StartCoroutine(RunWorld(null));
            }
            else
            {
                worldPanel.style.display = DisplayStyle.None;
                ClearTerrainView();
            }
        }

        private void PollWorldGeneration()
        {
            if (!nextWorldCheck.HasValue || Time.realtimeSinceStartupAsDouble < nextWorldCheck.Value ||
                busy || pending != null || reloadRequired || session?.SignedIn != true ||
                worldPanel?.style.display.value != DisplayStyle.Flex) return;

            StartCoroutine(RunWorld(null, automatic: true));
        }

        private void SubmitWorld()
        {
            if (busy) return;
            if (!double.TryParse(latitude.value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                !double.TryParse(longitude.value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon) ||
                !(lat >= -80 && lat <= 80 && lon >= -180 && lon < 180))
            {
                worldStatus.text = "Enter latitude from −80 to 80 and longitude from −180 to less than 180. Use a decimal point.";
                return;
            }
            int areaKm2 = int.Parse(area.value, CultureInfo.InvariantCulture);
            string key = FormattableString.Invariant($"{lat:R}/{lon:R}/{areaKm2}/{failedWorldRequestId}");
            if (selectionKey != key)
            {
                selectionKey = key;
                selectionId = Guid.NewGuid();
            }
            var payload = new JObject
            {
                ["requestId"] = selectionId.ToString(),
                ["latitude"] = lat,
                ["longitude"] = lon,
                ["areaKm2"] = areaKm2,
            };
            if (failedWorldRequestId != null)
                payload["replaceFailedRequestId"] = failedWorldRequestId;
            StartCoroutine(RunWorld(payload.ToString(Formatting.None)));
        }

        private IEnumerator RunWorld(string payload, bool automatic = false)
        {
            if (busy || session?.SignedIn != true) yield break;
            busy = true;
            bool tracking = nextWorldCheck.HasValue;
            nextWorldCheck = null;
            if (!automatic)
                worldStatus.text = payload == null ? "Checking world…" : "Requesting terrain…";
            Render();
            try
            {
                byte[] response = null;
                long code = 0;
                yield return WorldRequest("/v1/world", payload, (statusCode, bytes) => { code = statusCode; response = bytes; });

                if (code == 404)
                {
                    failedWorldRequestId = null;
                    worldStatus.text = "No world selected. Enter the center of the pilot area.";
                    yield break;
                }

                if (code != 200 && code != 201 && code != 409)
                {
                    if (tracking && payload == null && (code == 0 || code == 429 || code >= 500))
                    {
                        nextWorldCheck = Time.realtimeSinceStartupAsDouble + WorldRetrySeconds;
                        worldStatus.text = "Could not check terrain progress. Retrying automatically.";
                    }
                    else worldStatus.text = "Could not reach the world service. Try again.";
                    yield break;
                }

                JObject world;
                try
                {
                    var result = JObject.Parse(Encoding.UTF8.GetString(response));
                    world = code == 409 ? result["world"] as JObject : result;
                }
                catch (JsonException)
                {
                    world = null;
                }

                string worldState = (string)world?["status"];
                failedWorldRequestId = worldState == "failed" && Guid.TryParse((string)world["requestId"], out Guid failedId)
                    ? failedId.ToString() : null;

                switch (worldState)
                {
                    case "queued":
                        nextWorldCheck = Time.realtimeSinceStartupAsDouble + WorldPollSeconds;
                        worldStatus.text = "Terrain request queued.";
                        break;
                    case "generating":
                        nextWorldCheck = Time.realtimeSinceStartupAsDouble + WorldPollSeconds;
                        worldStatus.text = "Generating terrain…";
                        break;
                    case "ready":
                        if (terrainView == null) yield return LoadTerrainView();
                        else worldStatus.text = "Terrain loaded.";
                        break;
                    case "failed":
                        worldStatus.text = "Terrain generation failed. Choose a location and request terrain again.";
                        break;
                    default:
                        worldStatus.text = "Could not read the world status. Try again.";
                        break;
                }
            }
            finally
            {
                busy = false;
                Render();
            }
        }

        private IEnumerator LoadTerrainView()
        {
            worldStatus.text = "Downloading terrain…";
            byte[] manifest = null;
            yield return WorldRequest("/v1/world/terrain/manifest.json", null,
                (code, bytes) => { if (code == 200) manifest = bytes; });

            if (manifest == null)
            {
                worldStatus.text = "Terrain files are unavailable. Check again.";
                yield break;
            }

            TerrainPackage package = null;
            try
            {
                package = TerrainPackage.Parse(Encoding.UTF8.GetString(manifest));
            }
            catch (Exception error) when (error is JsonException || error is IOException ||
                                           error is InvalidCastException || error is FormatException || error is OverflowException)
            {
                worldStatus.text = "The terrain manifest is invalid.";
            }
            if (package == null) yield break;

            var view = new GameObject("Pilot Terrain");
            view.transform.SetParent(transform, false);
            var meshes = view.AddComponent<TerrainMeshLifetime>();
            bool completed = false;
            try
            {
                for (int y = 0; y < package.ChunksPerAxis; y++)
                {
                    for (int x = 0; x < package.ChunksPerAxis; x++)
                    {
                        byte[] chunk = null;
                        string filename = TerrainPackage.ChunkFilename(x, y);
                        yield return WorldRequest("/v1/world/terrain/" + filename, null,
                            (code, bytes) => { if (code == 200) chunk = bytes; });
                        byte[] water = null;
                        if (package.HasWaterMask && waterMaterial != null)
                        {
                            yield return WorldRequest("/v1/world/terrain/" + TerrainPackage.WaterFilename(x, y),
                                null, (code, bytes) => { if (code == 200) water = bytes; });
                            if (water == null)
                            {
                                worldStatus.text = "A terrain water mask is unavailable. Check again.";
                                yield break;
                            }
                        }

                        Mesh mesh = null;
                        try
                        {
                            mesh = package.CreateChunk(x, y, chunk, water);
                        }
                        catch (InvalidDataException)
                        {
                            worldStatus.text = "A terrain chunk is missing or invalid. Check again.";
                        }
                        if (mesh == null) yield break;

                        meshes.Add(mesh);
                        var tile = new GameObject(filename, typeof(MeshRenderer), typeof(MeshFilter));
                        tile.transform.SetParent(view.transform, false);
                        tile.GetComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = tile.GetComponent<MeshRenderer>();
                        renderer.enabled = false;
                        if (water == null) renderer.sharedMaterial = terrainMaterial;
                        else renderer.sharedMaterials = new[] { terrainMaterial, waterMaterial };
                    }
                }

                float scale = 8f / (1000f * package.ChunksPerAxis);
                view.transform.localScale = Vector3.one * scale;
                Bounds bounds = view.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh.bounds;
                for (int i = 1; i < view.transform.childCount; i++)
                    bounds.Encapsulate(view.transform.GetChild(i).GetComponent<MeshFilter>().sharedMesh.bounds);
                view.transform.position = Vector3.down * (bounds.center.y * scale);
                terrainView = view;
                completed = true;
                fixedFarm.SetActive(false);
                foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>())
                    renderer.enabled = true;
                worldStatus.text = "Terrain loaded.";
            }
            finally
            {
                if (!completed) Destroy(view);
            }
        }

        private IEnumerator WorldRequest(string path, string payload, Action<long, byte[]> complete)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                string token = null;
                yield return session.GetToken(attempt == 1, value => token = value);
                if (token == null) break;

                using var request = new UnityWebRequest(worldOrigin + path,
                    payload == null ? UnityWebRequest.kHttpVerbGET : UnityWebRequest.kHttpVerbPOST)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    redirectLimit = 0,
                    timeout = 15,
                };
                request.SetRequestHeader("Authorization", "Bearer " + token);
                if (payload != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                yield return request.SendWebRequest();
                if (request.responseCode == 401 && attempt == 0) continue;
                complete(request.responseCode, request.downloadHandler.data ?? Array.Empty<byte>());
                yield break;
            }
            complete(401, Array.Empty<byte>());
        }

        private void ClearTerrainView()
        {
            nextWorldCheck = null;
            ClosePilotMap();
            if (terrainView != null) Destroy(terrainView);
            terrainView = null;
            if (fixedFarm != null) fixedFarm.SetActive(true);
        }
    }
}
