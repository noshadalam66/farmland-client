using FarmLand.Empire.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        [SerializeField] private GameObject roadGeometry;
        [SerializeField] private GameObject[] cottageGeometry;

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
            int roads = town == null ? 0 : CountBuildings(town, "road");
            int cottages = town == null ? 0 : CountBuildings(town, "cottage");
            if (roadGeometry != null) SetVisible(roadGeometry, roads > 0);
            if (cottageGeometry != null)
                for (int i = 0; i < cottageGeometry.Length; i++)
                    if (cottageGeometry[i] != null) SetVisible(cottageGeometry[i], i < cottages);
            if (town == null) return;
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

        private static void SetVisible(GameObject building, bool visible)
        {
            if (building.activeSelf != visible) building.SetActive(visible);
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
