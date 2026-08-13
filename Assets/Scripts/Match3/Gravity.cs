using System;
using System.Collections;
using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>Hace caer las fichas hasta cerrar los huecos que dejan las combinaciones.</summary>
    public class Gravity : MonoBehaviour
    {
        [SerializeField] private Board board;
        [Tooltip("El aplastado al aterrizar. Vacio: las fichas se paran en seco.")]
        [SerializeField] private BoardJuice juice;
        [SerializeField, Min(0f)] private float cellMoveDuration = 0.06f;

        public event Action GravityCompleted;

        /// <summary>Coge el tablero del propio objeto si no viene asignado.</summary>
        private void Awake()
        {
            if (board == null)
                board = GetComponent<Board>();

            if (juice == null)
                juice = GetComponent<BoardJuice>();
        }

        /// <summary>Baja cada columna hasta que no queden huecos por debajo de una ficha.</summary>
        public IEnumerator ApplyGravity()
        {
            if (board == null)
                yield break;

            for (int x = 0; x < board.Width; x++)
            {
                int destinationY = 0;

                for (int y = 0; y < board.Height; y++)
                {
                    Vector2Int source = new Vector2Int(x, y);
                    BoardBlock block = board.GetBlock(source);

                    if (block == null)
                        continue;

                    Vector2Int destination = new Vector2Int(x, destinationY++);

                    if (destination != source)
                    {
                        board.SetBlock(source, null);

                        // snapTransform en falso: si no, SetBlock ya coloca el
                        // bloque en el destino y la animacion interpola de un
                        // punto a si mismo, o sea que no se ve caer nada.
                        board.SetBlock(destination, block, false);
                        yield return MoveBlock(block, destination);
                    }
                }
            }

            GravityCompleted?.Invoke();
        }

        /// <summary>Anima una ficha desde donde esta hasta su celda nueva.</summary>
        private IEnumerator MoveBlock(BoardBlock block, Vector2Int destination)
        {
            if (block == null || cellMoveDuration <= 0f)
                yield break;

            Vector3 start = block.transform.position;
            Vector3 end = board.GridToWorld(destination);
            float elapsed = 0f;
            block.SetMoving(true);

            while (elapsed < cellMoveDuration)
            {
                elapsed += Time.deltaTime;
                block.transform.position = Vector3.Lerp(
                    start,
                    end,
                    Mathf.Clamp01(elapsed / cellMoveDuration));
                yield return null;
            }

            block.transform.position = end;
            block.SetMoving(false);
            juice?.Land(block);
        }
    }
}
