using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TetrisTakana;
using TetrisTakana.Match3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TetrisTakana.Tests
{
    /// <summary>
    /// Comprueba el recorrido real de la cortina y la carga asíncrona. El
    /// timeout evita que una regresión deje el Test Runner esperando para
    /// siempre si una escena no activa.
    /// </summary>
    public sealed class SceneTransitionSmokeTests
    {
        private static readonly string[] MenuDestinations =
        {
            "Game",
            "Ayuda",
            "Credits",
            "Puntuaciones"
        };

        [TearDown]
        public void RestoreTimeScale()
        {
            UnityEngine.Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator MenuCreditsMenuCompletesTransition()
        {
            yield return LoadSceneDirectly("Menu");

            Assert.AreEqual("Menu", SceneManager.GetActiveScene().name);
            Assert.IsTrue(SceneTransitionManager.LoadScene("Credits"));
            Assert.IsTrue(SceneTransitionManager.IsTransitioning);
            Assert.IsFalse(SceneTransitionManager.LoadScene("Menu"));

            yield return WaitForScene("Credits", 5f);
            Assert.AreEqual("Credits", SceneManager.GetActiveScene().name);
            Assert.IsFalse(SceneTransitionManager.IsTransitioning);

            Assert.IsTrue(SceneTransitionManager.LoadScene("Menu"));
            yield return WaitForScene("Menu", 5f);
            Assert.AreEqual("Menu", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator MenuCanReachEveryBuildSceneAndReturn()
        {
            yield return LoadSceneDirectly("Menu");

            foreach (string destination in MenuDestinations)
            {
                Assert.IsTrue(SceneTransitionManager.LoadScene(destination));
                yield return WaitForScene(destination, 5f);
                Assert.IsFalse(SceneTransitionManager.IsTransitioning);

                Assert.IsTrue(SceneTransitionManager.LoadScene("Menu"));
                yield return WaitForScene("Menu", 5f);
            }
        }

        [UnityTest]
        public IEnumerator TransitionRunsWithTimeScaleZero()
        {
            yield return LoadSceneDirectly("Menu");
            UnityEngine.Time.timeScale = 0f;

            Assert.IsTrue(SceneTransitionManager.LoadScene("Credits"));
            yield return WaitForScene("Credits", 5f);
            Assert.AreEqual("Credits", SceneManager.GetActiveScene().name);

            // Este test entra directamente al gestor, sin la tarjeta de pausa
            // que normalmente restaura el tiempo antes de navegar.
            UnityEngine.Time.timeScale = 1f;
            Assert.IsTrue(SceneTransitionManager.LoadScene("Menu"));
            yield return WaitForScene("Menu", 5f);
        }

        [UnityTest]
        public IEnumerator PauseCardReturnsToMenuAndRestoresTimeScale()
        {
            yield return LoadSceneDirectly("Game");

            Match3Game game = null;
            yield return WaitForPlayingGame(gameValue => game = gameValue, 5f);
            Assert.IsNotNull(game);

            game.TogglePause();
            Assert.AreEqual(BoardGame.GameState.Paused, game.State);
            Assert.AreEqual(0f, UnityEngine.Time.timeScale);

            yield return WaitForActiveButton("Menu Button", 5f);
            Button menuButton = FindActiveButton("Menu Button");
            Assert.IsNotNull(menuButton);
            menuButton.onClick.Invoke();

            yield return WaitForScene("Menu", 5f);
            Assert.AreEqual(1f, UnityEngine.Time.timeScale);
        }

        [UnityTest]
        public IEnumerator GameOverCardRestartsAndReturnsToMenu()
        {
            yield return LoadSceneDirectly("Menu");
            Assert.IsTrue(SceneTransitionManager.LoadScene("Game"));
            yield return WaitForScene("Game", 5f);

            Match3Game game = null;
            yield return WaitForPlayingGame(gameValue => game = gameValue, 5f);
            Assert.IsNotNull(game);

            InvokePrivateEndGame(game);
            yield return WaitForActiveButton("Replay Button", 5f);
            Button replayButton = FindActiveButton("Replay Button");
            Assert.IsNotNull(replayButton);
            replayButton.onClick.Invoke();

            yield return WaitForScene("Game", 5f);
            Assert.AreEqual(1f, UnityEngine.Time.timeScale);

            game = null;
            yield return WaitForPlayingGame(gameValue => game = gameValue, 5f);
            Assert.IsNotNull(game);
            InvokePrivateEndGame(game);
            yield return WaitForActiveButton("Menu Button", 5f);

            Button menuButton = FindActiveButton("Menu Button");
            Assert.IsNotNull(menuButton);
            menuButton.onClick.Invoke();
            yield return WaitForScene("Menu", 5f);
            Assert.AreEqual(1f, UnityEngine.Time.timeScale);
        }

        [UnityTest]
        public IEnumerator GameStartsWithAConcreteMove()
        {
            yield return LoadSceneDirectly("Game");

            Match3Game game = null;
            yield return WaitForPlayingGame(gameValue => game = gameValue, 5f);
            Assert.IsNotNull(game);

            float elapsed = 0f;
            while (game.IsBusy && elapsed < 5f)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(game.IsBusy, "El llenado inicial no terminó a tiempo.");
            Assert.IsTrue(
                game.HasConcreteMove(),
                "La distribución inicial no contiene ningún intercambio válido.");
        }

        [UnityTest]
        public IEnumerator DirectSceneEntryBootstrapsNavigationManager()
        {
            string[] scenes = { "Menu", "Game", "Ayuda", "Credits", "Puntuaciones" };

            foreach (string scene in scenes)
            {
                SceneTransitionManager manager =
                    Object.FindAnyObjectByType<SceneTransitionManager>();

                if (manager != null)
                {
                    Object.Destroy(manager.gameObject);
                    yield return null;
                }

                yield return LoadSceneDirectly(scene);
                Assert.AreEqual(scene, SceneManager.GetActiveScene().name);

                string destination = scene == "Menu" ? "Game" : "Menu";
                Assert.IsTrue(SceneTransitionManager.LoadScene(destination));
                yield return WaitForScene(destination, 5f);
                Assert.AreEqual(destination, SceneManager.GetActiveScene().name);
            }
        }

        [UnityTest]
        public IEnumerator RisingStackPushesExistingBlocksUpAndFillsBottom()
        {
            yield return LoadSceneDirectly("Game");

            Match3Game game = null;
            yield return WaitForPlayingGame(gameValue => game = gameValue, 5f);
            Assert.IsNotNull(game);

            float elapsed = 0f;
            while (game.IsBusy && elapsed < 5f)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }

            Board board = Object.FindAnyObjectByType<Board>();
            RisingStack stack = Object.FindAnyObjectByType<RisingStack>();
            Assert.IsNotNull(board);
            Assert.IsNotNull(stack);

            BoardBlock tracked = null;
            Vector2Int before = default;
            for (int y = 0; y < board.Height - 1 && tracked == null; y++)
            for (int x = 0; x < board.Width; x++)
            {
                BoardBlock candidate = board.GetBlock(new Vector2Int(x, y));
                if (candidate != null)
                {
                    tracked = candidate;
                    before = new Vector2Int(x, y);
                    break;
                }
            }

            Assert.IsNotNull(tracked, "El tablero inicial no tenía una ficha para seguir.");

            // El test aísla el desplazamiento de la pila: la resolución de una
            // combinación nueva se cubre en Match3CoreTests y aquí no debe
            // hacer desaparecer la ficha que estamos siguiendo.
            SetPrivate(stack, "matchSystem", null);
            SetPrivate(stack, "pushDuration", 0f);
            MethodInfo pushMethod = typeof(RisingStack).GetMethod(
                "PushRow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(pushMethod);

            bool rowPushed = false;
            stack.RowPushed += () => rowPushed = true;
            yield return game.StartCoroutine((IEnumerator)pushMethod.Invoke(stack, null));

            Assert.IsTrue(rowPushed, "La pila no notificó la fila empujada.");
            Assert.AreSame(tracked, board.GetBlock(new Vector2Int(before.x, before.y + 1)));

            bool bottomFilled = false;
            for (int x = 0; x < board.Width; x++)
                bottomFilled |= board.GetBlock(new Vector2Int(x, 0)) != null;

            Assert.IsTrue(bottomFilled, "La pila no creó una fila nueva desde abajo.");
        }

        private static IEnumerator LoadSceneDirectly(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            Assert.IsNotNull(operation, $"No se pudo iniciar '{sceneName}'.");

            while (!operation.isDone)
                yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout)
        {
            float elapsed = 0f;

            while ((SceneManager.GetActiveScene().name != sceneName ||
                    SceneTransitionManager.IsTransitioning) &&
                   elapsed < timeout)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
            Assert.IsFalse(SceneTransitionManager.IsTransitioning);
        }

        private static IEnumerator WaitForPlayingGame(
            System.Action<Match3Game> assign,
            float timeout)
        {
            float elapsed = 0f;
            Match3Game game = null;

            while ((game = Object.FindAnyObjectByType<Match3Game>()) == null ||
                   game.State != BoardGame.GameState.Playing)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                if (elapsed >= timeout)
                    break;

                yield return null;
            }

            assign(game);
        }

        private static IEnumerator WaitForActiveButton(string objectName, float timeout)
        {
            float elapsed = 0f;

            while (FindActiveButton(objectName) == null && elapsed < timeout)
            {
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(
                FindActiveButton(objectName),
                $"No apareció el botón activo '{objectName}'.");
        }

        private static Button FindActiveButton(string objectName)
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);

            foreach (Button button in buttons)
                if (button != null && button.name == objectName && button.gameObject.activeInHierarchy)
                    return button;

            return null;
        }

        private static void InvokePrivateEndGame(Match3Game game)
        {
            MethodInfo endGame = typeof(Match3Game).GetMethod(
                "EndGame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(endGame, "No se encontró el cierre de partida del modo Match-3.");
            endGame.Invoke(game, null);
        }

        private static void SetPrivate(Object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"No se encontró el campo privado '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
