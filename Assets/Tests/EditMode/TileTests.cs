using System.Collections.Generic;
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
        public void PowderKeg_CountsDownOnEveryTurn_AndStartsAgainAfterTheBlast()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            PowderKegTile keg = new PowderKegTile();
            state.PlaceTile(3, keg);

            // Turn order: player 0, player 1, player 0, player 1. Nobody stands on it; the point counts anyway.
            int[] countOnTurn = new int[4];
            for (int turn = 0; turn < countOnTurn.Length; turn++)
            {
                countOnTurn[turn] = keg.Countdown;
                state.EndTurn();
            }

            CollectionAssert.AreEqual(new[] { 3, 2, 1, 3 }, countOnTurn);
        }

        [Test]
        public void PowderKeg_WhenTheCountRunsOut_DestroysEveryCheckerOnItForGood()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(3, new PowderKegTile());
            DefaultChecker mine = TestBoard.PlaceDefault(state, 3, 0);
            DefaultChecker mine2 = TestBoard.PlaceDefault(state, 3, 0);
            TestBoard.PlaceDefault(state, 10, 0); // Keeps player 0 in the match after the blast.
            TestBoard.PlaceDefault(state, 20, 1);

            List<BaseChecker> lost = new List<BaseChecker>();
            state.CheckerLostForGood += (destroyed, point) => lost.Add(destroyed);

            state.EndTurn();
            state.EndTurn();
            Assert.AreEqual(2, state.Nodes[3].CheckerCount, "Two turns in, the count has not run out.");

            state.EndTurn(); // The third turn ends: the keg blows up.
            Assert.IsTrue(state.Nodes[3].IsEmpty);
            CollectionAssert.AreEquivalent(new[] { mine, mine2 }, lost, "Lost for good: in a run they leave the deck.");
            Assert.AreEqual(0, state.GetBarCount(0), "Not even the bar.");
        }

        [Test]
        public void PowderKeg_SparesACheckerImmuneToTiles()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(3, new PowderKegTile());
            RecklessChecker reckless = TestBoard.Place(state, 3, new RecklessChecker(0));
            DefaultChecker plain = TestBoard.PlaceDefault(state, 3, 0);

            for (int turn = 0; turn < PowderKegTile.FuseTurns; turn++)
                state.EndTurn();

            Assert.AreEqual(3, state.FindNodeIndexOf(reckless));
            Assert.AreEqual(GameState.InvalidIndex, state.FindNodeIndexOf(plain));
        }

        [Test]
        public void PlunderCove_TakesBackTheTurnsScoreOfThePlayerStandingOnIt()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(3, new PlunderCoveTile());
            TestBoard.PlaceDefault(state, 3, 0);
            TestBoard.PlaceDefault(state, 20, 1);

            context.AddScore(0, 10); // An earlier turn's score is safe.
            context.RollDice(0);     // This turn begins.
            context.AddScore(0, 30);
            context.AddScore(1, 20); // Player 1 has no checker on the cove.

            state.EndTurn();

            Assert.AreEqual(10, context.GetScore(0));
            Assert.AreEqual(20, context.GetScore(1));
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
