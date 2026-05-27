using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Practice1.Editor
{
    public static class Practice4LinuxServerBuilder
    {
        private const string OutputPath = "Builds/LinuxServer/MultiplayerServer.x86_64";

        [MenuItem("Practice 4/Build Linux Dedicated Server")]
        public static void BuildLinuxDedicatedServer()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainScene.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Linux dedicated server build failed: {report.summary.result}");
            }
        }
    }
}
