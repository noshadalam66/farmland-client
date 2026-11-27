using System;
using FarmLand.Empire.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        private Foldout millPanel;
        private Label millSummary;
        private Button millAction;
        private Button sellFlour;

        private void BuildMillPanel()
        {
            millPanel = new Foldout { text = "Grain mill", value = false };
            controls.Add(millPanel);
            millSummary = new Label();
            millPanel.Add(millSummary);
            millAction = new Button(SubmitMillAction);
            millPanel.Add(millAction);
            sellFlour = new Button(SellFlour);
            millPanel.Add(sellFlour);
        }

        private void RenderMill()
        {
            FarmMill mill = farm?.Mill;
            bool available = mill != null && farm.Wallet != null && farm.Inventory.Flour.HasValue;
            millPanel.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            if (!available) return;

            bool canAct = !busy && !reloadRequired && pending == null;
            double remaining = MillSecondsRemaining(mill);
            string batch = !mill.Owned ? "Mill available to buy"
                : !mill.ReadyAt.HasValue ? "No batch running"
                : remaining > 0 ? $"Batch ready in {Math.Ceiling(remaining)}s" : "Flour ready";
            millSummary.text = $"Flour: {farm.Inventory.Flour} · {batch}";
            millAction.text = !mill.Owned ? $"Buy mill · {mill.Price} coins"
                : mill.ReadyAt.HasValue ? "Collect flour" : $"Mill wheat · {mill.WheatPerBatch} wheat";
            millAction.SetEnabled(canAct && CanUseMill(mill));

            sellFlour.text = $"Sell flour · {mill.FlourPrice} coins";
            sellFlour.SetEnabled(canAct && mill.FlourPrice > 0 && farm.Inventory.Flour > 0);
        }

        private bool CanUseMill(FarmMill mill)
        {
            if (!mill.Owned) return mill.Price > 0 && farm.Wallet.Coins >= mill.Price;
            if (mill.ReadyAt.HasValue) return MillSecondsRemaining(mill) <= 0;
            return mill.WheatPerBatch > 0 && farm.Inventory.Wheat >= mill.WheatPerBatch;
        }

        private double MillSecondsRemaining(FarmMill mill) =>
            mill.ReadyAt.HasValue
                ? (mill.ReadyAt.Value - farm.ServerTime).TotalSeconds - (Time.realtimeSinceStartupAsDouble - receivedAt)
                : 0;

        private void SubmitMillAction()
        {
            FarmMill mill = farm?.Mill;
            if (busy || reloadRequired || pending != null || mill == null || farm.Wallet == null || !CanUseMill(mill))
                return;

            pending = !mill.Owned ? FarmCommand.BuyMill(farm.Revision)
                : mill.ReadyAt.HasValue ? FarmCommand.CollectFlour(farm.Revision)
                : FarmCommand.StartMilling(farm.Revision);
            StartCoroutine(Run(pending));
        }

        private void SellFlour()
        {
            if (busy || reloadRequired || pending != null || farm?.Mill == null ||
                farm.Mill.FlourPrice <= 0 || farm.Inventory.Flour.GetValueOrDefault() < 1)
                return;

            pending = FarmCommand.SellFlour(farm.Revision);
            StartCoroutine(Run(pending));
        }
    }
}
