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

            List<Point> moves = game.GetLegalMoves(true);
            List<Point> threeMoves = new List<Point>()
            {
                new Point(0, 0),
                new Point(0, 1),
                new Point(0, 2)
            };
            Assert.AreEqual(threeMoves, moves);

            game = game.MakeMove(threeMoves[1]);
            moves = game.GetLegalMoves(true);
            List<Point> passMoves = new List<Point>()
            {
                new Point(-1, -1)
            };
            Assert.AreEqual(passMoves, moves,
                "After play center. Board:\n" + game.Board.ToString());
        }
    }
}
