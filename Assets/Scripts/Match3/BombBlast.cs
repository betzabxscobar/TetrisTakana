using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// El fogonazo de una bomba: pasa la secuencia de fotogramas una vez y se
    /// destruye sola.
    ///
    /// Vive en su propio objeto y no en la ficha porque la ficha desaparece en
    /// el mismo instante en que explota: si la animacion colgase de ella, se
    /// iria con el Destroy y no se veria nada.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BombBlast : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float frameTime = 0.05f;
        private float timer;
        private int frame;

        /// <summary>
        /// Crea el fogonazo en un punto del mundo. <paramref name="size"/> es
        /// lo que debe ocupar de ancho, en unidades: se le pasa el tamaño real
        /// del hueco que abre para que el dibujo cuadre con las celdas que
        /// desaparecen.
        /// </summary>
        public static BombBlast Play(
            Sprite[] frames,
            Vector3 worldPosition,
            float size,
            float frameTime,
            int sortingOrder,
            Transform parent = null)
        {
            if (frames == null || frames.Length == 0)
                return null;

            GameObject instance = new GameObject("BombBlast");
            instance.transform.SetParent(parent, false);
            instance.transform.position = worldPosition;

            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = sortingOrder;

            BombBlast blast = instance.AddComponent<BombBlast>();
            blast.spriteRenderer = renderer;
            blast.frames = frames;
            blast.frameTime = Mathf.Max(0.01f, frameTime);
            blast.FitTo(size);

            return blast;
        }

        private void Awake()
        {
            spriteRenderer ??= GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            timer += Time.deltaTime;

            if (timer < frameTime)
                return;

            timer -= frameTime;
            frame++;

            if (frame >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            spriteRenderer.sprite = frames[frame];
        }

        /// <summary>
        /// Escala el objeto para que el primer fotograma mida lo pedido. Se usa
        /// el primero como referencia y no cada uno: los fotogramas crecen entre
        /// si a proposito, y reescalando uno a uno la explosion se quedaria del
        /// mismo tamaño toda la secuencia.
        /// </summary>
        private void FitTo(float size)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null || size <= 0f)
                return;

            float widest = 0f;

            foreach (Sprite candidate in frames)
                if (candidate != null)
                    widest = Mathf.Max(widest, candidate.bounds.size.x);

            if (widest <= 0f)
                return;

            float scale = size / widest;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
