using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace FarmLand.Empire.Networking
{
    public sealed class FarmState
    {
        [JsonProperty("revision", Required = Required.Always)]
        public int Revision { get; private set; }

        [JsonProperty("serverTime", Required = Required.Always)]
        public DateTimeOffset ServerTime { get; private set; }

        [JsonProperty("inventory", Required = Required.Always)]
        public FarmInventory Inventory { get; private set; }

        [JsonProperty("plot", Required = Required.AllowNull)]
        public FarmPlot Plot { get; private set; }
    }

    public sealed class FarmInventory
    {
        [JsonProperty("wheat", Required = Required.Always)]
        public int Wheat { get; private set; }
    }

    public sealed class FarmPlot
    {
        [JsonProperty("plantedAt", Required = Required.Always)]
        public DateTimeOffset PlantedAt { get; private set; }

        [JsonProperty("readyAt", Required = Required.Always)]
        public DateTimeOffset ReadyAt { get; private set; }
    }

    public sealed class FarmCommand
    {
        private FarmCommand(string action, int expectedRevision)
        {
            if (expectedRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));

            Action = action;
            ExpectedRevision = expectedRevision;
            CommandId = Guid.NewGuid();
        }

        [JsonProperty("commandId")]
        public Guid CommandId { get; }

        [JsonProperty("expectedRevision")]
        public int ExpectedRevision { get; }

        [JsonProperty("action")]
        public string Action { get; }

        public static FarmCommand Plant(int revision) => new FarmCommand("plant", revision);
        public static FarmCommand Harvest(int revision) => new FarmCommand("harvest", revision);
    }

    public sealed class FarmResult
    {
        internal FarmResult(long statusCode, FarmState state, string code)
        {
            StatusCode = statusCode;
            State = state;
            Code = code;
        }

        public long StatusCode { get; }
        public FarmState State { get; }
        public string Code { get; }
        public bool Succeeded => StatusCode == 200 && State != null;
    }

    public sealed class StarterFarmApi
    {
        private readonly string baseUrl;

        public StarterFarmApi(Uri apiOrigin)
        {
            if (apiOrigin == null || !apiOrigin.IsAbsoluteUri)
                throw new ArgumentException("An absolute API origin is required.", nameof(apiOrigin));

            bool supportedScheme = apiOrigin.Scheme == Uri.UriSchemeHttps ||
                                   (apiOrigin.Scheme == Uri.UriSchemeHttp && apiOrigin.IsLoopback);
            if (!supportedScheme || apiOrigin.UserInfo.Length != 0 || apiOrigin.PathAndQuery != "/" ||
                apiOrigin.Fragment.Length != 0)
                throw new ArgumentException("Use an HTTPS origin or a loopback HTTP origin for local development.", nameof(apiOrigin));

            baseUrl = apiOrigin.GetLeftPart(UriPartial.Authority);
        }

        public IEnumerator Load(string idToken, Action<FarmResult> complete)
        {
            return Send(UnityWebRequest.kHttpVerbGET, "/v1/farm", idToken, null, complete);
        }

        public IEnumerator Submit(FarmCommand command, string idToken, Action<FarmResult> complete)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            return Send(UnityWebRequest.kHttpVerbPOST, "/v1/farm/commands", idToken,
                JsonConvert.SerializeObject(command), complete);
        }

        private IEnumerator Send(string method, string path, string idToken, string body, Action<FarmResult> complete)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                throw new ArgumentException("A Firebase ID token is required.", nameof(idToken));
            if (complete == null)
                throw new ArgumentNullException(nameof(complete));

            using var request = new UnityWebRequest(baseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                redirectLimit = 0,
                timeout = 15,
            };
            request.SetRequestHeader("Authorization", "Bearer " + idToken);
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            yield return request.SendWebRequest();
            complete(ReadResult(request));
        }

        private static FarmResult ReadResult(UnityWebRequest request)
        {
            long status = request.responseCode;
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
                return new FarmResult(status, null, "transport_error");

            try
            {
                if (status == 200)
                {
                    FarmState state = JsonConvert.DeserializeObject<FarmState>(request.downloadHandler.text);
                    return new FarmResult(status, state, state == null ? "invalid_response" : null);
                }

                if (status == 409)
                {
                    var conflict = JsonConvert.DeserializeObject<ConflictResponse>(request.downloadHandler.text);
                    return conflict == null
                        ? new FarmResult(status, null, "invalid_response")
                        : new FarmResult(status, conflict.State, conflict.Code);
                }
            }
            catch (JsonException)
            {
                return new FarmResult(status, null, "invalid_response");
            }

            return new FarmResult(status, null, status == 401 ? "unauthorized" : "request_failed");
        }

        private sealed class ConflictResponse
        {
            [JsonProperty("code", Required = Required.Always)]
            public string Code { get; private set; }

            [JsonProperty("state", Required = Required.Always)]
            public FarmState State { get; private set; }
        }
    }
}
