using System;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>
    /// How matches end: target score, counted turns, running out of checkers and the final stretch
    /// (<see cref="MatchRules"/> as applied by <see cref="GameState"/>).
    /// </summary>
    public class MatchRulesTests
    {
        private const int Target = 50;
        private const int TurnLimit = 10;
        private const int FinalStretch = 3;

        // Player 1's bear-off distance from point 2 is 3 (its home board is 0..5).
        private const int OpponentHomePoint = 2;

        private GameState _state;
        private MatchContext _context;
        private int _matchEndedCount;

        #region MatchRules itself

        [Test]
        public void ForTarget_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.ForTarget(0, TurnLimit, FinalStretch));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.ForTarget(Target, 0, FinalStretch));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.ForTarget(Target, TurnLimit, -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MatchRules.ForTarget(Target, TurnLimit, FinalStretch, GameState.PlayerCount));
        }

        [Test]
        public void Classic_CountsEveryTurnAndHasNoTarget()
        {
            MatchRules rules = MatchRules.Classic(5);

            Assert.IsFalse(rules.HasTargetScore);
            Assert.IsTrue(rules.CountsTurnOf(0));
            Assert.IsTrue(rules.CountsTurnOf(1));
            Assert.IsFalse(rules.IsTargetReached(0, int.MaxValue));
        }

        #endregion

        #region Target score

        [Test]
        public void ReachingTheTarget_EndsTheMatchImmediately()
        {
            CreateTargetMatch(target: GameState.BearOffScore);
            TestBoard.PlaceDefault(_state, 20, 0);
            TestBoard.PlaceDefault(_state, 10, 0);
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 6, 1);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(0, _state.WinnerId);
            Assert.AreEqual(1, _matchEndedCount);
        }

        [Test]
        public void ScoreFromAnAbility_ReachesTheTargetToo()
        {
            CreateTargetMatch();

            // Abilities call AddScore directly instead of going through the rule engine.
            _context.AddScore(0, Target);

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(0, _state.WinnerId);
        }

        [Test]
        public void OpponentScore_NeverEndsTheMatch()
        {
            CreateTargetMatch();

            _context.AddScore(1, Target * 10);

            Assert.IsFalse(_state.IsMatchOver);
        }

        [Test]
        public void RunningOutOfTurns_BelowTheTarget_Loses()
        {
            CreateTargetMatch(turnLimit: 1);
            _context.AddScore(0, Target - 1);

            _state.EndTurn();

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(1, _state.WinnerId);
        }

        [Test]
        public void OnlyTheScoringPlayersTurnsAreCounted()
        {
            CreateTargetMatch(turnLimit: 2);

            _state.EndTurn();
            Assert.AreEqual(1, _state.RemainingTurns);
            Assert.AreEqual(1, _state.CurrentPlayerId);

            _state.EndTurn();
            Assert.AreEqual(1, _state.RemainingTurns, "The opponent's turn must not be counted.");
            Assert.AreEqual(0, _state.CurrentPlayerId);

            _state.EndTurn();
            Assert.IsTrue(_state.IsMatchOver);
        }

        [Test]
        public void ScoringPlayerRunningOutOfCheckers_BelowTheTarget_Loses()
        {
            CreateTargetMatch(target: 100);
            TestBoard.PlaceDefault(_state, 20, 0);
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 6, 1);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(1, _state.WinnerId);
        }

        [Test]
        public void Simulation_CannotEndTheMatchByReachingTheTarget()
        {
            CreateTargetMatch(target: GameState.BearOffScore);
            TestBoard.PlaceDefault(_state, 20, 0);
            TestBoard.PlaceDefault(_state, 10, 0);
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 6, 1);

            GameState clone = _state.Clone();
            Assert.IsTrue(clone.TryBearOff(20));

            Assert.IsFalse(clone.IsMatchOver);
            Assert.IsFalse(_state.IsMatchOver);
        }

        #endregion

        #region Final stretch

        [Test]
        public void OpponentRunningOutOfCheckers_StartsTheFinalStretch()
        {
            CreateTargetMatch();
            int stretchStartedWith = -1;
            _state.FinalStretchStarted += remaining => stretchStartedWith = remaining;

            BearOffTheOpponentsLastChecker();

            Assert.IsFalse(_state.IsMatchOver);
            Assert.IsTrue(_state.IsInFinalStretch);
            Assert.AreEqual(FinalStretch, _state.RemainingTurns);
            Assert.AreEqual(FinalStretch, stretchStartedWith);
        }

        [Test]
        public void DuringTheFinalStretch_TheOpponentGetsNoTurns()
        {
            CreateTargetMatch();
            BearOffTheOpponentsLastChecker();

            _state.EndTurn(); // The opponent's own turn ends (not counted).
            Assert.AreEqual(0, _state.CurrentPlayerId);
            Assert.AreEqual(FinalStretch, _state.RemainingTurns);

            _state.EndTurn(); // The scoring player's turn: counted, and the turn stays with them.
            Assert.AreEqual(0, _state.CurrentPlayerId);
            Assert.AreEqual(FinalStretch - 1, _state.RemainingTurns);
        }

        [Test]
        public void FinalStretch_NeverRaisesTheRemainingTurns()
        {
            CreateTargetMatch(turnLimit: 2);
            BearOffTheOpponentsLastChecker(); // One counted turn is spent getting there.

            Assert.AreEqual(1, _state.RemainingTurns);
        }

        [Test]
        public void WithoutAFinalStretch_TheOpponentRunningOutEndsTheMatch()
        {
            CreateTargetMatch(finalStretch: 0);

            BearOffTheOpponentsLastChecker();

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(1, _state.WinnerId, "The target was not reached.");
        }

        [Test]
        public void Clone_KeepsTheRulesAndTheFinalStretch()
        {
            CreateTargetMatch();
            BearOffTheOpponentsLastChecker();

            GameState clone = _state.Clone();

            Assert.AreSame(_state.Rules, clone.Rules);
            Assert.IsTrue(clone.IsInFinalStretch);
            Assert.AreEqual(_state.RemainingTurns, clone.RemainingTurns);

            clone.EndTurn();
            clone.EndTurn();
            Assert.AreEqual(0, clone.CurrentPlayerId, "The clone must know the opponent is out of checkers.");
            Assert.AreEqual(1, _state.CurrentPlayerId, "The real board must not move.");
        }

        #endregion

        #region Classic mode

        [Test]
        public void Classic_BearingOffTheLastChecker_EndsTheMatch()
        {
            _state = TestBoard.CreateEmpty(out _context);
            TestBoard.PlaceDefault(_state, 20, 0);
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 6, 1);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(0, _state.WinnerId);
        }

        [Test]
        public void Classic_DestroyingTheLastChecker_EndsTheMatch()
        {
            _state = TestBoard.CreateEmpty(out _context);
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.Place(_state, 3, new SacrificialChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.IsFalse(_state.HasCheckersInPlay(1));
            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(0, _state.WinnerId);
        }

        #endregion

        #region Helpers

        private void CreateTargetMatch(int target = Target, int turnLimit = TurnLimit, int finalStretch = FinalStretch)
        {
            _state = TestBoard.CreateWithRules(MatchRules.ForTarget(target, turnLimit, finalStretch), out _context);
            _matchEndedCount = 0;
            _state.MatchEnded += winner => _matchEndedCount++;
        }

        /// <summary>
        /// Player 0 keeps one checker far from home; player 1 has a single checker in its home board and
        /// bears it off on its own turn. Spends one counted turn (player 0's) on the way.
        /// </summary>
        private void BearOffTheOpponentsLastChecker()
        {
            TestBoard.PlaceDefault(_state, 10, 0);
            TestBoard.PlaceDefault(_state, OpponentHomePoint, 1);

            _state.EndTurn();
            Assert.AreEqual(1, _state.CurrentPlayerId);

            TestBoard.SetDice(_context, 3, 1);
            Assert.IsTrue(_state.TryBearOff(OpponentHomePoint));
        }

        #endregion
    }
}
