using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TetrisTakana.EditorTools
{
    /// <summary>
    /// Compila el juego para web sin abrir el editor, que es lo que hace falta
    /// para subirlo a cualquier sitio (itch.io, GitHub Pages, Netlify): todos
    /// sirven la carpeta que sale de aqui tal cual.
    ///
    /// Las escenas salen de Build Settings y no de una lista propia, para que
    /// no haya dos sitios donde acordarse de dar de alta una escena nueva.
    /// </summary>
    public static class WebBuilder
    {
        /// <summary>Donde queda el resultado. El .gitignore ya se salta Build/.</summary>
        private const string OutputPath = "Build/Web";

        /// <summary>Compila para web desde el menu del editor.</summary>
        [MenuItem("TetrisTakana/Compilar para web")]
        public static void BuildWeb()
        {
            string[] scenes = EnabledScenes();

            if (scenes.Length == 0)
            {
                Fail("No hay ninguna escena activada en Build Settings.");
                return;
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"La compilacion termino en {summary.result} " +
                     $"con {summary.totalErrors} errores.");
                return;
            }

            Debug.Log(
                $"Compilacion web lista en '{OutputPath}': " +
                $"{summary.totalSize / (1024 * 1024)} MB en " +
                $"{summary.totalTime.TotalSeconds:F0} segundos.");
        }

        /// <summary>Las escenas marcadas en Build Settings, en su orden.</summary>
        private static string[] EnabledScenes()
        {
            List<string> scenes = new List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    scenes.Add(scene.path);

            return scenes.ToArray();
        }

        /// <summary>
        /// Deja constancia del fallo y devuelve un codigo de salida distinto de
        /// cero: en batch mode, sin esto Unity termina como si todo hubiera ido
        /// bien y una compilacion rota pasaria desapercibida.
        /// </summary>
        private static void Fail(string message)
        {
            Debug.LogError(message);

            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
