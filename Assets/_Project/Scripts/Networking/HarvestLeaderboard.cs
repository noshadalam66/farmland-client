using Newtonsoft.Json;

namespace FarmLand.Empire.Networking
{
    public sealed class HarvestLeaderboard
    {
        [JsonProperty("enabled", Required = Required.Always)]
        public bool Enabled { get; private set; }

        [JsonProperty("entries", Required = Required.Always)]
        public HarvestRank[] Entries { get; private set; }

        [JsonProperty("yourRank", Required = Required.AllowNull)]
        public HarvestRank YourRank { get; private set; }
    }

    public sealed class HarvestRank
    {
        [JsonProperty("rank", Required = Required.Always)]
        public int Rank { get; private set; }

        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; private set; }

        [JsonProperty("harvests", Required = Required.Always)]
        public long Harvests { get; private set; }

        [JsonProperty("isYou", Required = Required.Always)]
        public bool IsYou { get; private set; }
    }
}
