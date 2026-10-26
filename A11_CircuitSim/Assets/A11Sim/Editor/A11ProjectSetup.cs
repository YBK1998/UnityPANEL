using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace A11Sim.EditorTools
{
    /// Batch-mode friendly project setup: TMP essentials, scene, player settings, Windows build.
    public static class A11ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/A11Circuit.unity";
        const string BuildPath = "Build/A11CircuitSim.exe";

        [MenuItem("A11 Sim/1 · Import TMP Essential Resources")]
        public static void ImportTmp()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                Debug.Log("[A11] TMP essentials already present");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string pkg = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            Debug.Log("[A11] Importing " + pkg);
            // The import is asynchronous: in batch mode run without -quit and exit from the callbacks.
            AssetDatabase.importPackageCompleted += name =>
            {
                Debug.Log("[A11] Imported " + name);
                AssetDatabase.SaveAssets();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (name, error) =>
            {
                Debug.LogError("[A11] Import failed: " + error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            };
            // The callbacks above belong to this API; keep it until the replacement offers the same events.
#pragma warning disable CS0618
            AssetDatabase.ImportPackage(pkg, false);
#pragma warning restore CS0618
        }

        [MenuItem("A11 Sim/2 · Create scene and player settings")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 160;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.030f, 0.038f, 0.052f);
            camGo.transform.position = new Vector3(184, 156, -10);
            camGo.AddComponent<CameraRig>();
            new GameObject("A11 Circuit").AddComponent<SchematicView>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "A11 Circuit Sim";
            PlayerSettings.companyName = "United Box";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[A11] Scene created: " + ScenePath);
        }

        /// Standard-shader templates for the 3D door. Kept in Resources so the build includes the
        /// Standard variants they need (runtime Shader.Find("Standard") alone would be stripped).
        [InitializeOnLoadMethod]
        static void AutoEnsureMaterials() => EditorApplication.delayCall += EnsureMaterials;

        public static void EnsureMaterials()
        {
            CreateStd("Assets/A11Sim/Resources/A11Std.mat", false);
            CreateStd("Assets/A11Sim/Resources/A11StdEmissive.mat", true);
        }

        static void CreateStd(string path, bool emissive)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var m = new Material(Shader.Find("Standard"));
            m.SetFloat("_Glossiness", 0.4f);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[A11] Created " + path);
        }

        [MenuItem("A11 Sim/3 · Build Windows player")]
        public static void BuildWindows()
        {
            EnsureMaterials();
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[A11] Build {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / 1024 / 1024} MB → {BuildPath}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
