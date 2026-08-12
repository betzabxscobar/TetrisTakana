using UnityEngine;

namespace TetrisTakana.Match3
{
    /// <summary>
    /// Busca jugadas posibles: intercambios entre vecinos que formarian tres en
    /// raya. Trabaja siempre sobre una copia de los tipos, nunca sobre el
    /// tablero real, asi que se puede preguntar en mitad de la partida sin
    /// mover ni una ficha.
    ///
    /// No es un MonoBehaviour a proposito: la usan el bucle del modo (para
    /// saber si queda alguna jugada) y la mascota (para saber cual), y como
    /// clase suelta cada uno se hace la suya sin tener que colgarla de la
    /// escena ni mantener dos referencias que puedan quedarse sin asignar.
    /// </summary>
    public sealed class HintFinder
    {
        /// <summary>Una jugada: las dos celdas que hay que intercambiar.</summary>
        public readonly struct Hint
        {
            public readonly Vector2Int First;
            public readonly Vector2Int Second;

            /// <summary>Fichas que se romperian al hacerla.</summary>
            public readonly int Size;

            public Hint(Vector2Int first, Vector2Int second, int size)
            {
                First = first;
                Second = second;
                Size = size;
            }

            /// <summary>El punto medio de las dos celdas, para apuntar hacia el.</summary>
            public Vector2 Center => new Vector2(
                (First.x + Second.x) * 0.5f,
                (First.y + Second.y) * 0.5f);
        }

        private int[,] types;
        private int width;
        private int height;

        /// <summary>
        /// Dice si queda alguna jugada. Un tablero con huecos cuenta como que
        /// si: las celdas vacias son fichas aun cayendo, y dar la partida por
        /// perdida a media cascada es perderla por un fotograma de nada.
        /// </summary>
        public bool HasAnyMove(Board board)
        {
            if (!Snapshot(board))
                return true;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (SwapSize(x, y, x + 1, y) > 0 || SwapSize(x, y, x, y + 1) > 0)
                    return true;

            return false;
        }

        /// <summary>
        /// Devuelve la mejor jugada que haya sobre el tablero, la que mas
        /// fichas rompe. Se elige la mayor y no la primera porque la primera
        /// sale siempre de la esquina de abajo a la izquierda, y la mascota
        /// acabaria señalando siempre la misma zona.
        /// </summary>
        public bool TryFind(Board board, out Hint hint)
        {
            hint = default;

            if (!Snapshot(board))
                return false;

            int best = 0;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int size = SwapSize(x, y, x + 1, y);

                if (size > best)
                {
                    best = size;
                    hint = new Hint(
                        new Vector2Int(x, y),
                        new Vector2Int(x + 1, y),
                        size);
                }

                size = SwapSize(x, y, x, y + 1);

                if (size > best)
                {
                    best = size;
                    hint = new Hint(
                        new Vector2Int(x, y),
                        new Vector2Int(x, y + 1),
                        size);
                }
            }

            return best > 0;
        }

        /// <summary>
        /// Copia los tipos del tablero. Devuelve falso si el tablero no esta
        /// entero, que es la señal de que aun se esta resolviendo.
        /// </summary>
        private bool Snapshot(Board board)
        {
            if (board == null)
                return false;

            width = board.Width;
            height = board.Height;

            if (types == null ||
                types.GetLength(0) != width ||
                types.GetLength(1) != height)
                types = new int[width, height];

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                BoardBlock block = board.GetBlock(new Vector2Int(x, y));

                if (block == null)
                    return false;

                types[x, y] = block.BlockType;
            }

            return true;
        }

        /// <summary>
        /// Prueba un intercambio sobre la copia y devuelve cuantas fichas
        /// romperia, o cero si no forma nada. La celda de destino puede caer
        /// fuera: asi quien llama recorre el tablero entero sin comprobar
        /// bordes en cada paso.
        /// </summary>
        private int SwapSize(int firstX, int firstY, int secondX, int secondY)
        {
            if (secondX >= width || secondY >= height)
                return 0;

            // Dos fichas iguales no cambian nada al cruzarse.
            if (types[firstX, firstY] == types[secondX, secondY])
                return 0;

            (types[firstX, firstY], types[secondX, secondY]) =
                (types[secondX, secondY], types[firstX, firstY]);

            int size = Mathf.Max(LineSize(firstX, firstY), LineSize(secondX, secondY));

            (types[firstX, firstY], types[secondX, secondY]) =
                (types[secondX, secondY], types[firstX, firstY]);

            return size;
        }

        /// <summary>
        /// La linea mas larga que pasa por esa celda, o cero si no llega a
        /// tres. Se mira en cruz y se devuelve la mayor de las dos.
        /// </summary>
        private int LineSize(int x, int y)
        {
            int type = types[x, y];

            int horizontal = 1 + CountSame(x, y, -1, 0, type) + CountSame(x, y, 1, 0, type);
            int vertical = 1 + CountSame(x, y, 0, -1, type) + CountSame(x, y, 0, 1, type);
            int longest = Mathf.Max(horizontal, vertical);

            return longest >= 3 ? longest : 0;
        }

        /// <summary>Cuenta cuantas fichas iguales seguidas hay en una direccion.</summary>
        private int CountSame(int x, int y, int stepX, int stepY, int type)
        {
            int count = 0;
            int currentX = x + stepX;
            int currentY = y + stepY;

            while (currentX >= 0 && currentX < width &&
                   currentY >= 0 && currentY < height &&
                   types[currentX, currentY] == type)
            {
                count++;
                currentX += stepX;
                currentY += stepY;
            }

            return count;
        }
    }
}
