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

            Stopwatch timePerCapturedGroups = new Stopwatch();
            timePerCapturedGroups.Start();
            List<Group> capturedGroups = Board.GroupListPool.Rent();
            capturedGroups.Clear();
            board.GetCapturedGroups(0, 1, capturedGroups);
            int numCaptures = capturedGroups.Count;
            Board.GroupListPool.Return(capturedGroups);
            timePerCapturedGroups.Stop();
            long millisecondsPerCapturedGroups = timePerCapturedGroups.ElapsedMilliseconds;

            Assert.AreEqual(0, numCaptures);

            Debug.Log("GetCapturedGroupsOn1x3: " + millisecondsPerCapturedGroups + "ms");
        }

        [Test]
        public void GetHypotheticalCapturedGroupsOn1x3()
        {
            ObjectPool<Board>.TryInit(1);
            Board.InitPools();
            Board board = new Board(1, 3);
            Board hypotheticalBoard = ObjectPool<Board>.Shared.Rent();
            List<Group> capturedGroups = Board.GroupListPool.Rent();
            board.GetHypotheticalCapturedGroups(
                hypotheticalBoard, capturedGroups, 0, 1, Content.Black);
            int numCaptures = capturedGroups.Count;
            Board.GroupListPool.Return(capturedGroups);
            ObjectPool<Board>.Shared.Return(hypotheticalBoard);

            Assert.AreEqual(0, numCaptures);
        }
    }
}
