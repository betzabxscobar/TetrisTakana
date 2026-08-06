using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TetrisTakana
{
    /// <summary>
    /// Controla la tarjeta de ayuda y la anima desde la parte inferior de la
    /// pantalla. El prefab puede reutilizarse en cualquier Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class HelpCardController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Button closeButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animacion")]
        [SerializeField, Min(0.05f)] private float entranceDuration = 0.45f;
        [SerializeField, Min(0.05f)] private float exitDuration = 0.3f;
        [SerializeField, Min(0f)] private float hiddenExtraDistance = 48f;

        private Vector2 visiblePosition;
        private Vector2 hiddenPosition;
        private Coroutine animationRoutine;
        private bool isVisible;

        /// <summary>Recoge sus piezas y deja la tarjeta escondida.</summary>
        private void Awake()
        {
            cardRect ??= GetComponent<RectTransform>();
            canvasGroup ??= GetComponent<CanvasGroup>();
            closeButton ??= GetComponentInChildren<Button>(true);

            if (cardRect == null || canvasGroup == null)
            {
                Debug.LogError("La tarjeta de ayuda necesita RectTransform y CanvasGroup.", this);
                enabled = false;
                return;
            }

            visiblePosition = cardRect.anchoredPosition;
            hiddenPosition = visiblePosition + Vector2.down *
                (cardRect.rect.height + hiddenExtraDistance);

            if (closeButton != null)
                closeButton.onClick.AddListener(Ocultar);
            else
                Debug.LogWarning("No se encontro el boton Cerrar en la tarjeta de ayuda.", this);

            SetHiddenInstant();
        }

        /// <summary>Suelta el boton de cerrar.</summary>
        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Ocultar);
        }

        /// <summary>Abre o cierra la tarjeta desde el boton Ayuda.</summary>
        public void AlternarTarjeta()
        {
            if (isVisible)
                Ocultar();
            else
                Mostrar();
        }

        /// <summary>Despliega la tarjeta desde abajo.</summary>
        public void Mostrar()
        {
            if (!enabled || cardRect == null || canvasGroup == null)
                return;

            StopAnimation();
            isVisible = true;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            animationRoutine = StartCoroutine(AnimateCard(
                cardRect.anchoredPosition,
                visiblePosition,
                canvasGroup.alpha,
                1f,
                entranceDuration,
                true));
        }

        /// <summary>Oculta la tarjeta hacia la parte inferior.</summary>
        public void Ocultar()
        {
            if (!enabled || cardRect == null || canvasGroup == null)
                return;

            if (!isVisible && animationRoutine == null)
                return;

            StopAnimation();
            isVisible = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            animationRoutine = StartCoroutine(AnimateCard(
                cardRect.anchoredPosition,
                hiddenPosition,
                canvasGroup.alpha,
                0f,
                exitDuration,
                false));
        }

        /// <summary>Mueve y funde la tarjeta entre dos posiciones.</summary>
        private IEnumerator AnimateCard(
            Vector2 startPosition,
            Vector2 targetPosition,
            float startAlpha,
            float targetAlpha,
            float duration,
            bool visibleAtEnd)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = visibleAtEnd
                    ? EaseOutCubic(progress)
                    : EaseInCubic(progress);

                cardRect.anchoredPosition = Vector2.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    easedProgress);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, easedProgress);
                yield return null;
            }

            cardRect.anchoredPosition = targetPosition;
            canvasGroup.alpha = targetAlpha;
            animationRoutine = null;

            if (!visibleAtEnd)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>Deja la tarjeta escondida de golpe, sin animar.</summary>
        private void SetHiddenInstant()
        {
            StopAnimation();
            isVisible = false;
            cardRect.anchoredPosition = hiddenPosition;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        /// <summary>Corta la animacion si estaba a medias.</summary>
        private void StopAnimation()
        {
            if (animationRoutine == null)
                return;

            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        /// <summary>Curva que arranca rapido y frena al final.</summary>
        private static float EaseOutCubic(float progress)
        {
            return 1f - Mathf.Pow(1f - progress, 3f);
        }

        /// <summary>Curva que arranca lento y acelera al final.</summary>
        private static float EaseInCubic(float progress)
        {
            return progress * progress * progress;
        }
    }
}
