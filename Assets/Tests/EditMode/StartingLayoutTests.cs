using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>Starting formations for 1..15 checkers: <see cref="StartingLayout"/> and <see cref="GameState.SetupPlayer"/>.</summary>
    public class StartingLayoutTests
    {
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            MatchContext context;
            _state = TestBoard.CreateEmpty(out context);
        }

        [Test]
        public void SlotCount_MatchesTheMaximumCheckerCount()
        {
            Assert.AreEqual(GameState.MaxCheckersPerPlayer, StartingLayout.SlotCount);
        }

        [TestCase(5, 0, 2, 1, 2)]
        [TestCase(9, 1, 3, 2, 3)]
        [TestCase(10, 2, 3, 2, 3)]
        [TestCase(13, 2, 4, 3, 4)]
        [TestCase(15, 2, 5, 3, 5)]
        public void SetupPlayer_FollowsTheFillOrder(int count, int onPoint0, int onPoint11, int onPoint16, int onPoint18)
        {
            _state.SetupPlayer(0, CreateDefaults(0, count));

            Assert.AreEqual(onPoint0, _state.Nodes[0].CountFor(0));
            Assert.AreEqual(onPoint11, _state.Nodes[11].CountFor(0));
            Assert.AreEqual(onPoint16, _state.Nodes[16].CountFor(0));
            Assert.AreEqual(onPoint18, _state.Nodes[18].CountFor(0));
        }

        [Test]
        public void SetupPlayer_MirrorsThePointsForPlayerOne()
        {
            _state.SetupPlayer(1, CreateDefaults(1, GameState.MaxCheckersPerPlayer));

            Assert.AreEqual(2, _state.Nodes[23].CountFor(1));
            Assert.AreEqual(5, _state.Nodes[12].CountFor(1));
            Assert.AreEqual(3, _state.Nodes[7].CountFor(1));
            Assert.AreEqual(5, _state.Nodes[5].CountFor(1));
        }

        [Test]
        public void NoCheckerCount_ExceedsTheDefaultStackLimit()
        {
            for (int count = 1; count <= GameState.MaxCheckersPerPlayer; count++)
            {
                int[] perPoint = new int[GameState.NodeCount];
                for (int slot = 0; slot < count; slot++)
                    perPoint[StartingLayout.GetPoint(0, slot)]++;

                for (int point = 0; point < GameState.NodeCount; point++)
                    Assert.LessOrEqual(perPoint[point], GameState.DefaultStackLimit, $"{count} checkers, point {point}");
            }
        }

        [Test]
        public void SetupPlayer_PutsEachCheckerInItsOwnSlot()
        {
            List<BaseChecker> checkers = CreateDefaults(0, 9);
            ArmoredChecker special = new ArmoredChecker(0);
            checkers[8] = special;

            _state.SetupPlayer(0, checkers);

            Assert.AreEqual(StartingLayout.GetPoint(0, 8), _state.FindNodeIndexOf(special));
        }

        [Test]
        public void SetupPlayer_RejectsCountsOutsideTheRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _state.SetupPlayer(0, new BaseChecker[0]));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _state.SetupPlayer(0, CreateDefaults(0, GameState.MaxCheckersPerPlayer + 1)));
        }

        [Test]
        public void SetupPlayer_RejectsTheOtherPlayersChecker_AndLeavesTheBoardUntouched()
        {
            List<BaseChecker> checkers = CreateDefaults(0, 5);
            checkers[4] = new DefaultChecker(1);

            Assert.Throws<ArgumentException>(() => _state.SetupPlayer(0, checkers));
            Assert.IsFalse(_state.HasCheckersInPlay(0));
            Assert.IsFalse(_state.HasCheckersInPlay(1));
        }

        [Test]
        public void SetupPlayer_RejectsASecondSetup()
        {
            _state.SetupPlayer(0, CreateDefaults(0, 5));

            Assert.Throws<InvalidOperationException>(() => _state.SetupPlayer(0, CreateDefaults(0, 5)));
        }

        private static List<BaseChecker> CreateDefaults(int ownerId, int count)
        {
            List<BaseChecker> checkers = new List<BaseChecker>(count);
            for (int i = 0; i < count; i++)
                checkers.Add(new DefaultChecker(ownerId));
            return checkers;
        }
    }
}
