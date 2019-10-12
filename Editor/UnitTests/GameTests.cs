using Go;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;

using Debug = UnityEngine.Debug;

namespace Go.UnitTests
{
    public sealed class GameTests
    {
        [Test]
        public void GetLegalMovesOn1x3()
        {
            Game.InitPools();
            Game game = new Game(new Board(1, 3), Content.Black);

            Stopwatch timePerLegalMoves = new Stopwatch();
            timePerLegalMoves.Start();
            List<Point> moves = game.GetLegalMoves(true);
            timePerLegalMoves.Stop();
            long millisecondsPerLegalMoves = timePerLegalMoves.ElapsedMilliseconds;

            List<Point> threeMoves = new List<Point>()
            {
                new Point(0, 0),
                new Point(0, 1),
                new Point(0, 2)
            };
            Assert.AreEqual(threeMoves, moves);

            Debug.Log("GetLegalMovesOn1x3: " + millisecondsPerLegalMoves + "ms");
        }
    }
}
