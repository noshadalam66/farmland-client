using System;
using System.Collections.Generic;
using FarmLand.Empire.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        private const double TownOrderRefreshRetrySeconds = 30;
        private double nextTownOrderRefresh;
        private Foldout townOrdersPanel;
        private Label townOrdersDate;
        private VisualElement townOrderRows;
        private TownOrderOffer[] displayedOrders;
        private readonly List<(TownOrderOffer Offer, Button Button)> orderButtons =
            new List<(TownOrderOffer, Button)>();

        private void BuildTownOrdersPanel()
        {
            townOrdersPanel = new Foldout { text = "Daily supply orders", value = false };
            townOrdersDate = new Label();
            townOrderRows = new VisualElement();
            townOrdersPanel.Add(townOrdersDate);
            townOrdersPanel.Add(townOrderRows);
            townPanel.Add(townOrdersPanel);
            displayedOrders = null;
            orderButtons.Clear();
        }

        private void RenderTownOrders()
        {
            FarmTownOrders orders = farm.TownOrders;
            townOrdersPanel.style.display = orders == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (orders == null) return;

            townOrdersDate.text = TownOrderDayEnded()
                ? "A new order day has started. Refresh for the latest offers."
                : $"{orders.Date} UTC";
            if (!ReferenceEquals(orders.Offers, displayedOrders))
            {
                displayedOrders = orders.Offers;
                townOrderRows.Clear();
                orderButtons.Clear();
                foreach (TownOrderOffer offer in orders.Offers)
                {
                    var button = new Button(() => FulfilTownOrder(offer.Key));
                    townOrderRows.Add(button);
                    orderButtons.Add((offer, button));
                }
            }

            foreach (var row in orderButtons)
            {
                TownOrderOffer offer = row.Offer;
                string title = $"{offer.Quantity} {offer.Item} · {offer.Reward} coins";
                row.Button.text = offer.Fulfilled ? $"{title} · Filled"
                    : offer.Unlocked ? title : $"{title} · Needs {offer.MinimumResidents} residents";
                row.Button.SetEnabled(CanFulfilOrder(offer));
            }
        }

        private bool TownOrderDayEnded()
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - receivedAt;
            DateTimeOffset utc = farm.ServerTime.ToUniversalTime();
            return elapsed >= (utc.Date.AddDays(1) - utc.UtcDateTime).TotalSeconds;
        }

        private void RefreshExpiredTownOrders()
        {
            if (farm?.TownOrders == null || townOrdersPanel?.value != true ||
                busy || reloadRequired || pending != null || session?.SignedIn != true) return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextTownOrderRefresh || !TownOrderDayEnded()) return;

            nextTownOrderRefresh = now + TownOrderRefreshRetrySeconds;
            StartCoroutine(Run(null));
        }

        private bool CanFulfilOrder(TownOrderOffer offer)
        {
            if (busy || reloadRequired || pending != null || farm?.TownOrders == null ||
                TownOrderDayEnded() || !offer.Unlocked || offer.Fulfilled || offer.Quantity <= 0) return false;
            int stock = offer.Item switch
            {
                "wheat" => farm.Inventory.Wheat,
                "flour" => farm.Inventory.Flour.GetValueOrDefault(),
                "milk" => farm.Inventory.Milk.GetValueOrDefault(),
                _ => 0,
            };
            return stock >= offer.Quantity;
        }

        private void FulfilTownOrder(string key)
        {
            FarmTownOrders orders = farm?.TownOrders;
            if (orders == null) return;
            TownOrderOffer offer = Array.Find(orders.Offers, item => item.Key == key);
            if (offer == null || !CanFulfilOrder(offer)) return;

            pending = FarmCommand.FulfilTownOrder(farm.Revision, key, orders.Date);
            StartCoroutine(Run(pending));
        }
    }
}
