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
        public void GetMovableChecker_ReturnsTheTopmostOwnCheckerOfAPlayablePoint()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            DefaultChecker top = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.PlaceDefault(_state, 10, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.AreSame(top, _state.GetMovableChecker(0));
            Assert.IsNull(_state.GetMovableChecker(10), "The opponent's checkers are not the current player's to play.");
            Assert.IsNull(_state.GetMovableChecker(4), "An empty point has nothing to play.");
        }

        [Test]
        public void GetMovableChecker_UnderAnEnemyOnTheSamePoint_ReturnsTheOwnChecker()
        {
            DefaultChecker own = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.PlaceDefault(_state, 0, 1); // An enemy that infiltrated the point sits on top.
            TestBoard.SetDice(_context, 3, 5);

            Assert.AreSame(own, _state.GetMovableChecker(0));
        }

        [Test]
        public void GetMovableChecker_IsNullWhenEveryDestinationIsBlocked()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            foreach (int blocked in new[] { 3, 5, 8 })
            {
                TestBoard.PlaceDefault(_state, blocked, 1);
                TestBoard.PlaceDefault(_state, blocked, 1);
            }
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsNull(_state.GetMovableChecker(0));
            Assert.IsFalse(_state.HasAnyLegalMove());
        }

        [Test]
        public void GetMovableChecker_WithACheckerOnTheBar_OnlyTheBarIsPlayable()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            DefaultChecker hit = new DefaultChecker(0);
            TestBoard.Place(_state, 5, hit);
            _state.ForceCapture(hit, 5, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.AreSame(hit, _state.GetMovableChecker(GameState.BarIndex));
            Assert.IsNull(_state.GetMovableChecker(0), "Nothing else may be played while a checker waits on the bar.");
        }

        [Test]
        public void TryMove_WithAChosenChecker_MovesItFromUnderTheStack()
        {
            DefaultChecker bottom = TestBoard.PlaceDefault(_state, 0, 0);
            DefaultChecker top = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(bottom, 3));

            Assert.AreEqual(3, _state.FindNodeIndexOf(bottom));
            Assert.AreEqual(0, _state.FindNodeIndexOf(top));
        }

        [Test]
        public void TryMove_WithTheOpponentsChecker_IsRejected()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            DefaultChecker enemy = TestBoard.PlaceDefault(_state, 0, 1); // Infiltrated onto player 0's point.
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsFalse(_state.TryMove(enemy, 3));
            Assert.AreEqual(MoveError.SourceEmpty, _lastRejection);
            Assert.AreEqual(0, _state.FindNodeIndexOf(enemy));
        }

        [Test]
        public void GetMovableChecker_ABuriedCheckerThatCanMove_IsPlayable()
        {
            NomadChecker nomad = TestBoard.Place(_state, 2, new NomadChecker(0));
            DefaultChecker top = TestBoard.PlaceDefault(_state, 2, 0);
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.SetDice(_context, 2, 2);

            // Every 2 runs into the blocked point 4; only the Nomad's 2 + 3 jumps it, to point 7.
            Assert.IsFalse(_state.CanPlayChecker(top));
            Assert.IsTrue(_state.CanPlayChecker(nomad));
            Assert.AreSame(nomad, _state.GetMovableChecker(2));
            Assert.IsTrue(_state.HasAnyLegalMove(), "The turn must not pass while the buried Nomad can move.");
        }

        [Test]
        public void GetMovableChecker_IsNullOnceTheDiceAreSpent()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));
            Assert.IsTrue(_state.TryMove(3, 8));

            Assert.IsNull(_state.GetMovableChecker(8));
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
            for (int i = 0; i < GameState.StackLimit; i++)
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
        public void Clone_WithASubstitute_ReplacesCheckersOnlyInTheCopy()
        {
            DefaultChecker mine = TestBoard.PlaceDefault(_state, 0, 0);
            DefaultChecker theirs = TestBoard.PlaceDefault(_state, 5, 1);
            DefaultChecker standIn = new DefaultChecker(1);

            GameState clone = _state.Clone(checker => checker.OwnerId == 1 ? standIn : checker);

            Assert.AreSame(mine, clone.Nodes[0].Checkers[0]);
            Assert.AreSame(standIn, clone.Nodes[5].Checkers[0]);
            Assert.AreSame(theirs, _state.Nodes[5].Checkers[0], "The real board keeps its own checker.");
        }

        [Test]
        public void Clone_WithASubstituteOfAnotherPlayer_Throws()
        {
            TestBoard.PlaceDefault(_state, 0, 0);

            Assert.Throws<InvalidOperationException>(() => _state.Clone(checker => new DefaultChecker(1)));
        }

        [Test]
        public void InvalidPlayerId_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _state.GetBarCount(GameState.PlayerCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => _context.GetScore(-1));
        }
    }
}
