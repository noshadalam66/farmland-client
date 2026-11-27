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
