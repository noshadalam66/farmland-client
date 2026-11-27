using Newtonsoft.Json;

namespace FarmLand.Empire.Networking
{
    public sealed class FarmTownOrders
    {
        [JsonProperty("date", Required = Required.Always)]
        public string Date { get; private set; }

        [JsonProperty("offers", Required = Required.Always)]
        public TownOrderOffer[] Offers { get; private set; }
    }

    public sealed class TownOrderOffer
    {
        [JsonProperty("key", Required = Required.Always)]
        public string Key { get; private set; }

        [JsonProperty("item", Required = Required.Always)]
        public string Item { get; private set; }

        [JsonProperty("quantity", Required = Required.Always)]
        public int Quantity { get; private set; }

        [JsonProperty("reward", Required = Required.Always)]
        public int Reward { get; private set; }

        [JsonProperty("minimumResidents", Required = Required.Always)]
        public int MinimumResidents { get; private set; }

        [JsonProperty("unlocked", Required = Required.Always)]
        public bool Unlocked { get; private set; }

        [JsonProperty("fulfilled", Required = Required.Always)]
        public bool Fulfilled { get; private set; }
    }
}
