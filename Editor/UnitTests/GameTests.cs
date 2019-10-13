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
        public void GetLegalMovesOn1x3Center()
        {
            Game.InitPools();
            Game game = Game.GamePool.Rent();
            game.Clone(new Board(1, 3), Content.Black);

            List<Point> moves = game.GetLegalMoves(true);
            List<Point> threeMoves = new List<Point>()
            {
                new Point(0, 0),
                new Point(0, 1),
                new Point(0, 2)
            };
            Assert.AreEqual(threeMoves, moves,
                "First move has 3 legal moves on 1x3. Board:\n" + game.Board.ToString());

            Game nextGame = Game.GamePool.Rent();
            nextGame = game.MakeMove(threeMoves[1], nextGame);
            Game.GamePool.Return(game);
            moves = nextGame.GetLegalMoves(true);
            List<Point> passMoves = new List<Point>()
            {
                Game.PassMove
            };

            Game.GamePool.Return(nextGame);
            Assert.AreEqual(passMoves, moves,
                "After play center. Board:\n" + nextGame.Board.ToString());
        }

        [Test]
        public void GetLegalMovesOn1x2()
        {
            Game.InitPools();
            Game game = Game.GamePool.Rent();
            game.Clone(new Board(1, 2), Content.Black);

            List<Point> moves = game.GetLegalMoves(true);
            List<Point> twoMoves = new List<Point>()
            {
                new Point(0, 0),
                new Point(0, 1)
            };
            Assert.AreEqual(twoMoves, moves,
                "First move has 2 legal moves on 1x2. Board:\n" + game.Board.ToString());

            Game nextGame = Game.GamePool.Rent();
            nextGame = game.MakeMove(twoMoves[0], nextGame);
            Game.GamePool.Return(game);
            moves = nextGame.GetLegalMoves(true);
            List<Point> captureMoves = new List<Point>()
            {
                new Point(0, 1)
            };
            Assert.AreEqual(captureMoves, moves,
                "After play (0, 0) can capture. Board:\n" + nextGame.Board.ToString());

            #if STRICT_KO
            game = nextGame.MakeMove(captureMoves[0], game);
            moves = game.GetLegalMoves(true);
            List<Point> passMoves = new List<Point>()
            {
                Game.PassMove
            };
            Game.GamePool.Return(nextGame);
            Assert.AreEqual(passMoves, moves,
                "After capture. Ko. Board:\n" + game.Board.ToString());
            #endif
        }
    }
}
