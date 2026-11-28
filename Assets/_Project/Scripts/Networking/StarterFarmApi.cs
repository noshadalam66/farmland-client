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

        [JsonProperty("wallet")]
        public FarmWallet Wallet { get; private set; }

        [JsonProperty("cropGrowthMilliseconds")]
        public System.Collections.Generic.Dictionary<string, int> CropGrowthMilliseconds { get; private set; }

        [JsonProperty("market")]
        public FarmMarket Market { get; private set; }

        [JsonProperty("expansion")]
        public FarmExpansion Expansion { get; private set; }

        [JsonProperty("livestock")]
        public FarmLivestock Livestock { get; private set; }

        [JsonProperty("mill")]
        public FarmMill Mill { get; private set; }

        [JsonProperty("town")]
        public FarmTown Town { get; private set; }

        [JsonProperty("townOrders")]
        public FarmTownOrders TownOrders { get; private set; }
    }

    public sealed class FarmMill
    {
        [JsonProperty("owned", Required = Required.Always)]
        public bool Owned { get; private set; }

        [JsonProperty("price", Required = Required.Always)]
        public int Price { get; private set; }

        [JsonProperty("readyAt", Required = Required.AllowNull)]
        public DateTimeOffset? ReadyAt { get; private set; }

        [JsonProperty("wheatPerBatch", Required = Required.Always)]
        public int WheatPerBatch { get; private set; }

        [JsonProperty("flourPrice", Required = Required.Always)]
        public int FlourPrice { get; private set; }
    }

    public sealed class FarmLivestock
    {
        [JsonProperty("animals", Required = Required.Always)]
        public FarmAnimal[] Animals { get; private set; }

        [JsonProperty("prices", Required = Required.Always)]
        public System.Collections.Generic.Dictionary<string, int> Prices { get; private set; }

        [JsonProperty("capacity", Required = Required.Always)]
        public int Capacity { get; private set; }

        [JsonProperty("breeding")]
        public FarmBreeding Breeding { get; private set; }
    }

    public sealed class FarmBreeding
    {
        [JsonProperty("readyAt", Required = Required.Always)]
        public DateTimeOffset ReadyAt { get; private set; }

        [JsonProperty("motherId", Required = Required.Always)]
        public Guid MotherId { get; private set; }

        [JsonProperty("fatherId", Required = Required.Always)]
        public Guid FatherId { get; private set; }
    }

    public sealed class FarmAnimal
    {
        [JsonProperty("animalId", Required = Required.Always)]
        public Guid AnimalId { get; private set; }

        [JsonProperty("species", Required = Required.Always)]
        public string Species { get; private set; }

        [JsonProperty("productReadyAt")]
        public DateTimeOffset? ProductReadyAt { get; private set; }

        [JsonProperty("sex")]
        public string Sex { get; private set; }

        [JsonProperty("motherId")]
        public Guid? MotherId { get; private set; }

        [JsonProperty("fatherId")]
        public Guid? FatherId { get; private set; }

        [JsonProperty("matureAt")]
        public DateTimeOffset? MatureAt { get; private set; }
    }

    public sealed class FarmExpansion
    {
        [JsonProperty("owned", Required = Required.Always)]
        public bool Owned { get; private set; }

        [JsonProperty("price", Required = Required.Always)]
        public int Price { get; private set; }

        [JsonProperty("plot", Required = Required.AllowNull)]
        public FarmPlot Plot { get; private set; }
    }

    public sealed class FarmWallet
    {
        [JsonProperty("coins", Required = Required.Always)]
        public int Coins { get; private set; }
    }

    public sealed class FarmMarket
    {
        [JsonProperty("sellPrices", Required = Required.Always)]
        public FarmInventory SellPrices { get; private set; }
    }

    public sealed class FarmInventory
    {
        [JsonProperty("wheat", Required = Required.Always)]
        public int Wheat { get; private set; }

        [JsonProperty("corn")]
        public int? Corn { get; private set; }

        [JsonProperty("rice")]
        public int? Rice { get; private set; }

        [JsonProperty("milk")]
        public int? Milk { get; private set; }

        [JsonProperty("eggs")]
        public int? Eggs { get; private set; }

        [JsonProperty("flour")]
        public int? Flour { get; private set; }

        public bool HasCropVariety => Corn.HasValue && Rice.HasValue;

        public int Count(string crop) => crop switch
        {
            "wheat" => Wheat,
            "corn" => Corn ?? 0,
            "rice" => Rice ?? 0,
            _ => 0,
        };
    }

    public sealed class FarmPlot
    {
        [JsonProperty("crop")]
        public string Crop { get; private set; } = "wheat";

        [JsonProperty("plantedAt", Required = Required.Always)]
        public DateTimeOffset PlantedAt { get; private set; }

        [JsonProperty("readyAt", Required = Required.Always)]
        public DateTimeOffset ReadyAt { get; private set; }
    }

    public sealed class FarmCommand
    {
        private FarmCommand(string action, int expectedRevision, string crop = null, string plotId = null, string species = null, Guid? animalId = null, Guid? partnerId = null, string building = null, string orderKey = null, string orderDate = null)
        {
            if (expectedRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));

            Action = action;
            Crop = crop;
            PlotId = plotId;
            Species = species;
            AnimalId = animalId;
            PartnerId = partnerId;
            Building = building;
            OrderKey = orderKey;
            OrderDate = orderDate;
            ExpectedRevision = expectedRevision;
            CommandId = Guid.NewGuid();
        }

        [JsonProperty("commandId")]
        public Guid CommandId { get; }

        [JsonProperty("expectedRevision")]
        public int ExpectedRevision { get; }

        [JsonProperty("action")]
        public string Action { get; }

        [JsonProperty("crop", NullValueHandling = NullValueHandling.Ignore)]
        public string Crop { get; }

        [JsonProperty("plotId", NullValueHandling = NullValueHandling.Ignore)]
        public string PlotId { get; }

        public static FarmCommand Plant(int revision, string crop = null, bool expansion = false)
        {
            if (crop != null && crop != "wheat" && crop != "corn" && crop != "rice")
                throw new ArgumentOutOfRangeException(nameof(crop));

            return new FarmCommand("plant", revision, crop, expansion ? "expansion" : null);
        }
        public static FarmCommand Harvest(int revision, bool expansion = false) =>
            new FarmCommand("harvest", revision, plotId: expansion ? "expansion" : null);

        public static FarmCommand BuyParcel(int revision) => new FarmCommand("buy_parcel", revision);

        public static FarmCommand BuyMill(int revision) => new FarmCommand("buy_mill", revision);
        public static FarmCommand StartMilling(int revision) => new FarmCommand("start_milling", revision);
        public static FarmCommand CollectFlour(int revision) => new FarmCommand("collect_flour", revision);
        public static FarmCommand SellFlour(int revision) => new FarmCommand("sell_flour", revision);

        [JsonProperty("species", NullValueHandling = NullValueHandling.Ignore)]
        public string Species { get; }

        [JsonProperty("animalId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? AnimalId { get; }

        [JsonProperty("partnerId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? PartnerId { get; }

        public static FarmCommand StartBreeding(int revision, Guid first, Guid second) =>
            new FarmCommand("start_breeding", revision, animalId: first, partnerId: second);

        public static FarmCommand CollectOffspring(int revision) => new FarmCommand("collect_offspring", revision);

        [JsonProperty("building", NullValueHandling = NullValueHandling.Ignore)]
        public string Building { get; }

        [JsonProperty("orderKey", NullValueHandling = NullValueHandling.Ignore)]
        public string OrderKey { get; }

        [JsonProperty("orderDate", NullValueHandling = NullValueHandling.Ignore)]
        public string OrderDate { get; }

        public static FarmCommand FulfilTownOrder(int revision, string key, string date) =>
            new FarmCommand("fulfil_town_order", revision, orderKey: key, orderDate: date);

        public static FarmCommand BuildTown(int revision, string building)
        {
            if (building != "road" && building != "cottage" && building != "water_tower" &&
                building != "workshop" && building != "school" && building != "clinic")
                throw new ArgumentOutOfRangeException(nameof(building));
            return new FarmCommand("build_town", revision, building: building);
        }

        public static FarmCommand BuyAnimal(int revision, string species)
        {
            if (species != "goat" && species != "chicken")
                throw new ArgumentOutOfRangeException(nameof(species));

            return new FarmCommand("buy_animal", revision, species: species);
        }

        public static FarmCommand CareForAnimal(int revision, Guid animalId, bool collect) =>
            new FarmCommand(collect ? "collect_animal" : "feed_animal", revision, animalId: animalId);

        public static FarmCommand Sell(int revision, string crop)
        {
            if (crop != "wheat" && crop != "corn" && crop != "rice")
                throw new ArgumentOutOfRangeException(nameof(crop));

            return new FarmCommand("sell", revision, crop);
        }
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

        public IEnumerator LoadHarvestLeaderboard(string idToken, Action<long, HarvestLeaderboard> complete)
        {
            return SendRequest(UnityWebRequest.kHttpVerbGET, "/v1/leaderboard/harvests", idToken, null, request =>
            {
                HarvestLeaderboard board = null;
                if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
                {
                    try { board = JsonConvert.DeserializeObject<HarvestLeaderboard>(request.downloadHandler.text); }
                    catch (JsonException) { }
                }
                complete(request.responseCode, board);
            });
        }

        public IEnumerator SetLeaderboardVisibility(string idToken, bool enabled, Action<long> complete)
        {
            string body = enabled ? "{\"enabled\":true}" : "{\"enabled\":false}";
            return SendRequest(UnityWebRequest.kHttpVerbPUT, "/v1/leaderboard/preferences", idToken, body,
                request => complete(request.responseCode));
        }

        private IEnumerator Send(string method, string path, string idToken, string body, Action<FarmResult> complete)
        {
            if (complete == null) throw new ArgumentNullException(nameof(complete));
            return SendRequest(method, path, idToken, body, request => complete(ReadResult(request)));
        }

        private IEnumerator SendRequest(string method, string path, string idToken, string body,
            Action<UnityWebRequest> complete)
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
            complete(request);
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
