using NUnit.Framework;
using Tavla.Core.AI;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;
using Tavla.Core.Tiles;

namespace Tavla.Core.Tests
{
    /// <summary>Special checkers, tiles and the bot, exercised through the real rules engine.</summary>
    public class AbilityTests
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
        public void ArmoredChecker_CannotBeCaptured()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.Place(_state, 3, new ArmoredChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsFalse(_state.TryMove(0, 3));
            Assert.AreEqual(MoveError.DestinationBlocked, _lastRejection);
        }

        [Test]
        public void DoomsdayChecker_CapturesTheWholePoint()
        {
            TestBoard.Place(_state, 0, new DoomsdayChecker(0));
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));
            Assert.AreEqual(2, _state.GetBarCount(1));
            Assert.AreEqual(2 * GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void PhoenixChecker_ReturnsToItsStartingPointInsteadOfTheBar()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            PhoenixChecker phoenix = TestBoard.Place(_state, 3, new PhoenixChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(0, _state.GetBarCount(1));
            Assert.AreEqual(GameState.GetHomeEntryIndex(1), _state.FindNodeIndexOf(phoenix));
            Assert.AreEqual(1, phoenix.RebirthCount);
            Assert.IsTrue(_context.LuxuryShopPending);
        }

        [Test]
        public void FogTile_SuppressesAbilitiesUntilTheCheckerLeaves()
        {
            ArmoredChecker armored = TestBoard.Place(_state, 0, new ArmoredChecker(0));
            _state.PlaceTile(3, new FogTile());
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsTrue(_state.TryMove(0, 3));
            Assert.IsTrue(armored.AbilitiesSuppressed);
            Assert.IsTrue(armored.CanBeCaptured, "Inside the Fog the Armored Checker is a plain checker.");

            Assert.IsTrue(_state.TryMove(3, 5));
            Assert.IsFalse(armored.AbilitiesSuppressed);
            Assert.IsFalse(armored.CanBeCaptured);
        }

        [Test]
        public void SpikePit_CapturesALandingChecker()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 0, 0);
            _state.PlaceTile(3, new SpikePitTile());
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(GameState.BarIndex, _state.FindNodeIndexOf(checker));
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(1), "A neutral tile's capture scores for the opponent.");
        }

        [Test]
        public void Bot_ChoosesALegalMoveWithoutTouchingTheBoard()
        {
            _state.SetupStandardBoard();
            TestBoard.SetDice(_context, 3, 1);
            TavlaBot bot = new TavlaBot(0);

            BotMove move = bot.ChooseBestMove(_state);

            Assert.IsTrue(move.IsValid);
            Assert.IsTrue(_state.CanReach(move.FromIndex, move.ToIndex));
            Assert.AreEqual(2, _context.CurrentRoll.RemainingMoves.Count, "Evaluation must not spend real dice.");
            Assert.AreEqual(2, _state.Nodes[0].CountFor(0), "Evaluation must not move real checkers.");
        }

        [Test]
        public void Bot_DoesNothingWhenItIsNotItsTurn()
        {
            _state.SetupStandardBoard();
            TestBoard.SetDice(_context, 3, 1);

            Assert.IsFalse(new TavlaBot(1).ChooseBestMove(_state).IsValid);
        }
    }
}
