# 429 Horizon build

## APK and verification

Output: `Builds/Horizon/429-Horizon-release.apk`.

This APK repackages the existing Unity-generated Android project from the last
compiled build. It does not freshly compile current Unity scenes or scripts.
The existing global scene list enables only `Assets/Scenes/2 Game Scene.unity`;
the Android profile inherits that global list. Confirm the intended release
scene list before a fresh Unity build.

Verified with Android SDK `aapt` and `apksigner`:

| Property | Result |
| --- | --- |
| Package | `com.MeLikePie.FourTwentyKnight429` |
| Version | `0.0.0.6`, version code `1` |
| Minimum SDK | 32 |
| Target SDK | 34 |
| Compile SDK | 36 (allowed to exceed target SDK) |
| Install location | `auto` |
| Launch orientation | `landscape` |
| Exclude from recents | true |
| Supported devices | `quest2\|questpro\|quest3\|quest3s` |
| Debuggable | absent |
| Signing | RSA release certificate `CN=429 Release`, APK signature v3 verified |

APK SHA-256: `36AB35440F49688B2776B544F154AE1B7C0332298FE7FD79E256BDC6B52DB8B0`.

Build and inspection logs are in `Builds/Horizon`. The editor build hook compiles
against the installed Unity 6000.3.10f1 assemblies. Its order follows Unity's
OpenXR manifest hooks, so it corrects the final source manifests before merging.

## Release signing and future Unity builds

The new signing key is `Builds/Signing/429-horizon-release.p12`, alias
`horizon429`. Its randomly generated password is in `Builds/Signing/password.txt`.
That directory has access restricted to the current Windows user. The entire
`Builds` directory is ignored by Git. Back up both files privately; reuse this
key for all updates. If an earlier binary was already accepted with another
key, verify the app's signing requirements before adopting this new key.

1. Allow Unity to import the changed settings and editor script.
2. Select **429 > Configure Horizon Release Signing**. This loads the local key
   and password into the current Unity editor session.
3. Check the release scene list and increment Android **Bundle Version Code**
   above any version already accepted by Horizon (the current code is 1).
4. Build Android as an APK with Development Build disabled, ARM64 and IL2CPP.
5. Recheck the packaged APK, then upload it to Horizon.

Project settings now pin target SDK 34, Auto installation and Landscape Left.
Quest 1 is disabled in the Android OpenXR device settings. The build hook
validates those Player Settings and writes landscape, exclude-from-recents and
current device identifiers into the generated manifests.

## Remaining submission checks

Complete age-group self-certification in the Horizon Developer Dashboard.
This is an account/app declaration and cannot be embedded into the APK.

The APK still contains foreground fine/coarse location permissions generated
from the existing project. PlayMaker includes location actions; confirm whether
the game actually uses them and review Meta's permission requirements before
submission. They were not among the three errors in the supplied upload log.

No new Horizon upload or headset runtime/performance review was performed.
Passing the corrected manifest checks does not establish full Store approval.

Reference: https://developers.meta.com/horizon/resources/publish-mobile-manifest/
