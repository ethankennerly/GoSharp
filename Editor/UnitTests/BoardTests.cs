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
        public void GetHypotheticalCapturedGroupsOn1x3()
        {
            ObjectPool<Board>.TryInit(1);
            Board.InitPools();
            Board board = new Board(1, 3);
            int x = 0;
            for (int y = 0; y < 3; ++y)
            {
                Board hypotheticalBoard = ObjectPool<Board>.Shared.Rent();
                List<Group> capturedGroups = Board.GroupListPool.Rent();
                board.GetHypotheticalCapturedGroups(
                    hypotheticalBoard, capturedGroups, x, y, Content.Black);
                int numCaptures = capturedGroups.Count;
                bool hasLiberties = hypotheticalBoard.HasLiberties(x, y);
                Board.GroupListPool.Return(capturedGroups);
                ObjectPool<Board>.Shared.Return(hypotheticalBoard);

                Assert.AreEqual(0, numCaptures,
                    "y: " + y);
                Assert.IsTrue(hasLiberties,
                    "Has liberties at y: " + y);
            }
        }
    }
}
