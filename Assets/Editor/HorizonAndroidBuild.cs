using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// Run after the OpenXR manifest hooks. These settings apply to this Quest project.
public sealed class HorizonAndroidBuild : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
{
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    public int callbackOrder => 1000;

    [MenuItem("429/Configure Horizon Release Signing")]
    public static void ConfigureReleaseSigning()
    {
        var signingDirectory = Path.GetFullPath("Builds/Signing");
        var keystore = Path.Combine(signingDirectory, "429-horizon-release.p12");
        var passwordFile = Path.Combine(signingDirectory, "password.txt");
        if (!File.Exists(keystore) || !File.Exists(passwordFile))
            throw new BuildFailedException("Restore the 429 release keystore and password into Builds/Signing first.");
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystore;
        PlayerSettings.Android.keyaliasName = "horizon429";
        PlayerSettings.Android.keystorePass = File.ReadAllText(passwordFile).Trim();
        PlayerSettings.Android.keyaliasPass = PlayerSettings.Android.keystorePass;
        UnityEngine.Debug.Log("429 Horizon release signing configured for this editor session.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        if ((int)PlayerSettings.Android.targetSdkVersion != 34 ||
            PlayerSettings.Android.preferredInstallLocation != AndroidPreferredInstallLocation.Auto ||
            PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
            throw new BuildFailedException("Horizon requires target SDK 34, install location Auto, and Landscape Left orientation. Update Player Settings before building.");
    }

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var libraryManifest = Path.Combine(path, "src/main/AndroidManifest.xml");
        var launcherManifest = Path.GetFullPath(Path.Combine(path, "../launcher/src/main/AndroidManifest.xml"));
        var launcher = Load(launcherManifest);
        launcher.DocumentElement.SetAttribute("installLocation", AndroidNamespace, "auto");
        launcher.Save(launcherManifest);

        var library = Load(libraryManifest);
        var namespaces = new XmlNamespaceManager(library.NameTable);
        namespaces.AddNamespace("android", AndroidNamespace);
        var activity = library.SelectSingleNode("/manifest/application/activity[intent-filter/action[@android:name='android.intent.action.MAIN']]", namespaces) as XmlElement;
        if (activity == null)
            throw new BuildFailedException("Cannot locate the Horizon launch activity in the generated manifest.");
        activity.SetAttribute("screenOrientation", AndroidNamespace, "landscape");
        activity.SetAttribute("excludeFromRecents", AndroidNamespace, "true");
        library.Save(libraryManifest);

        // OpenXR generates device metadata in a separate Android library.
        foreach (var manifestPath in Directory.GetFiles(path, "AndroidManifest.xml", SearchOption.AllDirectories))
        {
            if (manifestPath.Contains(Path.DirectorySeparatorChar + "build" + Path.DirectorySeparatorChar))
                continue;
            var manifest = Load(manifestPath);
            var manager = new XmlNamespaceManager(manifest.NameTable);
            manager.AddNamespace("android", AndroidNamespace);
            var devices = manifest.SelectSingleNode("/manifest/application/meta-data[@android:name='com.oculus.supportedDevices']", manager) as XmlElement;
            if (devices == null)
                continue;
            devices.SetAttribute("value", AndroidNamespace, "quest2|questpro|quest3|quest3s");
            manifest.Save(manifestPath);
        }
    }

    private static XmlDocument Load(string path)
    {
        var document = new XmlDocument();
        document.Load(path);
        return document;
    }
}
