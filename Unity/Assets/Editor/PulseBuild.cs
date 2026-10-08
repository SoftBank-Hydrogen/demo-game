using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PulseBuild
{
    [MenuItem("Pulse/Create or open game scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        const string scenePath = "Assets/Scenes/Pulse.unity";
        if (File.Exists(scenePath))
        {
            // Keep Inspector edits in the open scene when building from the menu.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    throw new OperationCanceledException("Scene switch canceled.");
                EditorSceneManager.OpenScene(scenePath);
            }
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.016f, .027f, .043f);
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        new GameObject("Pulse", typeof(PulseApi), typeof(PulseView));
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();
        PulseSceneAuthoring.Migrate();
    }
    [MenuItem("Pulse/Build Web")]
    public static void Web()
    {
        CreateScene();
        var view = UnityEngine.Object.FindFirstObjectByType<PulseView>();
        if (view == null || view.GetComponent<PulseLayout>() == null || view.GetComponentInChildren<Canvas>() == null)
            throw new Exception("PULSE UI is not authored. Run Pulse > Migrate legacy scene UI (once).");
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.isDirty && !EditorSceneManager.SaveScene(activeScene))
            throw new Exception("Could not save the PULSE scene before building.");
        PlayerSettings.companyName = "OneDeploy Demo";
        PlayerSettings.productName = "PULSE";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultWebScreenWidth = 1440;
        PlayerSettings.defaultWebScreenHeight = 900;
        PlayerSettings.WebGL.template = "PROJECT:Pulse";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.initialMemorySize = 128;
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SplashScreen.show = false;
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input = settings.FindProperty("activeInputHandler");
        if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../deploy/public/build"));
        Directory.CreateDirectory(output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Pulse.unity" }, locationPathName = output,
            target = BuildTarget.WebGL, options = BuildOptions.None
        });
        Debug.Log("PULSE_BUILD_RESULT " + report.summary.result + " bytes=" + report.summary.totalSize + " path=" + output);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("PULSE Web build failed: " + report.summary.result);
    }
}
