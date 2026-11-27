using System;
using System.Collections;
using FarmLand.Empire.Networking;
using UnityEngine.UIElements;

namespace FarmLand.Empire.Prototype
{
    public sealed partial class StarterFarmScreen
    {
        private Foldout leaderboardPanel;
        private Label leaderboardStatus;
        private Label personalRank;
        private ScrollView leaderboardRows;
        private Button leaderboardVisibility;
        private HarvestLeaderboard displayedLeaderboard;

        private void BuildLeaderboardPanel()
        {
            leaderboardPanel = new Foldout { text = "Harvest leaderboard", value = false };
            controls.Add(leaderboardPanel);
            leaderboardStatus = new Label();
            leaderboardStatus.style.whiteSpace = WhiteSpace.Normal;
            leaderboardPanel.Add(leaderboardStatus);
            leaderboardVisibility = new Button(ChangeLeaderboardVisibility);
            leaderboardPanel.Add(leaderboardVisibility);
            leaderboardPanel.Add(new Button(() => StartCoroutine(RefreshLeaderboard(null))) { text = "Refresh rankings" });
            personalRank = new Label();
            personalRank.style.whiteSpace = WhiteSpace.Normal;
            leaderboardPanel.Add(personalRank);
            leaderboardRows = new ScrollView();
            leaderboardRows.style.maxHeight = 180;
            leaderboardPanel.Add(leaderboardRows);
            leaderboardPanel.RegisterValueChangedCallback(change =>
            {
                if (change.newValue && !busy && session?.SignedIn == true)
                    StartCoroutine(RefreshLeaderboard(null));
            });
            DisplayLeaderboard(null);
        }

        private void ChangeLeaderboardVisibility()
        {
            if (busy || displayedLeaderboard == null) return;
            StartCoroutine(RefreshLeaderboard(!displayedLeaderboard.Enabled));
        }

        private IEnumerator RefreshLeaderboard(bool? enabled)
        {
            if (busy || session?.SignedIn != true) yield break;
            busy = true;
            Render();
            leaderboardStatus.text = enabled.HasValue ? "Saving visibility…" : "Loading rankings…";

            try
            {
                if (enabled.HasValue)
                {
                    long saved = 0;
                    yield return WithLeaderboardToken((token, result) =>
                        api.SetLeaderboardVisibility(token, enabled.Value, result), value => saved = value);
                    if (saved != 204)
                    {
                        DisplayLeaderboard(null);
                        leaderboardStatus.text = "Could not confirm visibility. Refresh to check the saved setting.";
                        yield break;
                    }
                }

                HarvestLeaderboard board = null;
                yield return WithLeaderboardToken((token, result) =>
                    api.LoadHarvestLeaderboard(token, (code, value) => { board = value; result(code); }), _ => { });
                DisplayLeaderboard(board);
                if (board == null)
                    leaderboardStatus.text = "Rankings unavailable. Refresh to try again.";
            }
            finally
            {
                busy = false;
                Render();
            }
        }

        private IEnumerator WithLeaderboardToken(Func<string, Action<long>, IEnumerator> request, Action<long> complete)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                string token = null;
                yield return session.GetToken(attempt == 1, value => token = value);
                if (token == null) break;
                long statusCode = 0;
                yield return request(token, value => statusCode = value);
                if (statusCode == 401 && attempt == 0) continue;
                complete(statusCode);
                yield break;
            }

            complete(401);
        }

        private void DisplayLeaderboard(HarvestLeaderboard board)
        {
            displayedLeaderboard = board;
            leaderboardRows.Clear();
            personalRank.text = string.Empty;
            leaderboardVisibility.style.display = board == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (board == null)
            {
                leaderboardStatus.text = string.Empty;
                return;
            }

            leaderboardStatus.text = board.Enabled
                ? "Your harvest count can appear under a generated farm name."
                : "Your farm is hidden. Join to share your harvest count under a generated name.";
            leaderboardVisibility.text = board.Enabled ? "Hide my farm" : "Join leaderboard";
            if (board.YourRank != null) personalRank.text = $"Your rank: {RankLabel(board.YourRank)}";
            else if (board.Enabled) personalRank.text = "Harvest a crop to earn a rank.";

            if (board.Entries.Length == 0) leaderboardRows.Add(new Label("No ranked farms yet."));
            foreach (HarvestRank entry in board.Entries)
                leaderboardRows.Add(new Label(RankLabel(entry) + (entry.IsYou ? " · You" : string.Empty)));
        }

        private static string RankLabel(HarvestRank entry) =>
            $"#{entry.Rank} {entry.Name} · {entry.Harvests} harvests";

        private void ClearLeaderboard()
        {
            if (leaderboardPanel == null) return;
            leaderboardPanel.SetValueWithoutNotify(false);
            DisplayLeaderboard(null);
        }
    }
}
