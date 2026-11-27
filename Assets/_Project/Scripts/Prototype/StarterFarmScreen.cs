using System;
using System.Collections;
using FarmLand.Empire.Networking;
using FarmLand.Empire.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class StarterFarmScreen : MonoBehaviour
    {
        [SerializeField] private string apiOrigin = "https://farm-api.example.com";
        [SerializeField] private string firebaseWebApiKey;
        [SerializeField] private GameObject cropMarker;

        private StarterFarmApi api;
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
            signIn.Add(new Button(BeginSignIn) { text = "Sign in" });

            controls = new VisualElement();
            panel.Add(controls);
            plot = new Label();
            controls.Add(plot);
            action = new Button(SubmitAction);
            controls.Add(action);
            refresh = new Button(() => StartCoroutine(Run(null))) { text = "Refresh farm" };
            controls.Add(refresh);
            controls.Add(new Button(SignOut) { text = "Sign out" });

            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(status);
        }

        private void BeginSignIn()
        {
            if (busy || session == null) return;
            if (string.IsNullOrWhiteSpace(email.value) || string.IsNullOrEmpty(password.value))
            {
                status.text = "Enter your email and password.";
                return;
            }

            string secret = password.value;
            password.value = string.Empty;
            StartCoroutine(SignIn(email.value.Trim(), secret));
        }

        private IEnumerator SignIn(string address, string secret)
        {
            busy = true;
            status.text = "Signing in…";
            Render();
            string token = null;
            yield return session.SignIn(address, secret, value => token = value);
            busy = false;

            if (token == null)
            {
                status.text = "Could not sign in. Check your account and connection.";
                Render();
                yield break;
            }
            yield return Run(null);
        }

        private void SignOut()
        {
            session?.SignOut();
            farm = null;
            pending = null;
            reloadRequired = false;
            refreshOnResume = false;
            busy = false;
            if (password != null) password.value = string.Empty;
            if (status != null) status.text = string.Empty;
            if (session != null) Render();
        }

        private double RemainingSeconds()
        {
            if (farm?.Plot == null) return 0;
            double elapsed = Time.realtimeSinceStartupAsDouble - receivedAt;
            return Math.Max(0, (farm.Plot.ReadyAt - farm.ServerTime).TotalSeconds - elapsed);
        }

        private void Render()
        {
            bool signedIn = session.SignedIn;
            if (cropMarker != null) cropMarker.SetActive(signedIn && farm?.Plot != null);
            signIn.style.display = signedIn ? DisplayStyle.None : DisplayStyle.Flex;
            controls.style.display = signedIn ? DisplayStyle.Flex : DisplayStyle.None;
            signIn.SetEnabled(!busy);
            controls.SetEnabled(!busy);
            double remaining = RemainingSeconds();
            plot.text = farm == null ? (busy ? "Loading farm…" : "Farm unavailable") : farm.Plot == null
                ? $"Empty plot · Wheat: {farm.Inventory.Wheat}"
                : $"Wheat: {farm.Inventory.Wheat} · " + (remaining > 0 ? $"Ready in {Math.Ceiling(remaining)}s" : "Ready to harvest");
            action.text = pending != null ? "Retry action" : farm?.Plot == null ? "Plant wheat" : "Harvest";
            action.SetEnabled(!busy && (pending != null ||
                (!reloadRequired && farm != null && remaining <= 0)));
            refresh.SetEnabled(!busy && pending == null);
        }

        private void SubmitAction()
        {
            if (busy || farm == null || (reloadRequired && pending == null) ||
                (pending == null && RemainingSeconds() > 0)) return;

            bool retry = pending != null;
            pending ??= farm.Plot == null ? FarmCommand.Plant(farm.Revision) : FarmCommand.Harvest(farm.Revision);
            StartCoroutine(Run(pending, retry));
        }

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
                _ => "The action was rejected. The latest farm state is shown.",
            };
        }
    }
}
