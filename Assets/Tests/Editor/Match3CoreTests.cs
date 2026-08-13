using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using TetrisTakana;
using TetrisTakana.Match3;

namespace TetrisTakana.Tests
{
    /// <summary>
    /// Pruebas pequeñas de las reglas que no necesitan abrir una escena
    /// completa. Se ejecutan con Unity Test Framework desde la carpeta Editor.
    /// </summary>
    public sealed class Match3CoreTests
    {
        private GameObject boardObject;
        private Board board;

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("Test Board");
            boardObject.SetActive(false);
            board = boardObject.AddComponent<Board>();
            SetPrivate(board, "width", 5);
            SetPrivate(board, "height", 5);

            boardObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (boardObject != null)
                Object.DestroyImmediate(boardObject);
        }

        [Test]
        public void BoardOnlyAcceptsCrossAdjacentSwaps()
        {
            AddBlock(new Vector2Int(1, 1), 0);
            AddBlock(new Vector2Int(2, 1), 1);

            Assert.IsTrue(board.AreAdjacent(new Vector2Int(1, 1), new Vector2Int(2, 1)));
            Assert.IsTrue(board.TrySwap(new Vector2Int(1, 1), new Vector2Int(2, 1)));
            Assert.IsFalse(board.AreAdjacent(new Vector2Int(1, 1), new Vector2Int(2, 2)));
            Assert.IsFalse(board.TrySwap(new Vector2Int(1, 1), new Vector2Int(2, 2)));
        }

        [Test]
        public void HintFinderFindsHorizontalThreeAfterSwap()
        {
            AddBlock(new Vector2Int(0, 0), 0);
            AddBlock(new Vector2Int(1, 0), 0);
            AddBlock(new Vector2Int(2, 0), 1);
            AddBlock(new Vector2Int(3, 0), 0);

            HintFinder finder = new HintFinder();

            Assert.IsTrue(finder.TryFind(board, out HintFinder.Hint hint));
            Assert.AreEqual(3, hint.Size);
            Assert.IsTrue(
                (hint.First == new Vector2Int(2, 0) && hint.Second == new Vector2Int(3, 0)) ||
                (hint.First == new Vector2Int(3, 0) && hint.Second == new Vector2Int(2, 0)));
        }

        [Test]
        public void MatchSystemFindsVerticalRunsOfThree()
        {
            AddBlock(new Vector2Int(2, 0), 1);
            AddBlock(new Vector2Int(2, 1), 1);
            AddBlock(new Vector2Int(2, 2), 1);

            MatchSystem matchSystem = boardObject.AddComponent<MatchSystem>();
            SetPrivate(matchSystem, "board", board);

            Assert.AreEqual(3, matchSystem.FindMatches().Count);
        }

        [Test]
        public void MatchSystemFindsHorizontalRunsOfThree()
        {
            AddBlock(new Vector2Int(0, 3), 2);
            AddBlock(new Vector2Int(1, 3), 2);
            AddBlock(new Vector2Int(2, 3), 2);

            MatchSystem matchSystem = boardObject.AddComponent<MatchSystem>();
            SetPrivate(matchSystem, "board", board);

            Assert.AreEqual(3, matchSystem.FindMatches().Count);
        }

        [Test]
        public void RisingStackReportsRowsUntilTop()
        {
            RisingStack risingStack = boardObject.AddComponent<RisingStack>();

            Assert.AreEqual(5, risingStack.RowsUntilTop);

            AddBlock(new Vector2Int(1, 2), 0);
            Assert.AreEqual(2, risingStack.RowsUntilTop);

            AddBlock(new Vector2Int(1, 4), 1);
            Assert.AreEqual(0, risingStack.RowsUntilTop);
        }

        [Test]
        public void GravityClosesAnEmptyCellBelowABlock()
        {
            Gravity gravity = boardObject.AddComponent<Gravity>();
            SetPrivate(gravity, "cellMoveDuration", 0f);

            BoardBlock block = AddBlock(new Vector2Int(3, 3), 4);
            IEnumerator routine = gravity.ApplyGravity();

            while (routine.MoveNext())
            {
                // El tiempo de movimiento esta a cero: solo se drena la
                // corrutina para probar la actualización de la matriz.
            }

            Assert.AreSame(block, board.GetBlock(new Vector2Int(3, 0)));
            Assert.IsNull(board.GetBlock(new Vector2Int(3, 3)));
        }

        [Test]
        public void SpawnerFillsRowsFromTheBottom()
        {
            Spawner spawner = boardObject.AddComponent<Spawner>();
            SetPrivate(spawner, "fillDuration", 0f);

            IEnumerator routine = spawner.FillUpTo(3);

            while (routine.MoveNext())
            {
                // Se ejecuta la animación con duración cero para dejar la
                // matriz en el estado final de las filas recién creadas.
            }

            for (int y = 0; y < 3; y++)
            for (int x = 0; x < board.Width; x++)
                Assert.IsNotNull(board.GetBlock(new Vector2Int(x, y)));

        }

        private BoardBlock AddBlock(Vector2Int position, int type)
        {
            GameObject blockObject = new GameObject($"Block {position}");
            blockObject.transform.SetParent(boardObject.transform, false);
            blockObject.transform.position = board.GridToWorld(position);

            BoardBlock block = blockObject.AddComponent<BoardBlock>();
            block.SetBlockType(type);
            board.SetBlock(position, block);
            return block;
        }

        private static void SetPrivate(Object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"No se encontro el campo privado '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
