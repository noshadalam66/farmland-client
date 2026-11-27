using System.Collections.Generic;
using FarmLand.Empire.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        [SerializeField] private GameObject roadGeometry;
        [SerializeField] private GameObject[] cottageGeometry;
        [SerializeField] private Transform townServiceGeometry;

        private Foldout townPanel;
        private Label townSummary;
        private Label townStatus;
        private Label townServices;
        private readonly Dictionary<string, Button> constructionButtons = new Dictionary<string, Button>();
        private static readonly (string Kind, string Name)[] buildingLabels =
        {
            ("road", "road"), ("cottage", "cottage"), ("water_tower", "water tower"),
            ("workshop", "workshop"), ("school", "school"), ("clinic", "clinic"),
        };

        private void BuildTownPanel()
        {
            townPanel = new Foldout { text = "Settlement", value = false };
            townSummary = new Label();
            townStatus = new Label();
            townStatus.style.whiteSpace = WhiteSpace.Normal;
            townServices = new Label();
            townServices.style.whiteSpace = WhiteSpace.Normal;
            townPanel.Add(townSummary);
            townPanel.Add(townServices);
            constructionButtons.Clear();
            foreach (var entry in buildingLabels)
            {
                string kind = entry.Kind;
                var button = new Button(() => BuildTown(kind));
                constructionButtons.Add(kind, button);
                townPanel.Add(button);
            }
            townPanel.Add(townStatus);
            BuildTownOrdersPanel();
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
            RenderServiceGeometry(town);
            if (town == null) return;
            townSummary.text = $"Residents: {town.Residents}/{town.HousingCapacity} · Cottages: {cottages}";
            townStatus.text = farm.Expansion?.Owned != true ? "Buy the farm expansion to start a settlement."
                : roads == 0 ? "Build a road to unlock cottages."
                : town.Residents < town.HousingCapacity ? "Residents arrive over time. Refresh farm to check arrivals."
                : string.Empty;

            townServices.style.display = town.Served == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (town.Served != null)
                townServices.text = $"Water: {town.Served.Water}/{town.Residents} · Jobs: {town.Served.Jobs}/{town.Residents}\n" +
                    $"School: {town.Served.Education}/{town.Residents} · Clinic: {town.Served.Health}/{town.Residents} · Wellbeing: {town.Wellbeing}%";

            foreach (var entry in buildingLabels)
                RenderBuildingOption(town, entry.Kind, entry.Name, constructionButtons[entry.Kind]);
            RenderTownOrders();
        }

        private void RenderBuildingOption(FarmTown town, string kind, string name, Button button)
        {
            bool available = town.Catalog.TryGetValue(kind, out TownBuildingOption option);
            button.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available) return;
            string prerequisite = RequiredBuilding(kind, option);
            button.text = prerequisite != null && CountBuildings(town, prerequisite) == 0
                ? $"Build {name} · needs a {prerequisite}"
                : $"Build {name} · {option.Price} coins";
            button.SetEnabled(CanConstruct(town, kind, option));
        }

        private bool CanConstruct(FarmTown town, string kind, TownBuildingOption option)
        {
            string prerequisite = RequiredBuilding(kind, option);
            return !busy && !reloadRequired && pending == null && farm.Expansion?.Owned == true &&
                farm.Wallet != null && option.Price > 0 && farm.Wallet.Coins >= option.Price &&
                CountBuildings(town, kind) < option.Limit &&
                (prerequisite == null || CountBuildings(town, prerequisite) > 0);
        }

        private static string RequiredBuilding(string kind, TownBuildingOption option) =>
            option.Requires ?? (kind == "cottage" ? "road" : null);

        private void BuildTown(string kind)
        {
            FarmTown town = farm?.Town;
            if (town == null || !town.Catalog.TryGetValue(kind, out TownBuildingOption option) ||
                !CanConstruct(town, kind, option)) return;
            pending = FarmCommand.BuildTown(farm.Revision, kind);
            StartCoroutine(Run(pending));
        }

        private void RenderServiceGeometry(FarmTown town)
        {
            if (townServiceGeometry == null) return;
            foreach (Transform group in townServiceGeometry)
            {
                int count = town == null ? 0 : CountBuildings(town, group.name);
                for (int i = 0; i < group.childCount; i++)
                    SetVisible(group.GetChild(i).gameObject, i < count);
            }
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
