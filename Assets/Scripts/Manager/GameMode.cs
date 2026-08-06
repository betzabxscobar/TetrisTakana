namespace TetrisTakana
{
    /// <summary>
    /// Los dos modos de juego. Comparten marcador y tabla de puntuaciones,
    /// pero no puntuan igual: en el Tetris se hacen lineas y en el match-3 se
    /// encadenan combos, asi que sus rankings van por separado y cada
    /// resultado tiene que decir de donde sale.
    /// </summary>
    public enum GameMode
    {
        Tetris = 0,
        Match3 = 1
    }

    /// <summary>Paso de modo a texto y al reves.</summary>
    public static class GameModeExtensions
    {
        private const string TetrisKey = "tetris";
        private const string Match3Key = "match3";

        /// <summary>
        /// El nombre con el que viaja el modo fuera del juego. Se escribe a
        /// mano y no con ToString() porque este texto es el del enum de la base
        /// de datos: renombrar el enum de C# no puede cambiar lo que ya hay
        /// guardado en las filas del ranking.
        /// </summary>
        public static string ToKey(this GameMode mode)
        {
            return mode == GameMode.Match3 ? Match3Key : TetrisKey;
        }

        /// <summary>Devuelve el modo que corresponde a ese texto.</summary>
        public static GameMode FromKey(string key)
        {
            return key == Match3Key ? GameMode.Match3 : GameMode.Tetris;
        }
    }
}
