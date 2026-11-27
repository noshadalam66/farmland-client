using System;
using System.Collections.Generic;
using FarmLand.Empire.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        private Foldout livestockPanel;
        private Label livestockSummary;
        private ScrollView animalList;
        private FarmAnimal[] displayedAnimals;
        private readonly Dictionary<string, Button> animalPurchases = new Dictionary<string, Button>();
        private readonly List<(FarmAnimal Animal, Label Status, Button Action)> animalRows =
            new List<(FarmAnimal, Label, Button)>();

        private void BuildLivestockPanel()
        {
            livestockPanel = new Foldout { text = "Livestock", value = false };
            controls.Add(livestockPanel);
            livestockSummary = new Label();
            livestockPanel.Add(livestockSummary);
            animalPurchases.Clear();
            foreach (string species in new[] { "goat", "chicken" })
            {
                var button = new Button(() => BuyAnimal(species));
                animalPurchases.Add(species, button);
                livestockPanel.Add(button);
            }

            animalList = new ScrollView();
            animalList.style.maxHeight = 180;
            livestockPanel.Add(animalList);
            displayedAnimals = null;
            animalRows.Clear();
        }

        private void RenderLivestock()
        {
            FarmLivestock livestock = farm?.Livestock;
            bool available = livestock != null && farm.Wallet != null;
            livestockPanel.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available) return;

            bool canAct = !busy && !reloadRequired && pending == null;
            bool canProduce = farm.Inventory.Milk.HasValue && farm.Inventory.Eggs.HasValue;
            livestockSummary.text = canProduce
                ? $"{livestock.Animals.Length}/{livestock.Capacity} animals · Milk: {farm.Inventory.Milk} · Eggs: {farm.Inventory.Eggs}"
                : $"{livestock.Animals.Length}/{livestock.Capacity} animals";
            foreach (var entry in animalPurchases)
            {
                int price = livestock.Prices.TryGetValue(entry.Key, out int cost) ? cost : 0;
                entry.Value.text = $"Buy {SpeciesLabel(entry.Key)} · {price} coins";
                entry.Value.SetEnabled(canAct && price > 0 && farm.Wallet.Coins >= price &&
                    livestock.Animals.Length < livestock.Capacity);
            }

            if (!ReferenceEquals(displayedAnimals, livestock.Animals)) RebuildAnimalRows(livestock.Animals);
            double elapsed = Time.realtimeSinceStartupAsDouble - receivedAt;
            foreach (var row in animalRows)
            {
                bool fed = row.Animal.ProductReadyAt.HasValue;
                double remaining = fed ? (row.Animal.ProductReadyAt.Value - farm.ServerTime).TotalSeconds - elapsed : 0;
                row.Status.text = !canProduce ? string.Empty : !fed ? "Needs feed"
                    : remaining > 0 ? $"Ready in {Math.Ceiling(remaining)}s" : "Ready to collect";
                row.Action.style.display = canProduce ? DisplayStyle.Flex : DisplayStyle.None;
                row.Action.text = fed ? "Collect product" : "Feed · 1 wheat";
                row.Action.SetEnabled(canAct && (fed ? remaining <= 0 : farm.Inventory.Wheat > 0));
            }
        }

        private void RebuildAnimalRows(FarmAnimal[] animals)
        {
            displayedAnimals = animals;
            animalList.Clear();
            animalRows.Clear();
            foreach (FarmAnimal animal in animals)
            {
                string identity = animal.AnimalId.ToString("N").Substring(0, 8);
                animalList.Add(new Label($"{SpeciesLabel(animal.Species)} {identity}"));
                var state = new Label();
                var action = new Button(() => CareForAnimal(animal));
                animalList.Add(state);
                animalList.Add(action);
                animalRows.Add((animal, state, action));
            }
        }

        private void BuyAnimal(string species)
        {
            FarmLivestock livestock = farm?.Livestock;
            if (busy || reloadRequired || pending != null || livestock == null || farm.Wallet == null ||
                livestock.Animals.Length >= livestock.Capacity ||
                !livestock.Prices.TryGetValue(species, out int price) || price <= 0 || farm.Wallet.Coins < price)
                return;

            pending = FarmCommand.BuyAnimal(farm.Revision, species);
            StartCoroutine(Run(pending));
        }

        private void CareForAnimal(FarmAnimal animal)
        {
            if (busy || reloadRequired || pending != null || farm?.Livestock == null ||
                !farm.Inventory.Milk.HasValue || !farm.Inventory.Eggs.HasValue) return;

            bool collect = animal.ProductReadyAt.HasValue;
            if (!collect && farm.Inventory.Wheat < 1) return;
            if (collect && (animal.ProductReadyAt.Value - farm.ServerTime).TotalSeconds >
                Time.realtimeSinceStartupAsDouble - receivedAt) return;
            pending = FarmCommand.CareForAnimal(farm.Revision, animal.AnimalId, collect);
            StartCoroutine(Run(pending));
        }

        private static string SpeciesLabel(string species) => species == "goat" ? "Goat" : "Chicken";
    }
}
