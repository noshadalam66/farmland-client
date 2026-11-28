using System;
using System.Collections;
using System.Collections.Generic;
using FarmLand.Empire.Networking;
using FarmLand.Empire.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class StarterFarmScreen : MonoBehaviour
    {
        [SerializeField] private string apiOrigin = "https://farm-api.example.com";
        [SerializeField] private string firebaseWebApiKey;
        [SerializeField] private GameObject cropMarker;
        [SerializeField] private GameObject expansionGeometry;
        [SerializeField] private GameObject expansionCropMarker;

        private StarterFarmApi api;
        private string worldOrigin;
        private FirebaseSession session;
        private FarmState farm;
        private FarmCommand pending;
        private double receivedAt;
        private bool busy;
        private bool reloadRequired;
        private bool refreshOnResume;
        private VisualElement signIn;
        private VisualElement controls;
        private TextField email;
        private TextField password;
        private Label status;
        private Label plot;
        private Label inventory;
        private DropdownField cropSelector;
        private Label otherPlot;
        private DropdownField plotSelector;
        private Button purchaseParcel;
        private VisualElement marketPanel;
        private Label wallet;
        private Label farmHint;
        private readonly Dictionary<string, Button> saleButtons = new Dictionary<string, Button>();
        private Button action;
        private Button refresh;

        private void OnEnable()
        {
            BuildScreen();
            if (cropMarker != null) cropMarker.SetActive(false);
            if (!Uri.TryCreate(apiOrigin, UriKind.Absolute, out Uri origin) ||
                string.IsNullOrWhiteSpace(firebaseWebApiKey))
            {
                ConfigurationError();
                return;
            }

            try
            {
                api = new StarterFarmApi(origin);
                worldOrigin = origin.GetLeftPart(UriPartial.Authority);
                session = new FirebaseSession(firebaseWebApiKey.Trim());
                Render();
            }
            catch (ArgumentException)
            {
                ConfigurationError();
            }
        }

        private void ConfigurationError()
        {
            signIn.SetEnabled(false);
            controls.style.display = DisplayStyle.None;
            status.text = "Set the API origin and Firebase Web API key on Starter Farm Screen.";
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            SignOut();
        }

        private void Update()
        {
            if (session != null) Render();
            RefreshExpiredTownOrders();
            CheckPilotMap();
            PollWorldGeneration();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) RefreshOnResume();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) RefreshOnResume();
        }

        private void RefreshOnResume()
        {
            if (session?.SignedIn != true || pending != null) return;
            if (busy)
            {
                refreshOnResume = true;
                return;
            }
            StartCoroutine(Run(null));
        }

        private void BuildScreen()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            var panel = new VisualElement();
            panel.style.width = Length.Percent(90);
            panel.style.maxWidth = 420;
            panel.style.maxHeight = Length.Percent(82);
            panel.style.marginTop = 24;
            panel.style.alignSelf = Align.Center;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.backgroundColor = new Color(0.13f, 0.18f, 0.14f);
            panel.style.color = Color.white;
            root.Add(panel);
            var title = new Label("FarmLand Empire");
            title.style.fontSize = 24;
            panel.Add(title);

            signIn = new VisualElement();
            panel.Add(signIn);
            email = new TextField("Email");
            password = new TextField("Password") { isPasswordField = true };
            signIn.Add(email);
            signIn.Add(password);
            signIn.Add(new Button(() => BeginAuthentication(false)) { text = "Sign in" });
            signIn.Add(new Button(() => BeginAuthentication(true)) { text = "Create account" });

            controls = new ScrollView(ScrollViewMode.Vertical);
            controls.style.flexShrink = 1;
            controls.style.minHeight = 0;
            panel.Add(controls);
            plot = new Label();
            controls.Add(plot);
            plotSelector = new DropdownField("Plot", new List<string> { "Starter", "Expansion" }, 0);
            controls.Add(plotSelector);
            otherPlot = new Label();
            otherPlot.style.whiteSpace = WhiteSpace.Normal;
            controls.Add(otherPlot);
            purchaseParcel = new Button(BuyParcel);
            controls.Add(purchaseParcel);
            inventory = new Label();
            inventory.style.whiteSpace = WhiteSpace.Normal;
            controls.Add(inventory);
            cropSelector = new DropdownField("Crop", new List<string> { "Wheat", "Corn", "Rice" }, 0);
            controls.Add(cropSelector);
            action = new Button(SubmitAction);
            controls.Add(action);
            marketPanel = new VisualElement();
            controls.Add(marketPanel);
            wallet = new Label();
            marketPanel.Add(wallet);
            saleButtons.Clear();
            foreach (string crop in new[] { "wheat", "corn", "rice" })
            {
                var sell = new Button(() => SellCrop(crop));
                saleButtons.Add(crop, sell);
                marketPanel.Add(sell);
            }
            refresh = new Button(() => StartCoroutine(Run(null))) { text = "Refresh farm" };
            controls.Add(refresh);
            BuildWorldControls();
            controls.Add(new Button(SignOut) { text = "Sign out" });

            farmHint = new Label();
            farmHint.style.whiteSpace = WhiteSpace.Normal;
            controls.Add(farmHint);
            BuildLivestockPanel();
            BuildMillPanel();
            BuildTownPanel();
            BuildLeaderboardPanel();
            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.flexShrink = 0;
            panel.Add(status);
        }

        private void BeginAuthentication(bool createAccount)
        {
            if (busy || session == null || session.SignedIn) return;
            if (string.IsNullOrWhiteSpace(email.value) || string.IsNullOrEmpty(password.value))
            {
                status.text = "Enter your email and password.";
                return;
            }

            string secret = password.value;
            password.value = string.Empty;
            StartCoroutine(Authenticate(email.value.Trim(), secret, createAccount));
        }

        private IEnumerator Authenticate(string address, string secret, bool createAccount)
        {
            busy = true;
            status.text = createAccount ? "Creating your account…" : "Signing in…";
            Render();
            string token = null;
            yield return session.Authenticate(address, secret, createAccount, value => token = value);
            busy = false;

            if (token == null)
            {
                status.text = createAccount
                    ? "Could not create the account. Check your email, password, and connection. If you already have an account, sign in."
                    : "Could not sign in. Check your account and connection.";
                Render();
                yield break;
            }
            yield return Run(null);
        }

        private void SignOut()
        {
            session?.SignOut();
            ClearTerrainView();
            if (worldPanel != null) worldPanel.style.display = DisplayStyle.None;
            selectionKey = null;
            failedWorldRequestId = null;
            farm = null;
            if (plotSelector != null) plotSelector.index = 0;
            pending = null;
            reloadRequired = false;
            refreshOnResume = false;
            busy = false;
            ClearLeaderboard();
            if (password != null) password.value = string.Empty;
            if (status != null) status.text = string.Empty;
            if (session != null) Render();
        }

        private bool ExpansionSelected => farm?.Expansion?.Owned == true && plotSelector.index == 1;

        private FarmPlot SelectedPlot => ExpansionSelected ? farm.Expansion.Plot : farm?.Plot;

        private double RemainingSeconds(FarmPlot crop)
        {
            if (crop == null) return 0;
            double elapsed = Time.realtimeSinceStartupAsDouble - receivedAt;
            return Math.Max(0, (crop.ReadyAt - farm.ServerTime).TotalSeconds - elapsed);
        }

        private void Render()
        {
            bool signedIn = session.SignedIn;
            if (cropMarker != null) cropMarker.SetActive(signedIn && farm?.Plot != null);
            signIn.style.display = signedIn ? DisplayStyle.None : DisplayStyle.Flex;
            controls.style.display = signedIn ? DisplayStyle.Flex : DisplayStyle.None;
            signIn.SetEnabled(!busy);
            controls.SetEnabled(!busy);
            FarmPlot selected = SelectedPlot;
            double remaining = RemainingSeconds(selected);
            bool canChooseCrop = farm?.Inventory.HasCropVariety == true;
            bool emptyPlot = farm != null && selected == null;
            bool expansionOwned = farm?.Expansion?.Owned == true;
            if (expansionGeometry != null) expansionGeometry.SetActive(signedIn && expansionOwned);
            if (expansionCropMarker != null)
                expansionCropMarker.SetActive(signedIn && expansionOwned && farm.Expansion.Plot != null);

            farmHint.text = FarmHint(remaining);
            farmHint.style.display = string.IsNullOrEmpty(farmHint.text) ? DisplayStyle.None : DisplayStyle.Flex;
            plotSelector.style.display = expansionOwned ? DisplayStyle.Flex : DisplayStyle.None;
            plotSelector.SetEnabled(!busy && pending == null);
            otherPlot.style.display = expansionOwned ? DisplayStyle.Flex : DisplayStyle.None;
            if (expansionOwned)
            {
                FarmPlot inactivePlot = ExpansionSelected ? farm.Plot : farm.Expansion.Plot;
                string inactiveName = ExpansionSelected ? "Starter" : "Expansion";
                otherPlot.text = $"{inactiveName} plot · {PlotStatus(inactivePlot, RemainingSeconds(inactivePlot))}";
            }

            bool canBuy = farm?.Expansion != null && !expansionOwned && farm.Wallet != null;
            purchaseParcel.style.display = canBuy ? DisplayStyle.Flex : DisplayStyle.None;
            if (canBuy)
            {
                purchaseParcel.text = $"Buy expansion · {farm.Expansion.Price} coins";
                purchaseParcel.SetEnabled(!busy && !reloadRequired && pending == null &&
                    farm.Wallet.Coins >= farm.Expansion.Price);
            }
            cropSelector.style.display = canChooseCrop && emptyPlot ? DisplayStyle.Flex : DisplayStyle.None;
            cropSelector.SetEnabled(!busy && !reloadRequired && pending == null);
            inventory.text = farm == null ? string.Empty : canChooseCrop
                ? $"Wheat: {farm.Inventory.Wheat} · Corn: {farm.Inventory.Corn} · Rice: {farm.Inventory.Rice}"
                : $"Wheat: {farm.Inventory.Wheat}";

            plot.text = farm == null ? (busy ? "Loading farm…" : "Farm unavailable")
                : PlotStatus(selected, remaining);
            action.text = pending != null ? "Retry action" : selected == null ? "Plant" : "Harvest";
            action.SetEnabled(!busy && (pending != null ||
                (!reloadRequired && farm != null && remaining <= 0)));
            refresh.SetEnabled(!busy && pending == null);
            RenderLivestock();
            RenderMill();
            RenderTown();

            bool marketAvailable = farm?.Wallet != null && farm.Market != null;
            marketPanel.style.display = marketAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            if (!marketAvailable) return;
            wallet.text = $"Coins: {farm.Wallet.Coins}";
            foreach (var entry in saleButtons)
            {
                int price = farm.Market.SellPrices.Count(entry.Key);
                entry.Value.text = $"Sell {CropLabel(entry.Key)} · {price} coins";
                entry.Value.SetEnabled(!busy && !reloadRequired && pending == null &&
                    price > 0 && farm.Inventory.Count(entry.Key) > 0);
            }
        }

        private static string PlotStatus(FarmPlot crop, double remaining)
        {
            if (crop == null) return "Empty plot";
            string name = CropLabel(crop.Crop);
            return remaining > 0 ? $"{name} · Ready in {Math.Ceiling(remaining)}s" : $"{name} · Ready to harvest";
        }

        private string FarmHint(double remaining)
        {
            if (busy || reloadRequired || pending != null || farm?.Expansion == null || farm.Expansion.Owned)
                return string.Empty;
            if (farm.Wallet != null && farm.Wallet.Coins >= farm.Expansion.Price)
                return "Buy the expansion to unlock a second plot.";
            if (farm.Inventory.Wheat > 0 || farm.Inventory.Count("corn") > 0 || farm.Inventory.Count("rice") > 0)
                return "Sell harvested crops to earn coins for the expansion.";
            if (farm.Plot == null) return "Choose a crop and plant it in the starter plot.";
            return remaining > 0
                ? "Your crop keeps growing while you're away."
                : "Harvest the crop to add it to your inventory.";
        }

        private void BuyParcel()
        {
            if (busy || reloadRequired || pending != null || farm?.Expansion == null ||
                farm.Expansion.Owned || farm.Wallet == null || farm.Wallet.Coins < farm.Expansion.Price) return;

            pending = FarmCommand.BuyParcel(farm.Revision);
            StartCoroutine(Run(pending));
        }

        private void SellCrop(string crop)
        {
            if (busy || reloadRequired || pending != null || farm?.Market == null ||
                farm.Inventory.Count(crop) < 1 || farm.Market.SellPrices.Count(crop) < 1) return;

            pending = FarmCommand.Sell(farm.Revision, crop);
            StartCoroutine(Run(pending));
        }

        private void SubmitAction()
        {
            if (busy || farm == null || (reloadRequired && pending == null) ||
                (pending == null && RemainingSeconds(SelectedPlot) > 0)) return;

            bool retry = pending != null;
            pending ??= SelectedPlot == null
                ? FarmCommand.Plant(farm.Revision, farm.Inventory.HasCropVariety ? cropSelector.value.ToLowerInvariant() : null,
                    ExpansionSelected)
                : FarmCommand.Harvest(farm.Revision, ExpansionSelected);
            StartCoroutine(Run(pending, retry));
        }

        private static string CropLabel(string crop) => crop switch
        {
            "corn" => "Corn",
            "rice" => "Rice",
            _ => "Wheat",
        };

        private IEnumerator Run(FarmCommand command, bool retry = false)
        {
            if (busy || !session.SignedIn) yield break;
            busy = true;
            status.text = command == null ? "Refreshing farm…" : "Saving action…";
            Render();

            FarmResult result = null;
            yield return Request(command, false, value => result = value);
            if (result?.StatusCode == 401)
            {
                result = null;
                yield return Request(command, true, value => result = value);
            }

            if (result?.State != null)
            {
                pending = null;
                ApplyState(result.State);
                status.text = result.Succeeded
                    ? (command == null ? string.Empty : "Farm saved.")
                    : ConflictMessage(result.Code);

                if (retry && result.Succeeded)
                {
                    reloadRequired = true;
                    FarmResult latest = null;
                    yield return Request(null, false, value => latest = value);
                    if (latest?.Succeeded == true) ApplyState(latest.State);
                    else status.text = "Action saved. Refresh the farm before playing.";
                }
            }
            else
            {
                status.text = result == null || result.StatusCode == 401
                    ? "Could not verify your session. Retry or sign in again."
                    : command == null ? "Could not load the farm. Refresh to try again."
                    : "Could not confirm the action. Retry to check whether it saved.";
            }

            busy = false;
            Render();
            if (refreshOnResume)
            {
                refreshOnResume = false;
                RefreshOnResume();
            }
        }

        private IEnumerator Request(FarmCommand command, bool refreshToken, Action<FarmResult> complete)
        {
            string token = null;
            yield return session.GetToken(refreshToken, value => token = value);
            if (token == null) yield break;
            yield return command == null ? api.Load(token, complete) : api.Submit(command, token, complete);
        }

        private void ApplyState(FarmState state)
        {
            farm = state;
            receivedAt = Time.realtimeSinceStartupAsDouble;
            nextTownOrderRefresh = 0;
            reloadRequired = false;
        }

        private static string ConflictMessage(string code)
        {
            return code switch
            {
                "stale_revision" => "The farm changed on another device. The latest state is shown.",
                "crop_not_ready" => "The crop is still growing.",
                "plot_occupied" => "This plot already has a crop.",
                "plot_empty" => "There is no crop to harvest.",
                "insufficient_crop" => "There is no stock left to sell.",
                "insufficient_coins" => "Earn more coins by selling crops.",
                "parcel_already_owned" => "The expansion is already owned.",
                "parcel_not_owned" => "Buy the expansion before planting there.",
                "livestock_capacity" => "The livestock pen is full.",
                "animal_not_owned" => "That animal is unavailable.",
                "animal_already_fed" => "Collect its product before feeding again.",
                "animal_not_fed" => "Feed the animal to start production.",
                "product_not_ready" => "The product is still in progress.",
                "invalid_breeding_pair" => "Choose unrelated adults of the same species and opposite sexes.",
                "breeding_in_progress" => "A breeding timer is already running.",
                "breeding_not_started" => "Start breeding before collecting an offspring.",
                "offspring_not_ready" => "The offspring is still in progress.",
                "mill_already_owned" => "The grain mill is already owned.",
                "mill_not_owned" => "Buy the grain mill first.",
                "mill_busy" => "Collect the current batch before starting another.",
                "mill_idle" => "Start a wheat batch first.",
                "flour_not_ready" => "The flour is still in progress.",
                "insufficient_flour" => "There is no flour left to sell.",
                "town_requires_expansion" => "Buy the farm expansion before building.",
                "town_requires_road" => "Build a road before adding this building.",
                "town_requires_cottage" => "Build a cottage before adding this building.",
                "town_building_limit" => "This building type has reached its limit.",
                "town_order_expired" => "The order date changed. The latest offers are shown.",
                "town_order_locked" => "More residents are needed for this order.",
                "town_order_fulfilled" => "This order was already filled today.",
                "town_order_stock" => "There is not enough stock for this order.",
                _ => "The action was rejected. The latest farm state is shown.",
            };
        }
    }
}
