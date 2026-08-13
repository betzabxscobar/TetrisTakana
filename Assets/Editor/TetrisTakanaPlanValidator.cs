#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TetrisTakana.Editor
{
    /// <summary>
    /// Comprobaciones rápidas del plan que no necesitan entrar en Play Mode.
    /// Se ejecutan desde Tetris Takana/Validar plan y dejan los fallos en la
    /// consola para que no haya que recordar una lista manual de archivos.
    /// </summary>
    public static class TetrisTakanaPlanValidator
    {
        private static readonly string[] RequiredScenes =
        {
            "Menu",
            "Game",
            "Credits",
            "Puntuaciones",
            "Ayuda"
        };

        [MenuItem("Tetris Takana/Validar plan")]
        public static void ValidatePlan()
        {
            int errors = 0;
            int warnings = 0;

            foreach (string scene in RequiredScenes)
            {
                if (!SceneIsInBuildSettings(scene))
                {
                    Debug.LogError(
                        $"La escena requerida '{scene}' no esta en Build Settings.");
                    errors++;
                }
            }

            errors += RequireFile("Assets/Scripts/UI/SceneTransitionManager.cs");
            errors += RequireFile("Assets/Tests/Editor/Match3CoreTests.cs");
            warnings += WarnIfMissing("Assets/Tests/PlayMode/SceneTransitionSmokeTests.cs");

            string[] scriptFiles = Directory.GetFiles(
                Application.dataPath,
                "*.cs",
                SearchOption.AllDirectories);

            foreach (string file in scriptFiles)
            {
                // Este validador contiene el texto que busca dentro de su
                // propio codigo; excluirlo evita denunciar la cadena literal
                // como si fuera una llamada de produccion.
                string normalizedFile = file.Replace('\\', '/');

                if (normalizedFile.Contains("/Tests/") ||
                    file.EndsWith("SceneTransitionManager.cs") ||
                    file.EndsWith("TetrisTakanaPlanValidator.cs"))
                    continue;

                string source = File.ReadAllText(file);

                if (!source.Contains("SceneManager.LoadScene("))
                    continue;

                Debug.LogError(
                    $"Carga de escena directa encontrada en '{ToProjectPath(file)}'. " +
                    "Usa SceneTransitionManager.LoadScene().");
                errors++;
            }

            string gameScene = "Assets/Scenes/Game.unity";
            if (File.Exists(ProjectFile(gameScene)))
            {
                string source = File.ReadAllText(ProjectFile(gameScene));

                RequireSerializedField(source, "ensurePlayableStart", ref errors);
                RequireSerializedField(source, "risingStack:", ref errors);
                RequireSerializedField(source, "lineClearClip:", ref errors);
                RequireSerializedField(source, "matchClearClip:", ref errors);
            }
            else
            {
                Debug.LogError($"No se encontro '{gameScene}'.");
                errors++;
            }

            if (errors == 0)
                Debug.Log($"Tetris Takana: validacion del plan correcta ({warnings} advertencias).");
            else
                Debug.LogError($"Tetris Takana: validacion del plan con {errors} error(es) y {warnings} advertencia(s).");
        }

        [MenuItem("Tetris Takana/Validar plan", true)]
        private static bool ValidatePlanMenu()
        {
            return !EditorApplication.isCompiling;
        }

        private static int RequireFile(string projectPath)
        {
            if (File.Exists(ProjectFile(projectPath)))
                return 0;

            Debug.LogError($"Falta el archivo requerido '{projectPath}'.");
            return 1;
        }

        private static int WarnIfMissing(string projectPath)
        {
            if (File.Exists(ProjectFile(projectPath)))
                return 0;

            Debug.LogWarning($"No existe la prueba opcional '{projectPath}'.");
            return 1;
        }

        private static void RequireSerializedField(
            string source,
            string field,
            ref int errors)
        {
            if (source.Contains(field))
                return;

            Debug.LogError($"Game.unity no contiene el campo esperado '{field}'.");
            errors++;
        }

        private static string ToProjectPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            return normalized.StartsWith(dataPath)
                ? "Assets" + normalized.Substring(dataPath.Length)
                : normalized;
        }

        private static string ProjectFile(string projectPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectPath);
        }

        private static bool SceneIsInBuildSettings(string sceneName)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                    continue;

                if (Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                    return true;
            }

            return false;
        }
    }
}
#endif
