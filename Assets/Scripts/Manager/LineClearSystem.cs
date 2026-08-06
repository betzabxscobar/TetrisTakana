using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TetrisTakana
{
    /// <summary>
    /// Detecta las filas completas, las hace parpadear, las elimina y baja
    /// todo lo que quedaba por encima.
    /// </summary>
    public class LineClearSystem : MonoBehaviour
    {
        [SerializeField] private Board board;

        [Header("Parpadeo")]
        [SerializeField, Min(0)] private int flashCount = 3;
        [SerializeField, Min(0f)] private float flashInterval = 0.06f;
        [SerializeField] private Color flashColor = Color.white;

        [Header("Sonido")]
        [SerializeField] private AudioClip destroyBlocksClip;
        [SerializeField, Range(0f, 1f)] private float destroyBlocksVolume = 0.8f;

        [Header("Caída de las filas superiores")]
        [SerializeField, Min(0f)] private float collapseDuration = 0.12f;

        private readonly List<int> fullLines = new List<int>();
        private readonly List<SpriteRenderer> flashing = new List<SpriteRenderer>();
        private readonly List<Color> originalColors = new List<Color>();
        private readonly List<CollapsingBlock> collapsing = new List<CollapsingBlock>();

        /// <summary>Líneas eliminadas en la última llamada a <see cref="ClearFullLines"/>.</summary>
        public int LastClearedCount { get; private set; }

        public event Action<int> LinesCleared;

        /// <summary>Un bloque bajando al hueco que dejo una linea limpiada.</summary>
        private struct CollapsingBlock
        {
            public BoardBlock Block;
            public Vector3 From;
            public Vector3 To;
        }

        /// <summary>Coge el tablero del propio objeto si no viene asignado.</summary>
        private void Awake()
        {
            if (board == null)
                board = GetComponent<Board>();
        }

        /// <summary>Devuelve las filas en las que todas las celdas están ocupadas.</summary>
        public List<int> FindFullLines()
        {
            fullLines.Clear();

            if (board == null)
                return fullLines;

            for (int y = 0; y < board.Height; y++)
            {
                bool complete = true;

                for (int x = 0; x < board.Width && complete; x++)
                    if (!board.IsOccupied(new Vector2Int(x, y)))
                        complete = false;

                if (complete)
                    fullLines.Add(y);
            }

            return fullLines;
        }

        /// <summary>Busca las filas completas, las hace parpadear, las borra y baja el resto.</summary>
        public IEnumerator ClearFullLines()
        {
            LastClearedCount = 0;

            if (board == null)
                yield break;

            // Copia propia: FindFullLines reutiliza su lista interna.
            List<int> lines = new List<int>(FindFullLines());

            if (lines.Count == 0)
                yield break;

            LastClearedCount = lines.Count;

            yield return FlashLines(lines);
            RemoveLines(lines);
            yield return CollapseAbove(lines);

            LinesCleared?.Invoke(LastClearedCount);
        }

        /// <summary>Hace parpadear en blanco las lineas que van a desaparecer.</summary>
        private IEnumerator FlashLines(List<int> lines)
        {
            if (flashCount <= 0 || flashInterval <= 0f)
                yield break;

            flashing.Clear();
            originalColors.Clear();

            foreach (int y in lines)
            for (int x = 0; x < board.Width; x++)
            {
                BoardBlock block = board.GetBlock(new Vector2Int(x, y));
                SpriteRenderer renderer = block != null
                    ? block.GetComponent<SpriteRenderer>()
                    : null;

                if (renderer == null)
                    continue;

                flashing.Add(renderer);
                originalColors.Add(renderer.color);
            }

            for (int flash = 0; flash < flashCount; flash++)
            {
                SetFlashColor(flashColor);
                yield return new WaitForSeconds(flashInterval);

                RestoreColors();
                yield return new WaitForSeconds(flashInterval);
            }

            flashing.Clear();
            originalColors.Clear();
        }

        /// <summary>Tiñe de golpe todos los bloques que estan parpadeando.</summary>
        private void SetFlashColor(Color color)
        {
            foreach (SpriteRenderer renderer in flashing)
                if (renderer != null)
                    renderer.color = color;
        }

        /// <summary>Devuelve a los bloques su color de siempre.</summary>
        private void RestoreColors()
        {
            for (int i = 0; i < flashing.Count; i++)
                if (flashing[i] != null)
                    flashing[i].color = originalColors[i];
        }

        /// <summary>Borra del tablero las filas completas.</summary>
        private void RemoveLines(List<int> lines)
        {
            PlayDestroyBlocksSfx();

            foreach (int y in lines)
            for (int x = 0; x < board.Width; x++)
                board.RemoveBlock(new Vector2Int(x, y));
        }

        /// <summary>Suena justo cuando desaparecen los bloques de las lineas completas.</summary>
        private void PlayDestroyBlocksSfx()
        {
            if (destroyBlocksClip == null)
                return;

            AudioManager audioManager = AudioManager.EnsureInstance();

            if (audioManager == null)
                return;

            float previousVolume = audioManager.SfxVolume;
            audioManager.SfxVolume = destroyBlocksVolume;
            audioManager.PlaySfx(destroyBlocksClip);
            audioManager.SfxVolume = previousVolume;
        }

        /// <summary>
        /// Baja cada fila tantas celdas como líneas eliminadas haya por debajo.
        /// </summary>
        private IEnumerator CollapseAbove(List<int> lines)
        {
            collapsing.Clear();
            int shift = 0;

            for (int y = 0; y < board.Height; y++)
            {
                if (lines.Contains(y))
                {
                    shift++;
                    continue;
                }

                if (shift == 0)
                    continue;

                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int source = new Vector2Int(x, y);
                    BoardBlock block = board.GetBlock(source);

                    if (block == null)
                        continue;

                    Vector2Int destination = new Vector2Int(x, y - shift);
                    Vector3 from = block.transform.position;

                    board.SetBlock(source, null);
                    board.SetBlock(destination, block, false);

                    collapsing.Add(new CollapsingBlock
                    {
                        Block = block,
                        From = from,
                        To = board.GridToWorld(destination)
                    });
                }
            }

            yield return AnimateCollapse();
        }

        /// <summary>Anima la bajada de la pila hasta cerrar los huecos.</summary>
        private IEnumerator AnimateCollapse()
        {
            if (collapsing.Count == 0)
                yield break;

            if (collapseDuration > 0f)
            {
                float elapsed = 0f;

                while (elapsed < collapseDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / collapseDuration);

                    foreach (CollapsingBlock fall in collapsing)
                        if (fall.Block != null)
                            fall.Block.transform.position = Vector3.Lerp(
                                fall.From,
                                fall.To,
                                progress * progress);

                    yield return null;
                }
            }

            foreach (CollapsingBlock fall in collapsing)
                if (fall.Block != null)
                    fall.Block.transform.position = fall.To;

            collapsing.Clear();
        }
    }
}
