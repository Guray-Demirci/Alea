using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Abilities;
using Tavla.Core.AI;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;
using Tavla.Core.Tiles;

namespace Tavla.Core.Tests
{
    /// <summary>Tiles whose rules run on the turn flow or on the score: round cycles, countdowns, locks and plunder.</summary>
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
                Assert.AreEqual(!dungeon.IsClosed, ((IOpenCloseTile)dungeon).IsOpen, "What the View shows.");
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
        public void PowderKeg_CountsDownOncePerRound_AndStartsAgainAfterTheBlast()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            PowderKegTile keg = new PowderKegTile();
            state.PlaceTile(3, keg);

            // Turn order: player 0, player 1, player 0, ... The count drops once both have played; nobody stands on the
            // point, it counts anyway.
            int[] countOnTurn = new int[8];
            for (int turn = 0; turn < countOnTurn.Length; turn++)
            {
                countOnTurn[turn] = keg.Countdown;
                state.EndTurn();
            }

            CollectionAssert.AreEqual(new[] { 3, 3, 2, 2, 1, 1, 3, 3 }, countOnTurn);
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

            for (int turn = 0; turn < 5; turn++)
                state.EndTurn();
            Assert.AreEqual(2, state.Nodes[3].CheckerCount, "Five turns in, the third round is not over yet.");

            state.EndTurn(); // Player 1 ends the third round: the keg blows up.
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

            for (int turn = 0; turn < 2 * PowderKegTile.FuseRounds; turn++)
                state.EndTurn();

            Assert.AreEqual(3, state.FindNodeIndexOf(reckless));
            Assert.AreEqual(GameState.InvalidIndex, state.FindNodeIndexOf(plain));
        }

        [Test]
        public void PlunderCove_ACaptureOnIt_ScoresNothing()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(3, new PlunderCoveTile());
            TestBoard.PlaceDefault(state, 0, 0);
            TestBoard.PlaceDefault(state, 3, 1);
            TestBoard.PlaceDefault(state, 20, 1); // Keeps player 1 in the match after the capture.
            TestBoard.SetDice(context, 3, 5);

            List<string> abilities = new List<string>();
            state.AbilityTriggered += (id, checker, point) => abilities.Add(id);

            Assert.IsTrue(state.TryMove(0, 3));

            Assert.AreEqual(1, state.GetBarCount(1), "The capture itself still happens.");
            Assert.AreEqual(0, context.GetScore(0));
            CollectionAssert.Contains(abilities, AbilityIds.Plundered);
        }

        [Test]
        public void PlunderCove_ABearOffFromIt_ScoresNothing()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(20, new PlunderCoveTile());
            TestBoard.PlaceDefault(state, 20, 0);
            TestBoard.PlaceDefault(state, 22, 0);
            TestBoard.SetDice(context, 4, 2);

            Assert.IsTrue(state.TryBearOff(20));
            Assert.AreEqual(0, context.GetScore(0));

            Assert.IsTrue(state.TryBearOff(22), "A bear-off from any other point scores as usual.");
            Assert.AreEqual(GameState.BearOffScore, context.GetScore(0));
        }

        [Test]
        public void PlunderCove_TakesNothingEarnedElsewhere_FromThePlayerStandingOnIt()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(3, new PlunderCoveTile());
            TestBoard.PlaceDefault(state, 3, 0);
            TestBoard.PlaceDefault(state, 10, 0);
            TestBoard.PlaceDefault(state, 13, 1);
            TestBoard.PlaceDefault(state, 20, 1); // Keeps player 1 in the match after the capture.
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(10, 13));
            state.EndTurn();

            Assert.AreEqual(GameState.CaptureScore, context.GetScore(0));
        }

        [Test]
        public void PlunderCove_SeizesACaptureNoCheckerLandedFor()
        {
            // The Clover lands on point 2 and strikes the enemy on the cove just ahead of it.
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            state.PlaceTile(3, new PlunderCoveTile());
            TestBoard.Place(state, 0, new CloverChecker(0));
            TestBoard.PlaceDefault(state, 3, 1);
            TestBoard.PlaceDefault(state, 20, 1); // Keeps player 1 in the match after the strike.
            TestBoard.SetDice(context, 2, 5);

            Assert.IsTrue(state.TryMove(0, 2));

            Assert.AreEqual(1, state.GetBarCount(1));
            Assert.AreEqual(0, context.GetScore(0));
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

        [Test]
        public void RemoveTile_MidMatch_GivesTheFoggedCheckersTheirAbilitiesBack()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(3, new FogTile());
            ArmoredChecker armored = TestBoard.Place(state, 0, new ArmoredChecker(0));
            TestBoard.SetDice(context, 3, 5);
            Assert.IsTrue(state.TryMove(0, 3));
            Assert.IsTrue(armored.AbilitiesSuppressed);

            state.RemoveTile(3);

            Assert.IsNull(state.Nodes[3].ActiveTile);
            Assert.IsFalse(armored.AbilitiesSuppressed);
        }

        [Test]
        public void PlaceTile_OverASwamp_FreesTheCheckerItHeld()
        {
            MatchContext context;
            GameState state = CreateStuckChecker(out context);
            BaseChecker stuck = state.Nodes[3].Checkers[0];

            state.PlaceTile(3, new MaelstromTile());

            Assert.IsInstanceOf<MaelstromTile>(state.Nodes[3].ActiveTile);
            Assert.AreEqual(3, state.FindNodeIndexOf(stuck), "A new tile catches only the checkers that land on it.");
            TestBoard.SetDice(context, 3, 5);
            Assert.IsTrue(state.TryMove(3, 8), "The Swamp's lock went with it.");
        }

        /// <summary>
        /// Player 0 moves its only checker onto a Swamp on point 3, where it always sinks — even on an unlucky
        /// random, whose every chance roll fails.
        /// </summary>
        private static GameState CreateStuckChecker(out MatchContext context)
        {
            GameState state = TestBoard.CreateEmpty(out context);
            state.PlaceTile(3, new SwampTile());
            TestBoard.PlaceDefault(state, 0, 0);
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));
            return state;
        }
    }
}
