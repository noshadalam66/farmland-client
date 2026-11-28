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

1. Enable the Email/Password provider in Firebase Authentication. Use one
   Firebase project for the client and API.
2. Start the farm API with the merged database migrations and Firebase project
   settings. Set `WEB_ORIGIN` on the API for a browser build.
3. In Unity, choose **FarmLand → Create Starter Farm Scene**. On the selected
   **Starter Farm Screen** object, set the API origin and Firebase Web API key.
   Use an HTTPS API origin for devices. Loopback HTTP is supported by the client
   for local testing where Unity platform settings allow it.
4. Enter Play Mode and choose **Create account** with an unused email, or
   choose **Sign in** with an existing account. Plant wheat, wait for the server countdown,
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
offspring. Open **Family tree** under **Livestock** and select an animal to
follow its recorded maternal and paternal branches.

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
2. Run the API repository's terrain worker. Keep the world panel open to
   see generation status and watch the terrain tiles appear as they download.
3. Close **Pilot world** to return to the fixed starter-farm view.

The pilot selector takes numeric coordinates. It loads every chunk for the
selected bounded area and scales the preview to fit the starter camera.
To verify recovery, interrupt a tile request briefly and confirm that the
client retries that asset and continues to the next tile. If all retries
fail, the starter farm returns and **Check world** can reload the terrain.

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

The picker opens at the San Francisco pilot center and outlines the approximate
footprint for the size selected before opening the map. All platforms can fill
the coordinate fields with **Use San Francisco test area**. The preset uses
the center documented in the terrain worker setup.

## Starter settlement

With migration 000013 and the town API, open **Settlement** after buying the
farm expansion. Build the road, then cottages. Refresh the farm to check the
growing resident count. This panel shows the saved settlement state.
Regenerate the starter scene to see the road and cottages in fixed slots
along the farm edge. They reappear after sign-in based on the saved building
counts. Terrain mode continues to show the geographic pilot separately.

With migration 000014, the panel also supports a water tower, workshops,
schools, and clinics and displays server-calculated service coverage and
wellbeing. Regenerate the starter scene to show saved service buildings
in fixed slots along the other side of the farm. The game panel scrolls
when expanded controls exceed the available height.

With migration 000015, **Settlement → Daily supply orders** offers wheat,
flour, and milk deliveries as more residents arrive. The board resets at
midnight UTC, and completed deliveries credit the farm coin balance.

## Reproducible pilot builds

Use a licensed Unity 6.3 editor with the target's build support installed.
Set `FARMLAND_API_ORIGIN` and `FARMLAND_FIREBASE_WEB_API_KEY`. For the Web map
picker, optionally set `FARMLAND_GOOGLE_MAPS_BROWSER_KEY`. Use public client
configuration values and keep service credentials on the backend.

From the client checkout, run the Unity executable. Replace `WebGL` with
`Android` for a test APK, or `iOS` for an Xcode project.

```sh
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" -buildTarget WebGL \
  -executeMethod FarmLand.Empire.Editor.PilotBuild.Build -logFile -
```

Each run uses an isolated directory under `Builds/Pilot/`. The build creates
its configured starter scene temporarily, then removes it from the project.
For web, serve the output on the origin configured in the API's `WEB_ORIGIN`.
For iOS, use macOS and Xcode to sign and install the exported project.

Use a test account on all three builds. Plant on Android, verify the same
plot and countdown on web, harvest on iOS, then reopen Android and verify
one unit was credited. Run the test on a farm with an empty selected plot.

### Release build artifacts

Use the same batch-mode command and target modules with
`-executeMethod FarmLand.Empire.Editor.PilotBuild.BuildRelease`.
Set `FARMLAND_API_ORIGIN` to the deployed HTTPS API and use the Firebase key
for that environment. Outputs are under `Builds/Release/`.

- **WebGL** produces the static player. Serve it over HTTPS and configure
  the API's `WEB_ORIGIN` to match the deployed web origin.
- **Android** produces an AAB. Configure the project's application ID and
  release signing before uploading the bundle to Google Play internal testing.
  Use the pilot APK for direct device installation.
- **iOS** exports an Xcode project. Archive and sign it on macOS for TestFlight.

Verify the same-account handoff against the same deployed API and Firebase project.

Run **Phase 1 release builds** from **Actions** on `main` to package all
three targets from one commit. The workflow requires a project-controlled
macOS runner labeled `farmland-unity`, with an activated Unity version
matching `ProjectSettings/ProjectVersion.txt` and the WebGL, Android, and
iOS modules. Set these GitHub repository variables:

| Variable | Value |
| --- | --- |
| `FARMLAND_UNITY_EDITOR` | Absolute path to the runner's Unity executable |
| `FARMLAND_API_ORIGIN` | HTTPS origin of the shared test deployment |
| `FARMLAND_FIREBASE_WEB_API_KEY` | Client API key for that deployment's Firebase project |
| `FARMLAND_GOOGLE_MAPS_BROWSER_KEY` | Optional restricted browser key |

Download the artifacts from the same workflow run before performing the
Android → web → iOS → Android handoff above.
