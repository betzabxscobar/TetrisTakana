using System.Collections;
using NUnit.Framework;
using TetrisTakana;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
    }
}
