using System;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>Core movement, capture, bear-off and turn rules of <see cref="GameState"/>.</summary>
    public class GameStateTests
    {
        private GameState _state;
        private MatchContext _context;
        private MoveError _lastRejection;

        [SetUp]
        public void SetUp()
        {
            _state = TestBoard.CreateEmpty(out _context);
            _lastRejection = MoveError.None;
            _state.MoveRejected += result => _lastRejection = result.Error;
        }

        [Test]
        public void StandardSetup_PlacesFifteenCheckersPerPlayer()
        {
            _state.SetupStandardBoard();

            int player0 = 0;
            int player1 = 0;
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                player0 += _state.Nodes[i].CountFor(0);
                player1 += _state.Nodes[i].CountFor(1);
            }

            Assert.AreEqual(GameState.MaxCheckersPerPlayer, player0);
            Assert.AreEqual(GameState.MaxCheckersPerPlayer, player1);
        }

        [Test]
        public void TryMove_WithMatchingDie_MovesCheckerAndSpendsTheDie()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(3, _state.FindNodeIndexOf(checker));
            Assert.IsFalse(_context.CurrentRoll.CanAfford(3));
            Assert.IsTrue(_context.CurrentRoll.CanAfford(5));
        }

        [Test]
        public void TryMove_AgainstDirection_IsRejected()
        {
            TestBoard.PlaceDefault(_state, 5, 0);
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsFalse(_state.TryMove(5, 2));
            Assert.AreEqual(MoveError.WrongDirection, _lastRejection);
        }

        [Test]
        public void CanMove_IsADryQuery()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.CanMove(0, 3));

            Assert.AreEqual(0, _state.FindNodeIndexOf(checker), "A dry query must not move the checker.");
            Assert.AreEqual(2, _context.CurrentRoll.RemainingMoves.Count, "A dry query must not spend dice.");
        }

        [Test]
        public void TryMove_OntoBlot_CapturesItAndAwardsScore()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            DefaultChecker victim = TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(1, _state.GetBarCount(1));
            Assert.AreEqual(GameState.BarIndex, _state.FindNodeIndexOf(victim));
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void TryMove_OntoMadePoint_IsBlocked()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsFalse(_state.TryMove(0, 3));
            Assert.AreEqual(MoveError.DestinationBlocked, _lastRejection);
        }

        [Test]
        public void CheckerOnBar_MustEnterBeforeAnyOtherMove()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            DefaultChecker hit = new DefaultChecker(0);
            TestBoard.Place(_state, 5, hit);
            _state.ForceCapture(hit, 5, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsFalse(_state.TryMove(0, 3));
            Assert.AreEqual(MoveError.MustEnterFromBar, _lastRejection);

            // Player 0 enters from the bar at index (die - 1).
            Assert.IsTrue(_state.TryMove(GameState.BarIndex, 2));
            Assert.AreEqual(0, _state.GetBarCount(0));
        }

        [Test]
        public void CompoundMove_CombinesTwoDice()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsTrue(_state.CanReach(0, 5));
            Assert.IsTrue(_state.TryMove(0, 5));

            Assert.AreEqual(5, _state.FindNodeIndexOf(checker));
            Assert.IsFalse(_context.CurrentRoll.HasMovesLeft);
        }

        [Test]
        public void StackLimit_RejectsTheSixthChecker()
        {
            for (int i = 0; i < GameState.DefaultStackLimit; i++)
                TestBoard.PlaceDefault(_state, 5, 0);

            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 5, 1);

            Assert.IsFalse(_state.TryMove(0, 5));
            Assert.AreEqual(MoveError.StackFull, _lastRejection);
        }

        [Test]
        public void BearOff_FromHomeBoard_UsesSmallestSufficientDie()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 20, 0); // needs 4
            TestBoard.SetDice(_context, 6, 1);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.AreEqual(1, _state.GetBorneOffCount(0));
            Assert.AreEqual(GameState.BearOffScore, _context.GetScore(0));
            Assert.AreEqual(GameState.InvalidIndex, _state.FindNodeIndexOf(checker));
            Assert.IsTrue(_context.CurrentRoll.CanAfford(1), "The unused smaller die must remain.");
        }

        [Test]
        public void BearOff_OutsideHomeBoard_IsRejected()
        {
            TestBoard.PlaceDefault(_state, 10, 0);
            TestBoard.SetDice(_context, 6, 6);

            Assert.IsFalse(_state.TryBearOff(10));
            Assert.AreEqual(MoveError.NotInHomeBoard, _lastRejection);
        }

        [Test]
        public void EndTurn_PassesTheTurnAndEndsTheMatchWhenTurnsRunOut()
        {
            _state = TestBoard.CreateEmpty(out _context, turnLimit: 2);
            _context.AddScore(0, 10);

            int endedWith = int.MinValue;
            _state.MatchEnded += winner => endedWith = winner;

            _state.EndTurn();
            Assert.AreEqual(1, _state.CurrentPlayerId);
            Assert.IsFalse(_state.IsMatchOver);

            _state.EndTurn();
            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(0, _state.WinnerId);
            Assert.AreEqual(0, endedWith);
        }

        [Test]
        public void EqualScores_EndInADraw()
        {
            _state = TestBoard.CreateEmpty(out _context, turnLimit: 1);

            _state.EndTurn();

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(GameState.NoWinner, _state.WinnerId);
        }

        [Test]
        public void Clone_DoesNotAffectTheRealBoardOrDice()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 5);

            GameState clone = _state.Clone();
            Assert.IsTrue(clone.IsSimulation);
            Assert.IsTrue(clone.TryMove(0, 3));

            Assert.AreEqual(0, _state.FindNodeIndexOf(checker));
            Assert.AreEqual(2, _context.CurrentRoll.RemainingMoves.Count);
        }

        [Test]
        public void InvalidPlayerId_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _state.GetBarCount(GameState.PlayerCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => _context.GetScore(-1));
        }
    }
}
