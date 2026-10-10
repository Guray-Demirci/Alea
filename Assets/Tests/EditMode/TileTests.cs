using NUnit.Framework;
using Tavla.Core.AI;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;
using Tavla.Core.Tiles;

namespace Tavla.Core.Tests
{
    /// <summary>Tiles whose rules run on the turn flow: round cycles, countdowns and locks.</summary>
    public class TileTests
    {
        [Test]
        public void Dungeon_EachPlayerSeesItClosedAndThenOpen()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            DungeonTile dungeon = new DungeonTile();
            state.PlaceTile(10, dungeon);

            // Turn order: player 0, player 1, player 0, player 1.
            bool[] closedOnTurn = new bool[4];
            for (int turn = 0; turn < closedOnTurn.Length; turn++)
            {
                closedOnTurn[turn] = dungeon.IsClosed;
                state.EndTurn();
            }

            CollectionAssert.AreEqual(new[] { true, true, false, false }, closedOnTurn);
        }

        [Test]
        public void Dungeon_WhenTheOpponentIsSkipped_TheRoundStillEnds()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            DungeonTile dungeon = new DungeonTile();
            state.PlaceTile(10, dungeon);

            state.RequestSkipTurn(1);
            state.EndTurn();

            Assert.AreEqual(0, state.CurrentPlayerId);
            Assert.IsFalse(dungeon.IsClosed, "Player 0 plays again, so a new round has begun.");
        }

        [Test]
        public void PowderKeg_OnlyTheOwnersTurnsBurnTheFuse()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            PowderKegTile keg = new PowderKegTile();
            state.PlaceTile(3, keg);
            DefaultChecker checker = TestBoard.PlaceDefault(state, 0, 0);
            TestBoard.PlaceDefault(state, 10, 0); // Keeps player 0 in the match after the explosion.
            TestBoard.SetDice(context, 3, 5);
            Assert.IsTrue(state.TryMove(0, 3));

            state.EndTurn(); // Player 0's first turn ends.
            Assert.AreEqual(2, keg.GetRemainingFuse(checker));

            state.EndTurn(); // Player 1's turn does not burn it.
            Assert.AreEqual(2, keg.GetRemainingFuse(checker));

            state.EndTurn(); // Player 0's second turn.
            state.EndTurn();
            Assert.AreEqual(1, keg.GetRemainingFuse(checker));

            BaseChecker lost = null;
            state.CheckerLostForGood += (destroyed, point) => lost = destroyed;

            state.EndTurn(); // Player 0's third turn: the keg explodes.
            Assert.AreEqual(GameState.InvalidIndex, state.FindNodeIndexOf(checker));
            Assert.AreSame(checker, lost, "Lost for good: in a run it leaves the deck.");
        }

        [Test]
        public void Swamp_OnTheTurnTheCheckerSank_ItCanNeitherLeaveNorBeFreed()
        {
            MatchContext context;
            GameState state = CreateStuckChecker(out context);

            Assert.IsFalse(state.TryMove(3, 8));
            Assert.IsFalse(state.CanReleaseChecker(3), "Freeing it needs a later turn.");
        }

        [Test]
        public void Swamp_OnALaterTurn_SacrificingTheSmallestDieFreesTheChecker()
        {
            MatchContext context;
            GameState state = CreateStuckChecker(out context);
            state.EndTurn();
            state.EndTurn(); // Back to player 0.
            TestBoard.SetDice(context, 2, 5);

            Assert.IsTrue(state.HasAnyLegalMove(), "Freeing the checker is a playable action.");
            Assert.IsTrue(state.TryReleaseChecker(3));

            CollectionAssert.AreEqual(new[] { 5 }, context.CurrentRoll.RemainingMoves);
            Assert.IsTrue(state.TryMove(3, 8));
        }

        [Test]
        public void Bot_FreesItsLockedCheckerFirst()
        {
            MatchContext context;
            GameState state = CreateStuckChecker(out context);
            state.EndTurn();
            state.EndTurn(); // Back to player 0.
            TestBoard.SetDice(context, 2, 5);
            TavlaBot bot = new TavlaBot(0);

            BotMove move = bot.ChooseBestMove(state);
            Assert.IsTrue(move.IsRelease);
            Assert.AreEqual(3, move.FromIndex);

            Assert.IsTrue(bot.PlayOneMove(state));
            Assert.IsFalse(state.CanReleaseChecker(3), "The lock is gone.");
        }

        /// <summary>Player 0 moves its only checker onto a Swamp on point 3, where it always sinks.</summary>
        private static GameState CreateStuckChecker(out MatchContext context)
        {
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            state.PlaceTile(3, new SwampTile());
            TestBoard.PlaceDefault(state, 0, 0);
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));
            return state;
        }
    }
}
