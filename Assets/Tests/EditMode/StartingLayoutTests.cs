using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>
    /// Starting formations: the design table (<see cref="StartingPatterns"/>), dealing them onto the board
    /// (<see cref="StartingLayout"/>) and placing checkers on a layout (<see cref="GameState.SetupPlayer"/>).
    /// </summary>
    public class StartingLayoutTests
    {
        // Enough seeds that every formation and most points come up; the run stays deterministic.
        private const int Seeds = 200;

        private const int Min = GameState.MinCheckersPerPlayer;
        private const int Max = GameState.MaxCheckersPerPlayer;

        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            MatchContext context;
            _state = TestBoard.CreateEmpty(out context);
        }

        #region Patterns

        [Test]
        public void EveryCount_HasFormationsThatAddUpAndFitOnAPoint()
        {
            for (int count = Min; count <= Max; count++)
            {
                IReadOnlyList<IReadOnlyList<int>> formations = StartingPatterns.Get(count);
                Assert.Greater(formations.Count, 0, $"{count} checkers");

                foreach (IReadOnlyList<int> formation in formations)
                {
                    int sum = 0;
                    foreach (int stack in formation)
                    {
                        Assert.That(stack, Is.InRange(1, GameState.StackLimit), $"{count} checkers");
                        sum += stack;
                    }

                    Assert.AreEqual(count, sum, $"A formation for {count} checkers");
                    Assert.LessOrEqual(formation.Count, StartingPatterns.MaxStacks);
                }
            }
        }

        [Test]
        public void Patterns_FollowTheDesignTable()
        {
            AssertFormations(5, new[] { 2, 3 });
            AssertFormations(9, new[] { 4, 3, 2 }, new[] { 3, 3, 3 }, new[] { 2, 2, 3, 2 });
            AssertFormations(13, new[] { 4, 3, 3, 3 }, new[] { 4, 4, 3, 2 });
            AssertFormations(15, new[] { 4, 4, 4, 3 });
        }

        [Test]
        public void CountsOutsideTheTable_AreRejected()
        {
            IRandomProvider random = new FixedRandom();

            Assert.Throws<ArgumentOutOfRangeException>(() => StartingPatterns.Get(Min - 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => StartingPatterns.Get(Max + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => StartingLayout.Deal(Min - 1, Min, random));
            Assert.Throws<ArgumentOutOfRangeException>(() => StartingLayout.Deal(Min, Max + 1, random));
        }

        #endregion

        #region Dealing

        [TestCase(5, 15)]
        [TestCase(15, 5)]
        [TestCase(9, 12)]
        [TestCase(15, 15)]
        public void NoCheckerStarts_InItsOwnHomeBoard_OrOnTheOtherSidesPoint(int player0Checkers, int player1Checkers)
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                StartingLayout layout = StartingLayout.Deal(player0Checkers, player1Checkers, new SystemRandomProvider(seed));
                int[] owner = NewOwnerMap();

                for (int p = 0; p < GameState.PlayerCount; p++)
                {
                    for (int slot = 0; slot < layout.GetCheckerCount(p); slot++)
                    {
                        int point = layout.GetPoint(p, slot);
                        Assert.IsFalse(GameState.IsInHomeBoard(point, p), $"Seed {seed}: player {p} starts at home on {point}.");
                        Assert.That(owner[point], Is.EqualTo(Node.EmptyOwner).Or.EqualTo(p), $"Seed {seed}: point {point} is shared.");
                        owner[point] = p;
                    }
                }
            }
        }

        [Test]
        public void EachSide_IsDealtOneOfItsFormations()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                StartingLayout layout = StartingLayout.Deal(9, 12, new SystemRandomProvider(seed));

                CollectionAssert.Contains(Signatures(9), Signature(StacksOf(layout, 0)), $"Seed {seed}");
                CollectionAssert.Contains(Signatures(12), Signature(StacksOf(layout, 1)), $"Seed {seed}");
            }
        }

        [Test]
        public void EveryFormationOfACount_GetsDealt()
        {
            HashSet<string> dealt = new HashSet<string>();
            for (int seed = 1; seed <= Seeds; seed++)
                dealt.Add(Signature(StacksOf(StartingLayout.Deal(9, Min, new SystemRandomProvider(seed)), 0)));

            CollectionAssert.AreEquivalent(Signatures(9), dealt);
        }

        [Test]
        public void TheSameSeed_DealsTheSameLayout()
        {
            StartingLayout first = StartingLayout.Deal(11, 14, new SystemRandomProvider(42));
            StartingLayout second = StartingLayout.Deal(11, 14, new SystemRandomProvider(42));

            for (int p = 0; p < GameState.PlayerCount; p++)
            {
                for (int slot = 0; slot < first.GetCheckerCount(p); slot++)
                    Assert.AreEqual(first.GetPoint(p, slot), second.GetPoint(p, slot));
            }
        }

        [Test]
        public void BlockedPoints_StayEmpty()
        {
            int[] blocked = { 0, 1, 2, 6, 7, 8, 12, 13 };

            for (int seed = 1; seed <= Seeds; seed++)
            {
                StartingLayout layout = StartingLayout.Deal(Max, Max, new SystemRandomProvider(seed), blocked);

                for (int p = 0; p < GameState.PlayerCount; p++)
                {
                    for (int slot = 0; slot < layout.GetCheckerCount(p); slot++)
                        CollectionAssert.DoesNotContain(blocked, layout.GetPoint(p, slot), $"Seed {seed}");
                }
            }
        }

        [Test]
        public void HasRoomAround_NeedsTwiceTheMostStacksFreePerSide()
        {
            Assert.IsTrue(StartingLayout.HasRoomAround(new int[0]));

            // Player 0 may use 0..17; blocking 0..9 leaves it exactly 8 free points, blocking 0..10 leaves 7.
            Assert.IsTrue(StartingLayout.HasRoomAround(Range(0, 10)));
            Assert.IsFalse(StartingLayout.HasRoomAround(Range(0, 11)));
        }

        [Test]
        public void Deal_RejectsMissingRandomness_AndBlockedPointsOffTheBoard()
        {
            Assert.Throws<ArgumentNullException>(() => StartingLayout.Deal(Min, Min, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => StartingLayout.Deal(Min, Min, new FixedRandom(), new[] { GameState.NodeCount }));
        }

        #endregion

        #region SetupPlayer

        [Test]
        public void SetupPlayer_PlacesEveryChecker()
        {
            TestBoard.SetupFullBoard(_state);

            for (int p = 0; p < GameState.PlayerCount; p++)
            {
                int count = 0;
                for (int i = 0; i < GameState.NodeCount; i++)
                    count += _state.Nodes[i].CountFor(p);

                Assert.AreEqual(Max, count);
            }
        }

        [Test]
        public void SetupPlayer_PutsEachCheckerInItsOwnSlot()
        {
            StartingLayout layout = StartingLayout.Deal(9, 9, new FixedRandom());
            List<BaseChecker> checkers = TestBoard.CreateDefaults(0, 9);
            ArmoredChecker special = new ArmoredChecker(0);
            checkers[8] = special;

            _state.SetupPlayer(0, checkers, layout);

            Assert.AreEqual(layout.GetPoint(0, 8), _state.FindNodeIndexOf(special));
        }

        [Test]
        public void SetupPlayer_RejectsACountTheLayoutDoesNotDeal()
        {
            StartingLayout layout = StartingLayout.Deal(Min, Min, new FixedRandom());

            Assert.Throws<ArgumentException>(() => _state.SetupPlayer(0, TestBoard.CreateDefaults(0, Min + 1), layout));
            Assert.Throws<ArgumentNullException>(() => _state.SetupPlayer(0, TestBoard.CreateDefaults(0, Min), null));
        }

        [Test]
        public void SetupPlayer_RejectsTheOtherPlayersChecker_AndLeavesTheBoardUntouched()
        {
            StartingLayout layout = StartingLayout.Deal(Min, Min, new FixedRandom());
            List<BaseChecker> checkers = TestBoard.CreateDefaults(0, Min);
            checkers[Min - 1] = new DefaultChecker(1);

            Assert.Throws<ArgumentException>(() => _state.SetupPlayer(0, checkers, layout));
            Assert.IsFalse(_state.HasCheckersInPlay(0));
            Assert.IsFalse(_state.HasCheckersInPlay(1));
        }

        [Test]
        public void SetupPlayer_RejectsASecondSetup()
        {
            StartingLayout layout = StartingLayout.Deal(Min, Min, new FixedRandom());
            _state.SetupPlayer(0, TestBoard.CreateDefaults(0, Min), layout);

            Assert.Throws<InvalidOperationException>(() => _state.SetupPlayer(0, TestBoard.CreateDefaults(0, Min), layout));
        }

        [Test]
        public void SetupPlayer_RejectsAPointTheOtherPlayerHolds_AndLeavesTheBoardUntouched()
        {
            StartingLayout layout = StartingLayout.Deal(Min, Min, new FixedRandom());
            TestBoard.PlaceDefault(_state, layout.GetPoint(0, 0), 1);

            Assert.Throws<InvalidOperationException>(() => _state.SetupPlayer(0, TestBoard.CreateDefaults(0, Min), layout));
            Assert.IsFalse(_state.HasCheckersInPlay(0));
        }

        #endregion

        #region Helpers

        private static void AssertFormations(int count, params int[][] expected)
        {
            IReadOnlyList<IReadOnlyList<int>> formations = StartingPatterns.Get(count);

            Assert.AreEqual(expected.Length, formations.Count, $"{count} checkers");
            for (int i = 0; i < expected.Length; i++)
                CollectionAssert.AreEqual(expected[i], formations[i], $"{count} checkers, formation {i}");
        }

        /// <summary>The stack heights a player was dealt, whatever points they landed on.</summary>
        private static List<int> StacksOf(StartingLayout layout, int playerId)
        {
            int[] perPoint = new int[GameState.NodeCount];
            for (int slot = 0; slot < layout.GetCheckerCount(playerId); slot++)
                perPoint[layout.GetPoint(playerId, slot)]++;

            List<int> stacks = new List<int>();
            foreach (int height in perPoint)
            {
                if (height > 0)
                    stacks.Add(height);
            }

            return stacks;
        }

        /// <summary>Stack heights in a canonical order, so formations can be compared regardless of point order.</summary>
        private static string Signature(IEnumerable<int> stacks)
        {
            List<int> sorted = new List<int>(stacks);
            sorted.Sort();
            return string.Join("-", sorted);
        }

        private static List<string> Signatures(int count)
        {
            List<string> signatures = new List<string>();
            foreach (IReadOnlyList<int> formation in StartingPatterns.Get(count))
                signatures.Add(Signature(formation));

            return signatures;
        }

        private static int[] NewOwnerMap()
        {
            int[] owner = new int[GameState.NodeCount];
            for (int i = 0; i < owner.Length; i++)
                owner[i] = Node.EmptyOwner;

            return owner;
        }

        private static int[] Range(int first, int count)
        {
            int[] points = new int[count];
            for (int i = 0; i < count; i++)
                points[i] = first + i;

            return points;
        }

        #endregion
    }
}
