using FarmLand.Empire.Networking;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        private Foldout townPanel;
        private Label townSummary;
        private Label townStatus;
        private Button buildRoad;
        private Button buildCottage;

        private void BuildTownPanel()
        {
            townPanel = new Foldout { text = "Settlement", value = false };
            townSummary = new Label();
            townStatus = new Label();
            townStatus.style.whiteSpace = WhiteSpace.Normal;
            buildRoad = new Button(() => BuildTown("road"));
            buildCottage = new Button(() => BuildTown("cottage"));
            townPanel.Add(townSummary);
            townPanel.Add(buildRoad);
            townPanel.Add(buildCottage);
            townPanel.Add(townStatus);
            controls.Add(townPanel);
        }

        private void RenderTown()
        {
            FarmTown town = farm?.Town;
            townPanel.style.display = town == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (town == null) return;

            int roads = CountBuildings(town, "road");
            int cottages = CountBuildings(town, "cottage");
            townSummary.text = $"Residents: {town.Residents}/{town.HousingCapacity} · Cottages: {cottages}";
            townStatus.text = farm.Expansion?.Owned != true ? "Buy the farm expansion to start a settlement."
                : roads == 0 ? "Build a road to unlock cottages."
                : town.Residents < town.HousingCapacity ? "Residents arrive over time. Refresh farm to check arrivals."
                : string.Empty;

            RenderBuildingOption(town, "road", "road", buildRoad);
            RenderBuildingOption(town, "cottage", "cottage", buildCottage);
        }

        private void RenderBuildingOption(FarmTown town, string kind, string name, Button button)
        {
            bool available = town.Catalog.TryGetValue(kind, out TownBuildingOption option);
            button.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available) return;
            button.text = $"Build {name} · {option.Price} coins";
            button.SetEnabled(CanConstruct(town, kind, option));
        }

        private bool CanConstruct(FarmTown town, string kind, TownBuildingOption option) =>
            !busy && !reloadRequired && pending == null && farm.Expansion?.Owned == true &&
            farm.Wallet != null && option.Price > 0 && farm.Wallet.Coins >= option.Price &&
            CountBuildings(town, kind) < option.Limit && (kind != "cottage" || CountBuildings(town, "road") > 0);

        private void BuildTown(string kind)
        {
            FarmTown town = farm?.Town;
            if (town == null || !town.Catalog.TryGetValue(kind, out TownBuildingOption option) ||
                !CanConstruct(town, kind, option)) return;
            pending = FarmCommand.BuildTown(farm.Revision, kind);
            StartCoroutine(Run(pending));
        }

        private static int CountBuildings(FarmTown town, string kind)
        {
            int count = 0;
            foreach (TownBuilding building in town.Buildings)
                if (building.Kind == kind) count++;
            return count;
        }
    }
}
