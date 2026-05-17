using UnityEditor;
using UnityEditor.Build.Reporting;

// Builder CLI para WebGL — ejecutar con:
//   Unity -batchmode -quit -projectPath . -executeMethod WebGLBuilder.Build -logFile build.log
public static class WebGLBuilder
{
    public static void Build()
    {
        string[] scenes = { "Assets/Scenes/SampleScene.unity" };

        var opts = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = "Build/WebGL",
            target           = BuildTarget.WebGL,
            options          = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);

        if (report.summary.result != BuildResult.Succeeded)
        {
            UnityEngine.Debug.LogError("[WebGLBuilder] Build fallido: " + report.summary.result);
            EditorApplication.Exit(1);
        }
        else
        {
            UnityEngine.Debug.Log("[WebGLBuilder] Build completado en: " + opts.locationPathName);
        }
    }
}
