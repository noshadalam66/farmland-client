using System;
using System.Collections.Generic;
using FarmLand.Empire.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        private Foldout breedingPanel;
        private DropdownField firstParent;
        private DropdownField secondParent;
        private Label breedingStatus;
        private Button breedingAction;

        private void BuildBreedingPanel()
        {
            breedingPanel = new Foldout { text = "Breeding", value = false };
            livestockPanel.Add(breedingPanel);
            firstParent = new DropdownField { label = "First parent" };
            secondParent = new DropdownField { label = "Second parent" };
            breedingPanel.Add(firstParent);
            breedingPanel.Add(secondParent);
            breedingStatus = new Label();
            breedingStatus.style.whiteSpace = WhiteSpace.Normal;
            breedingPanel.Add(breedingStatus);
            breedingAction = new Button(SubmitBreeding);
            breedingPanel.Add(breedingAction);
        }

        private void SetParentChoices(FarmAnimal[] animals, Guid? previousFirst, Guid? previousSecond)
        {
            var labels = new List<string>();
            foreach (FarmAnimal animal in animals) labels.Add(AnimalLabel(animal));
            firstParent.choices = labels;
            secondParent.choices = new List<string>(labels);
            int first = Array.FindIndex(animals, animal => animal.AnimalId == previousFirst);
            int second = Array.FindIndex(animals, animal => animal.AnimalId == previousSecond);
            firstParent.index = first >= 0 ? first : animals.Length > 0 ? 0 : -1;
            secondParent.index = second >= 0 ? second : animals.Length > 1 ? 1 : -1;
        }

        private void RenderBreeding(FarmLivestock livestock, double elapsed, bool canAct)
        {
            bool supported = livestock.Breeding != null ||
                (livestock.Animals.Length > 0 && !string.IsNullOrEmpty(livestock.Animals[0].Sex));
            breedingPanel.style.display = supported ? DisplayStyle.Flex : DisplayStyle.None;
            if (!supported) return;

            FarmBreeding breeding = livestock.Breeding;
            DisplayStyle parentDisplay = breeding == null ? DisplayStyle.Flex : DisplayStyle.None;
            firstParent.style.display = parentDisplay;
            secondParent.style.display = parentDisplay;
            if (breeding != null)
            {
                double remaining = (breeding.ReadyAt - farm.ServerTime).TotalSeconds - elapsed;
                string parents = $"{ShortAnimalId(breeding.MotherId)} and {ShortAnimalId(breeding.FatherId)}";
                breedingStatus.text = remaining > 0 ? $"{parents} · Ready in {Math.Ceiling(remaining)}s"
                    : $"{parents} · Offspring ready";
                breedingAction.text = "Collect offspring";
                breedingAction.SetEnabled(canAct && remaining <= 0);
                return;
            }

            string problem = PairProblem(livestock, elapsed);
            breedingStatus.text = problem ?? "The server checks ancestry before starting.";
            breedingAction.text = "Start breeding";
            breedingAction.SetEnabled(canAct && problem == null);
        }

        private string PairProblem(FarmLivestock livestock, double elapsed)
        {
            if (livestock.Animals.Length < 2) return "Buy two animals of the same species.";
            if (livestock.Animals.Length >= livestock.Capacity) return "The livestock pen is full.";
            FarmAnimal first = AnimalAt(displayedAnimals, firstParent.index);
            FarmAnimal second = AnimalAt(displayedAnimals, secondParent.index);
            if (first == null || second == null || first.AnimalId == second.AnimalId ||
                first.Species != second.Species || first.Sex == second.Sex ||
                string.IsNullOrEmpty(first.Sex) || string.IsNullOrEmpty(second.Sex))
                return "Select two animals of the same species and opposite sexes.";

            DateTimeOffset now = farm.ServerTime.AddSeconds(elapsed);
            if (first.MatureAt > now || second.MatureAt > now) return "Both animals must mature.";
            return null;
        }

        private void SubmitBreeding()
        {
            FarmLivestock livestock = farm?.Livestock;
            if (busy || reloadRequired || pending != null || livestock == null) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - receivedAt;
            if (livestock.Breeding != null)
            {
                if ((livestock.Breeding.ReadyAt - farm.ServerTime).TotalSeconds > elapsed) return;
                pending = FarmCommand.CollectOffspring(farm.Revision);
            }
            else
            {
                if (PairProblem(livestock, elapsed) != null) return;
                pending = FarmCommand.StartBreeding(farm.Revision,
                    displayedAnimals[firstParent.index].AnimalId, displayedAnimals[secondParent.index].AnimalId);
            }
            StartCoroutine(Run(pending));
        }

        private static FarmAnimal AnimalAt(FarmAnimal[] animals, int index) =>
            animals != null && index >= 0 && index < animals.Length ? animals[index] : null;

        private static string ShortAnimalId(Guid id) => id.ToString("N").Substring(0, 8);

        private static string AnimalLabel(FarmAnimal animal) =>
            $"{SpeciesLabel(animal.Species)}{(string.IsNullOrEmpty(animal.Sex) ? "" : $" · {animal.Sex}")} · {ShortAnimalId(animal.AnimalId)}";
    }
}
