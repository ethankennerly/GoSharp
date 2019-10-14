using FineGameDesign.Pooling;
using Go;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;

using Debug = UnityEngine.Debug;

namespace Go.UnitTests
{
    public sealed class BoardTests
    {
        [Test]
        public void GetCapturedGroupsOn1x3()
        {
            Board.InitPools();
            Board board = new Board(1, 3);

            List<Group> capturedGroups = Board.GroupListPool.Rent();
            capturedGroups.Clear();
            board.GetCapturedGroups(0, 1, capturedGroups);
            int numCaptures = capturedGroups.Count;
            Board.GroupListPool.Return(capturedGroups);

            Assert.AreEqual(0, numCaptures);
        }

        [Test]
        public void WouldCaptureOn1x3FirstMoveFalse()
        {
            ObjectPool<Board>.TryInit(1);
            Board.InitPools();
            Board board = new Board(1, 3);
            int x = 0;
            for (int y = 0; y < 3; ++y)
            {
                board[x, y] = Content.Black;
                bool wouldCapture = board.WouldCapture(x, y, Content.White);
                bool wouldHaveLiberties = board.HasLiberties(x, y);
                board[x, y] = Content.Empty;

                Assert.IsFalse(wouldCapture,
                    "White would capture at y: " + y);
                Assert.IsTrue(wouldHaveLiberties,
                    "Would have liberties at y: " + y);
            }
        }

        [Test]
        public void WouldCaptureOn1x3AfterCenterIsSuicide()
        {
            ObjectPool<Board>.TryInit(1);
            Board.InitPools();
            Board board = new Board(1, 3);
            board[0, 1] = Content.Black;
            board[0, 0] = Content.White;

            bool wouldHaveLiberties = board.HasLiberties(0, 0);
            Assert.IsFalse(wouldHaveLiberties,
                "White would have liberties at (0, 0).");

            bool wouldCapture = board.WouldCapture(0, 0, Content.Black);
            Assert.IsFalse(wouldCapture,
                "White would capture black at (0, 0).");
        }
    }
}
