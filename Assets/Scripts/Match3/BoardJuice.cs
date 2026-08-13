using System.Collections;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// El golpe de vista de romper fichas: el reventon de cada ficha y la
    /// sacudida de camara.
    ///
    /// Antes las fichas se destruian de golpe con un Destroy, que es
    /// exactamente lo que hace que un match-3 se sienta muerto: el jugador
    /// acierta y la pantalla no le contesta nada. Aqui la ficha se queda un
    /// cuarto de segundo mas, da un respingo, se pone blanca y se apaga.
    ///
    /// Todo lo lleva este componente y no cada ficha porque la ficha ya no
    /// existe para el tablero cuando revienta: si la animacion colgase de ella
    /// habria que retrasar el borrado del tablero, y eso descuadra la gravedad
    /// y la deteccion de combinaciones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardJuice : MonoBehaviour
    {
        [Header("Reventon de la ficha")]
        [Tooltip("Lo que tarda una ficha en desaparecer del todo.")]
        [SerializeField, Min(0.02f)] private float popDuration = 0.24f;
        [Tooltip("Cuanto se hincha antes de apagarse.")]
        [SerializeField, Min(1f)] private float popOvershoot = 1.4f;
        [Tooltip("Cuanto sube mientras se apaga, en unidades.")]
        [SerializeField] private float popRise = 0.18f;
        [Tooltip("Parte del reventon que pasa poniendose blanca.")]
        [SerializeField, Range(0f, 1f)] private float popFlash = 0.35f;
        [Tooltip("Retraso entre fichas segun lo lejos que esten del centro.")]
        [SerializeField, Min(0f)] private float popStagger = 0.03f;

        [Header("Sacudida de camara")]
        [SerializeField] private bool shakeEnabled = true;
        [Tooltip("Desvio maximo de la camara, en unidades.")]
        [SerializeField, Min(0f)] private float shakeStrength = 0.22f;
        [SerializeField, Min(0.05f)] private float shakeDuration = 0.25f;
        [SerializeField, Min(1f)] private float shakeSpeed = 38f;

        private Camera view;
        private Vector3 appliedOffset;
        private float shakeTimer = -1f;
        private float shakeScale = 1f;

        private void Awake()
        {
            view = Camera.main;
        }

        /// <summary>
        /// La sacudida se aplica en LateUpdate y descontando la del fotograma
        /// anterior. Asi convive con BoardCameraFitter, que recoloca la camara
        /// por su cuenta: si se guardase una posicion de reposo, el primer
        /// reencuadre la dejaria clavada donde estuviera al empezar a temblar.
        /// </summary>
        private void LateUpdate()
        {
            if (view == null)
            {
                view = Camera.main;
                return;
            }

            view.transform.position -= appliedOffset;
            appliedOffset = Vector3.zero;

            if (shakeTimer >= 0f)
            {
                shakeTimer += Time.deltaTime;

                if (shakeTimer >= shakeDuration)
                {
                    shakeTimer = -1f;
                }
                else
                {
                    float fade = 1f - Mathf.Clamp01(shakeTimer / shakeDuration);
                    float amount = shakeStrength * shakeScale * fade * fade;

                    appliedOffset = new Vector3(
                        Mathf.Sin(shakeTimer * shakeSpeed) * amount,
                        Mathf.Cos(shakeTimer * shakeSpeed * 1.37f) * amount * 0.6f,
                        0f);
                }
            }

            view.transform.position += appliedOffset;
        }

        /// <summary>
        /// Sacude la camara. <paramref name="strength"/> es un multiplicador
        /// sobre la fuerza configurada; una combinacion normal manda 1 y una
        /// explosion manda mas.
        /// </summary>
        public void Shake(float strength)
        {
            if (!shakeEnabled || strength <= 0f)
                return;

            // Una sacudida nueva no corta la anterior si esta es mas floja: al
            // encadenar bombas eso dejaba temblores cada vez mas pequeños.
            if (shakeTimer >= 0f && strength < shakeScale)
                return;

            shakeTimer = 0f;
            shakeScale = strength;
        }

        /// <summary>
        /// Se queda con una ficha que el tablero acaba de soltar y la revienta.
        /// <paramref name="delayOrder"/> escalona el reventon dentro de una
        /// misma combinacion, que rompiendolas todas a la vez se lee como un
        /// parpadeo y escalonadas se lee como una racha.
        /// </summary>
        public void PopBlock(BoardBlock block, int delayOrder = 0)
        {
            if (block == null)
                return;

            StartCoroutine(PopRoutine(block.gameObject, delayOrder * popStagger));
        }

        private IEnumerator PopRoutine(GameObject target, float delay)
        {
            if (target == null)
                yield break;

            // La ficha ya no es del tablero: se le quitan los componentes que
            // sigan actuando sobre ella, o la bomba seguiria latiendo mientras
            // se apaga.
            foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
                behaviour.enabled = false;

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            Transform tr = target.transform;
            Vector3 baseScale = tr.localScale;
            Vector3 basePosition = tr.position;
            Color baseColor = renderer != null ? renderer.color : Color.white;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;

            while (elapsed < popDuration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);

                // Se hincha de golpe y se desinfla: el pico temprano es lo que
                // da la sensacion de golpe en vez de la de desvanecerse.
                float grow = t < 0.3f
                    ? Mathf.Lerp(1f, popOvershoot, t / 0.3f)
                    : Mathf.Lerp(popOvershoot, 0f, (t - 0.3f) / 0.7f);

                tr.localScale = baseScale * grow;
                tr.position = basePosition + Vector3.up * (popRise * t);

                if (renderer != null)
                {
                    Color tint = t < popFlash
                        ? Color.Lerp(baseColor, Color.white, t / Mathf.Max(0.01f, popFlash))
                        : Color.white;

                    tint.a = baseColor.a * (1f - Mathf.Clamp01((t - 0.4f) / 0.6f));
                    renderer.color = tint;
                }

                yield return null;
            }

            if (target != null)
                Destroy(target);
        }
    }
}
