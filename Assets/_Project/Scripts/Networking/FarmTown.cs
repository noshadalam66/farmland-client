using System.Collections.Generic;
using Newtonsoft.Json;

namespace FarmLand.Empire.Networking
{
    public sealed class FarmTown
    {
        [JsonProperty("buildings", Required = Required.Always)]
        public TownBuilding[] Buildings { get; private set; }

        [JsonProperty("housingCapacity", Required = Required.Always)]
        public int HousingCapacity { get; private set; }

        [JsonProperty("residents", Required = Required.Always)]
        public int Residents { get; private set; }

        [JsonProperty("catalog", Required = Required.Always)]
        public Dictionary<string, TownBuildingOption> Catalog { get; private set; }
    }

    public sealed class TownBuilding
    {
        [JsonProperty("kind", Required = Required.Always)]
        public string Kind { get; private set; }
    }

    public sealed class TownBuildingOption
    {
        [JsonProperty("price", Required = Required.Always)]
        public int Price { get; private set; }

        [JsonProperty("limit", Required = Required.Always)]
        public int Limit { get; private set; }
    }
}
