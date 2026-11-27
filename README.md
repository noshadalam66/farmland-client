# FarmLand Empire client

One Unity 6.3 LTS project for Android, iOS and web. This bootstrap pins the editor and declares URP and Input System dependencies.

1. Install Git LFS and run `git lfs install`.
2. Add this directory to Unity Hub and open it with Unity 6000.3.0f1.
3. Install Android, iOS and Web build support as needed. Use macOS with Xcode to sign and build the iOS app.

Commit Unity-generated asset metadata alongside its assets. The first gameplay milestone adds an isometric URP scene and a shared farm save flow through `farmland-api`.

Open pull requests against `main` after the initial bootstrap.

## Starter farm networking

`StarterFarmApi` loads the authenticated farm and submits `plant` and `harvest`
commands through Unity coroutines. Pass a current Firebase ID token from the
platform's sign-in layer. The API client keeps the token on the request and
supports HTTPS, with HTTP allowed for loopback development.

Build a command from the latest server revision, then keep the same
`FarmCommand` instance if retrying after a lost response. Refresh the farm
after a successful retry before rendering, since replay returns the original
committed snapshot. On HTTP 409 use the returned state to reconcile the UI.
On HTTP 401 refresh sign-in before retrying the same pending command.

The session layer will supply tokens, and the first playable scene will render
the plot, server countdown, and wheat inventory.

## Phase 1 farm prototype

1. Enable the Email/Password provider in Firebase Authentication and provision
   a test user. Use one Firebase project for the client and API.
2. Start the farm API with the merged database migrations and Firebase project
   settings. Set `WEB_ORIGIN` on the API for a browser build.
3. In Unity, choose **FarmLand → Create Starter Farm Scene**. On the selected
   **Starter Farm Screen** object, set the API origin and Firebase Web API key.
   Use an HTTPS API origin for devices. Loopback HTTP is supported by the client
   for local testing where Unity platform settings allow it.
4. Enter Play Mode and sign in. Plant wheat, wait for the server countdown,
   harvest, and refresh to see the confirmed inventory.
5. Add the generated scene to the build profiles for Android, iOS, and Web.
   Use the same account across all three to check farm handoff.

With the crop-variety API and database migration 000005, an empty plot offers
wheat, corn, and rice. Inventory displays the server totals for all three, and
planted plots show the crop name and server countdown. Older backends continue
to show wheat and accept the existing default plant command. With migration
000006 and the market API, the farm shows the coin balance and server prices,
and sells one harvested unit per action.

The scene generator creates a fixed isometric test map. Authentication uses
Firebase's email/password and token exchange REST endpoints. Sessions are
kept in memory for this prototype. Closing the app requires signing in again,
and signing in reloads the saved farm from the server.
