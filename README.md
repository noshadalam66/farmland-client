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
and sells one harvested unit per action. With migration 000007, earnings can
unlock one expansion. Use the plot selector to independently plant and
harvest the starter and expansion plots. Regenerate the starter scene if
needed to show the second plot and its crop marker. The farm screen guides
new players from planting through buying the expansion using saved progress.

With migrations 000008–000009 and the livestock API, open **Livestock** in
the starter farm. Buy goats or chickens with farm coins, feed each animal
one wheat, and collect the product after the server countdown. The roster
and product inventory are saved to the account. With migration 000010,
open **Breeding** to choose parents, follow the countdown, and collect the
offspring. The roster shows the saved parent identifiers for each offspring.

With migration 000011, open **Grain mill** to buy the building, start a wheat
batch, collect flour after the countdown, and sell it at the server price.

With migration 000012, open **Harvest leaderboard** to view the top farms
and your rank. Your farm starts hidden. Join to participate under the
server-generated alias or hide your farm again from the same panel.

The scene generator creates a fixed isometric test map. Authentication uses
Firebase's email/password and token exchange REST endpoints. Sessions are
kept in memory for this prototype. Closing the app requires signing in again,
and signing in reloads the saved farm from the server.

## Phase 2 terrain preview

Generate a pilot terrain package with the GDAL exporter in `farmland-api`.
In Unity, choose **FarmLand → Preview Terrain Package** and open its
`manifest.json`. The editor opens a temporary preview scene with a mesh for
each chunk. The meshes are generated in memory and intended for inspection
in the Scene view. Reopen the manifest to regenerate the preview.

The runtime mesh builder maps east to Unity X and north to Unity Z, with
elevation as Unity Y in meters. Adjacent meshes place shared edge samples
at the same coordinates. The current preview calculates lighting normals
per mesh, so lighting can differ along chunk boundaries.

### Load a pilot world in Play Mode

Use **FarmLand → Create Starter Farm Scene** to generate a scene with the
world controls, then configure and sign in as described above. The API must
include world migrations 000003–000004 and use the same terrain directory as
the pilot worker.

1. Open **Pilot world**. Enter coordinates covered by the configured source
   raster, select 1, 4, or 9 km², and request terrain.
2. Run the API repository's terrain worker. Select **Check world** once it
   finishes to fetch the manifest and render the height chunks.
3. Close **Pilot world** to return to the fixed starter-farm view.

The pilot selector takes numeric coordinates. It loads every chunk for the
selected bounded area and scales the preview to fit the starter camera.

If the pilot worker exports a water mask, the generated starter scene
downloads each mask and uses a separate blue material for faces whose vertices
are mostly classified as water. The material follows the elevation mesh.
This provides a classification preview without changing terrain heights.
Older scenes can assign a **Water Material** on Starter Farm Screen, or be
regenerated. Elevation-only packages and the editor mesh preview continue
to work without the optional mask.

### Google Maps in the Unity Web pilot

To enable **Choose on map** in the Web build, enable Maps JavaScript API in
the Google Maps project and set **Google Maps Browser Key** on Starter Farm
Screen. Restrict the browser key to the deployed Web origin and Maps
JavaScript API. The game requests terrain only after the player confirms the
coordinates and area in Unity. Google map tiles remain in the picker. The
terrain worker uses the separately sourced elevation and water datasets.

The picker opens at the San Francisco pilot center. Android and iOS use the
coordinate fields in this build.
