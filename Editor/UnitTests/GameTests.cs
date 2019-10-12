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
                Game.PassMove
            };
            Assert.AreEqual(passMoves, moves,
                "After play center. Board:\n" + game.Board.ToString());
        }

        [Test]
        public void GetLegalMovesOn1x2()
        {
            Game.InitPools();
            Game game = new Game(new Board(1, 2), Content.Black);

            List<Point> moves = game.GetLegalMoves(true);
            List<Point> twoMoves = new List<Point>()
            {
                new Point(0, 0),
                new Point(0, 1)
            };
            Assert.AreEqual(twoMoves, moves);

            game = game.MakeMove(twoMoves[0]);
            moves = game.GetLegalMoves(true);
            List<Point> captureMoves = new List<Point>()
            {
                new Point(0, 1)
            };
            Assert.AreEqual(captureMoves, moves,
                "After play (0, 0) can capture. Board:\n" + game.Board.ToString());

            game = game.MakeMove(captureMoves[0]);
            moves = game.GetLegalMoves(true);
            List<Point> passMoves = new List<Point>()
            {
                Game.PassMove
            };
            Assert.AreEqual(passMoves, moves,
                "After capture. Ko. Board:\n" + game.Board.ToString());
        }
    }
}
