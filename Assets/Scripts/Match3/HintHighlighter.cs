using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// Hace latir las dos fichas que la mascota esta señalando. Sin esto la
    /// mascota apunta hacia una fila entera y el jugador tiene que adivinar la
    /// columna: el latido dice cuales son las dos exactas.
    ///
    /// Guarda el color de origen de cada ficha y lo devuelve al soltarlas. Las
    /// fichas se destruyen al romperse, asi que todo lo que toca esta clase se
    /// comprueba antes de usarlo: la pista puede seguir viva justo cuando el
    /// jugador la ejecuta y el tablero se lleva las dos por delante.
    /// </summary>
    public sealed class HintHighlighter
    {
        private SpriteRenderer firstRenderer;
        private SpriteRenderer secondRenderer;
        private Color firstColor;
        private Color secondColor;
        private float timer;

        /// <summary>Hay una pareja de fichas marcada ahora mismo.</summary>
        public bool IsHighlighting => firstRenderer != null || secondRenderer != null;

        /// <summary>Empieza a marcar dos fichas, soltando las anteriores.</summary>
        public void Begin(BoardBlock first, BoardBlock second)
        {
            Clear();

            firstRenderer = GetRenderer(first);
            secondRenderer = GetRenderer(second);

            if (firstRenderer != null)
                firstColor = firstRenderer.color;

            if (secondRenderer != null)
                secondColor = secondRenderer.color;

            timer = 0f;
        }

        /// <summary>
        /// Lleva el latido. <paramref name="strength"/> es cuanto se aclara la
        /// ficha en el punto mas alto, y <paramref name="speed"/> los latidos
        /// por segundo.
        /// </summary>
        public void Tick(float delta, float strength, float speed)
        {
            if (!IsHighlighting)
                return;

            timer += delta;

            // De cero a uno y vuelta, sin llegar nunca a apagar la ficha: un
            // parpadeo a negro se lee como un fallo de dibujado, no como aviso.
            float pulse = (Mathf.Sin(timer * speed * Mathf.PI * 2f) + 1f) * 0.5f;

            Apply(firstRenderer, firstColor, pulse * strength);
            Apply(secondRenderer, secondColor, pulse * strength);
        }

        /// <summary>Devuelve las fichas a su color y deja de marcarlas.</summary>
        public void Clear()
        {
            Restore(firstRenderer, firstColor);
            Restore(secondRenderer, secondColor);

            firstRenderer = null;
            secondRenderer = null;
        }

        /// <summary>Aclara la ficha hacia el blanco sin tocar su transparencia.</summary>
        private static void Apply(SpriteRenderer renderer, Color origin, float amount)
        {
            if (renderer == null)
                return;

            Color lit = Color.Lerp(origin, Color.white, Mathf.Clamp01(amount));
            lit.a = origin.a;
            renderer.color = lit;
        }

        /// <summary>Devuelve una ficha a su color, si sigue existiendo.</summary>
        private static void Restore(SpriteRenderer renderer, Color origin)
        {
            if (renderer != null)
                renderer.color = origin;
        }

        /// <summary>Saca el dibujante de una ficha, o null si ya no hay ficha.</summary>
        private static SpriteRenderer GetRenderer(BoardBlock block)
        {
            return block != null ? block.GetComponent<SpriteRenderer>() : null;
        }
    }
}
