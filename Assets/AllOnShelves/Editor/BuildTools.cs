using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AllOnShelves.EditorTools
{
    /// <summary>«Всё по полкам → 3. Собрать WebGL для Яндекс Игр». Результат: Builds/WebGL (+ zip для загрузки).</summary>
    public static class BuildTools
    {
        public const string OutDir = "Builds/WebGL";

        [MenuItem("Всё по полкам/3. Собрать WebGL для Яндекс Игр", priority = 3)]
        public static string BuildWebGL()
        {
            // Скрипты редактора должны быть скомпилированы под WebGL: иначе плагин YG2 пропускает свою доработку
            // index.html (#if PLATFORM_WEBGL) — нет кода модулей Яндекса и заставки. 30.09.2026 так и было:
            // активным стоял профиль сборки «Windows». Переключаем и просим запустить сборку ещё раз.
            if (UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile() != null || EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(null);
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                const string again = "Build Aborted: редактор переключён на платформу WebGL — дождись перекомпиляции и собери ещё раз";
                Debug.LogWarning("[AllOnShelves] " + again);
                File.WriteAllText("Temp/build_result.txt", again);
                return again;
            }
#if !PLATFORM_WEBGL
            {
                const string notYet = "Build Aborted: скрипты редактора ещё не перекомпилированы под WebGL — подожди и собери ещё раз";
                Debug.LogWarning("[AllOnShelves] " + notYet);
                File.WriteAllText("Temp/build_result.txt", notYet);
                return notYet;
            }
#endif
            ProjectSetup.ConfigurePlayer();
            if (AssetDatabase.IsValidFolder("Assets/WebGLTemplates/YandexGames"))
                PlayerSettings.WebGL.template = "PROJECT:YandexGames";
            // без заставки Unity: минус 2,7 МБ и задержка на старте
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            long bytes = Directory.Exists(OutDir) ? new DirectoryInfo(OutDir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            string msg = $"Build {s.result}: {bytes / 1048576f:0.0} MB, {s.totalTime.TotalSeconds:0}s, errors {s.totalErrors}";
            Debug.Log("[AllOnShelves] " + msg);
            File.WriteAllText("Temp/build_result.txt", msg);
            if (s.result == BuildResult.Succeeded)
            {
                // плагин YG2 сам архивирует сборку в WebGL_*_Build(N).zip — оставляем один архив
                foreach (var dup in Directory.GetFiles("Builds", "WebGL_*_Build(*).zip")) File.Delete(dup);
                string zip = "Builds/AllOnShelves_WebGL.zip";
                if (File.Exists(zip)) File.Delete(zip);
                System.IO.Compression.ZipFile.CreateFromDirectory(OutDir, zip);
                msg += $"; zip {new FileInfo(zip).Length / 1048576f:0.0} MB";
            }
            return msg;
        }
    }
}
// build 1789759595
// build 1789759705
