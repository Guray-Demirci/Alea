using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Run;
using Tavla.Core.Runtime;
using Tavla.Core.Tiles;

namespace Tavla.Core.Tests
{
    /// <summary>
    /// Runs: validation of the design data (<see cref="RunDefinition"/> and its parts) and how
    /// <see cref="RunState"/> builds matches, settles them, keeps the deck and walks through the stages.
    /// </summary>
    public class RunTests
    {
        private const int Target = 50;
        private const int TurnLimit = 5;
        private const int StartingCheckers = 5;
        private const int FinalStretch = 2;
        private const int StartingGold = 4;
        private const int WinGold = 3;
        private const int BossWinGold = 5;
        private const int GoldPerUnusedTurn = 1;

        #region Definitions

        [Test]
        public void MatchDefinition_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchDefinition(0, TurnLimit, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchDefinition(Target, 0, 5));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchDefinition(Target, TurnLimit, GameState.MinCheckersPerPlayer - 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchDefinition(Target, TurnLimit, GameState.MaxCheckersPerPlayer + 1));
        }

        [Test]
        public void RunEconomy_RejectsNegativeGold()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunEconomy(-1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunEconomy(0, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunEconomy(0, 0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunEconomy(0, 0, 0, -1));
        }

        [Test]
        public void TilePlacement_RejectsPointsOffTheBoardAndAMissingFactory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TilePlacement.Of<SwampTile>(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TilePlacement.Of<SwampTile>(GameState.NodeCount));
            Assert.Throws<ArgumentNullException>(() => new TilePlacement(3, null));
        }

        [Test]
        public void StageDefinition_RejectsMissingMatchesAndTwoTilesOnOnePoint()
        {
            Assert.Throws<ArgumentNullException>(() => new StageDefinition(null));
            Assert.Throws<ArgumentException>(() => new StageDefinition(new MatchDefinition[0]));
            Assert.Throws<ArgumentException>(() => new StageDefinition(new MatchDefinition[] { null }));
            Assert.Throws<ArgumentException>(() => new StageDefinition(
                new[] { Match() },
                new[] { TilePlacement.Of<SwampTile>(7), TilePlacement.Of<FogTile>(7) }));
        }

        [Test]
        public void RunDefinition_RejectsInvalidValues()
        {
            StageDefinition[] stages = { Stage() };
            RunEconomy economy = Economy();

            Assert.Throws<ArgumentNullException>(() => new RunDefinition(null, StartingCheckers, FinalStretch, economy));
            Assert.Throws<ArgumentException>(
                () => new RunDefinition(new StageDefinition[0], StartingCheckers, FinalStretch, economy));
            Assert.Throws<ArgumentException>(
                () => new RunDefinition(new StageDefinition[] { null }, StartingCheckers, FinalStretch, economy));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RunDefinition(stages, GameState.MinCheckersPerPlayer - 1, FinalStretch, economy));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RunDefinition(stages, GameState.MaxCheckersPerPlayer + 1, FinalStretch, economy));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunDefinition(stages, StartingCheckers, -1, economy));
            Assert.Throws<ArgumentNullException>(() => new RunDefinition(stages, StartingCheckers, FinalStretch, null));
        }

        [Test]
        public void RunDefinition_RejectsAStageWhoseTilesLeaveNoRoomForTheCheckers()
        {
            TilePlacement[] tiles = new TilePlacement[12];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = TilePlacement.Of<FogTile>(i);

            Assert.Throws<ArgumentException>(() => Run(Stage(), Stage(tiles)));
        }

        [Test]
        public void RunDefinition_CountsTheMatchesOfEveryStage()
        {
            Assert.AreEqual(9, ThreeByThree().MatchCount);
        }

        [Test]
        public void Definitions_DoNotChangeWhenTheSourceListsDo()
        {
            List<MatchDefinition> matches = new List<MatchDefinition> { Match() };
            StageDefinition stage = new StageDefinition(matches);
            List<StageDefinition> stages = new List<StageDefinition> { stage };
            RunDefinition run = new RunDefinition(stages, StartingCheckers, FinalStretch, Economy());

            matches.Add(Match());
            stages.Add(Stage());

            Assert.AreEqual(1, stage.Matches.Count);
            Assert.AreEqual(1, run.Stages.Count);
            Assert.AreEqual(1, run.MatchCount);
        }

        [Test]
        public void TileLayout_KeepsEarlierStagesTiles_AndALaterTileReplacesAnEarlierOne()
        {
            RunDefinition run = Run(
                Stage(),
                Stage(TilePlacement.Of<SwampTile>(7), TilePlacement.Of<FogTile>(15)),
                Stage(TilePlacement.Of<SpikePitTile>(15), TilePlacement.Of<TollBoothTile>(2)));

            Assert.AreEqual(0, run.GetTileLayout(0).Count);
            AssertLayout(run.GetTileLayout(1), (7, SwampTile.Id), (15, FogTile.Id));
            AssertLayout(run.GetTileLayout(2), (2, TollBoothTile.Id), (7, SwampTile.Id), (15, SpikePitTile.Id));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.GetTileLayout(3));
        }

        #endregion

        #region Starting a match

        [Test]
        public void StartMatch_BuildsTheBoardFromTheDefinition()
        {
            RunState run = new RunState(Run(Stage(Match(opponentCheckers: 7))), new FixedRandom());

            RunMatch match = run.StartMatch();

            Assert.AreSame(match, run.ActiveMatch);
            Assert.AreEqual(StartingCheckers, CountCheckers(match.Board, 0), "The player fields the deck.");
            Assert.AreEqual(7, CountCheckers(match.Board, 1));

            MatchRules rules = match.Board.Rules;
            Assert.AreEqual(Target, rules.TargetScore);
            Assert.AreEqual(TurnLimit, rules.TurnLimit);
            Assert.AreEqual(FinalStretch, rules.FinalStretchTurns);
            Assert.AreEqual(0, rules.ScoringPlayerId);

            Assert.AreEqual(StartingGold, match.Context.GetGold(0), "The run's purse is lent to the match.");
            Assert.AreSame(match.Context, match.Board.Context);
        }

        [Test]
        public void StartMatch_PlacesTheStageTiles_FreshForEveryMatch()
        {
            RunState run = new RunState(Run(Stage(new[] { Match(), Match() }, TilePlacement.Of<PowderKegTile>(9))),
                                        new FixedRandom());

            ITileEffect first = run.StartMatch().Board.Nodes[9].ActiveTile;
            Win(run);
            ITileEffect second = run.StartMatch().Board.Nodes[9].ActiveTile;

            Assert.IsInstanceOf<PowderKegTile>(first);
            Assert.IsInstanceOf<PowderKegTile>(second);
            Assert.AreNotSame(first, second, "Tiles keep per-match state, so each match needs its own.");
        }

        [Test]
        public void StartMatch_KeepsTheStartingCheckersOffTheTiles()
        {
            // Without the tiles, FixedRandom would deal player 0 onto points 0 and 1, and player 1 onto 6 and 7.
            int[] tilePoints = { 0, 1, 6, 7 };
            TilePlacement[] tiles = new TilePlacement[tilePoints.Length];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = TilePlacement.Of<FogTile>(tilePoints[i]);

            RunState run = new RunState(Run(Stage(tiles)), new FixedRandom());
            RunMatch match = run.StartMatch();

            foreach (int point in tilePoints)
            {
                Assert.AreEqual(0, match.Board.Nodes[point].CheckerCount, $"Point {point}");
                Assert.IsNotNull(match.Board.Nodes[point].ActiveTile);
            }
        }

        [Test]
        public void StartMatch_WhileAMatchIsBeingPlayed_Throws()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            run.StartMatch();

            Assert.IsFalse(run.CanStartMatch);
            Assert.Throws<InvalidOperationException>(() => run.StartMatch());
        }

        [Test]
        public void StartMatch_ThatFails_LeavesTheRunUntouched()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());

            // Passes the check in AddToDeck, then breaks: GameState.SetupPlayer rejects the null checker.
            int calls = 0;
            run.AddToDeck(ownerId => calls++ == 0 ? new DefaultChecker(ownerId) : null);

            Assert.Throws<ArgumentException>(() => run.StartMatch());
            Assert.IsNull(run.ActiveMatch);
            Assert.IsTrue(run.CanStartMatch);
            Assert.AreEqual(0, run.CompletedMatches);
        }

        #endregion

        #region Deck

        [Test]
        public void ACheckerAddedToTheDeck_JoinsTheNextMatch()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            run.StartMatch();
            Win(run);

            run.AddToDeck(ownerId => new ArmoredChecker(ownerId));
            RunMatch match = run.StartMatch();

            Assert.AreEqual(StartingCheckers + 1, run.Deck.Count);
            Assert.AreEqual(StartingCheckers + 1, CountCheckers(match.Board, 0));
            Assert.AreEqual(1, FindCheckers<ArmoredChecker>(match.Board).Count);
        }

        [Test]
        public void EveryMatch_GetsNewCheckerObjects()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            List<DefaultChecker> first = FindCheckers<DefaultChecker>(run.StartMatch().Board);
            Win(run);
            GameState second = run.StartMatch().Board;

            foreach (DefaultChecker checker in first)
                Assert.AreEqual(GameState.InvalidIndex, second.FindNodeIndexOf(checker),
                    "Checkers keep per-match state, so each match needs its own.");
        }

        [Test]
        public void AddToDeck_RejectsBrokenFactoriesAndAFullDeck()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());

            Assert.Throws<ArgumentNullException>(() => run.AddToDeck(null));
            Assert.Throws<ArgumentException>(() => run.AddToDeck(ownerId => null));
            Assert.Throws<ArgumentException>(() => run.AddToDeck(ownerId => new DefaultChecker(1)));

            while (run.Deck.Count < GameState.MaxCheckersPerPlayer)
                run.AddToDeck(ownerId => new DefaultChecker(ownerId));

            Assert.Throws<InvalidOperationException>(() => run.AddToDeck(ownerId => new DefaultChecker(ownerId)));
        }

        [Test]
        public void TheDeck_HoldsOneOfEachSpecialChecker_ButAnyNumberOfPlainOnes()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            run.AddToDeck(ownerId => new ArmoredChecker(ownerId));

            Assert.IsTrue(run.OwnsChecker(ArmoredChecker.Id));
            Assert.Throws<InvalidOperationException>(() => run.AddToDeck(ownerId => new ArmoredChecker(ownerId)));

            run.AddToDeck(ownerId => new DefaultChecker(ownerId));
            run.AddToDeck(ownerId => new DefaultChecker(ownerId));
            Assert.AreEqual(StartingCheckers + 3, run.Deck.Count);
        }

        [Test]
        public void ASacrificedChecker_LeavesTheDeck_WhenItsMatchEnds()
        {
            // Added last, the Second Chance is dealt last: it tops its point, so it is the one that can act.
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            run.AddToDeck(ownerId => new ArmoredChecker(ownerId));
            run.AddToDeck(ownerId => new SecondChanceChecker(ownerId));
            GameState board = run.StartMatch().Board;

            int point = 0;
            while (point < GameState.NodeCount && !board.CanSacrificeChecker(point))
                point++;
            Assert.IsTrue(board.TrySacrificeChecker(point), "No point offers the Second Chance's sacrifice.");
            Assert.AreEqual(StartingCheckers + 2, run.Deck.Count, "The deck only changes between matches.");

            Win(run);

            Assert.AreEqual(StartingCheckers + 1, run.Deck.Count);
            Assert.IsFalse(run.OwnsChecker(SecondChanceChecker.Id), "The shop may offer it again.");
            Assert.AreEqual(1, FindCheckers<ArmoredChecker>(run.StartMatch().Board).Count,
                "Only the sacrificed one is gone.");
        }

        [Test]
        public void ACheckerBlownUpByAPowderKeg_LeavesTheDeck()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            run.AddToDeck(ownerId => new ArmoredChecker(ownerId));
            GameState board = run.StartMatch().Board;
            ArmoredChecker armored = FindCheckers<ArmoredChecker>(board)[0];

            board.DestroyChecker(armored, board.FindNodeIndexOf(armored)); // What an exploding keg does.
            Win(run);

            Assert.AreEqual(StartingCheckers, run.Deck.Count);
            Assert.AreEqual(0, FindCheckers<ArmoredChecker>(run.StartMatch().Board).Count);
        }

        [Test]
        public void ADeckThatFallsBelowTheMinimum_IsFilledUpWithPlainCheckers()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            List<DeckEntry> before = new List<DeckEntry>(run.Deck);
            GameState board = run.StartMatch().Board;

            DefaultChecker victim = null;
            foreach (DefaultChecker checker in FindCheckers<DefaultChecker>(board))
            {
                if (checker.OwnerId == run.PlayerId)
                    victim = checker;
            }

            board.DestroyChecker(victim, board.FindNodeIndexOf(victim));
            Win(run);

            int kept = 0;
            foreach (DeckEntry entry in run.Deck)
            {
                if (before.Contains(entry))
                    kept++;
            }

            Assert.AreEqual(GameState.MinCheckersPerPlayer - 1, kept, "The lost checker's entry is gone...");
            Assert.AreEqual(GameState.MinCheckersPerPlayer, run.Deck.Count, "...and a plain checker took its place.");
            Assert.AreEqual(GameState.MinCheckersPerPlayer, CountCheckers(run.StartMatch().Board, run.PlayerId));
        }

        #endregion

        #region Settling a match

        [Test]
        public void WinOnTheFirstTurn_PaysTheRewardAndEveryTurnLeft()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            MatchOutcome outcome = null;
            run.MatchFinished += result => outcome = result;
            run.StartMatch();

            Win(run);

            Assert.IsTrue(outcome.IsWin);
            Assert.AreEqual(TurnLimit - 1, outcome.UnusedTurns, "The turn the target was reached in counts as used.");
            Assert.AreEqual(WinGold, outcome.WinGold);
            Assert.AreEqual((TurnLimit - 1) * GoldPerUnusedTurn, outcome.UnusedTurnGold);
            Assert.AreEqual(StartingGold + outcome.RewardGold, outcome.GoldAfter);
            Assert.AreEqual(outcome.GoldAfter, run.Gold);
        }

        [Test]
        public void WinningABoss_PaysTheBossReward()
        {
            RunState run = new RunState(Run(Stage(Match(isBoss: true))), new FixedRandom());
            MatchOutcome outcome = null;
            run.MatchFinished += result => outcome = result;
            run.StartMatch();

            Win(run);

            Assert.AreEqual(BossWinGold, outcome.WinGold);
        }

        [Test]
        public void WinDuringTheOpponentsTurn_DoesNotTakeOffAnotherTurn()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            MatchOutcome outcome = null;
            run.MatchFinished += result => outcome = result;
            RunMatch match = run.StartMatch();

            match.Board.EndTurn(); // The player's first turn is used up.
            Assert.AreEqual(1, match.Board.CurrentPlayerId);
            match.Context.AddScore(0, Target);

            Assert.IsTrue(outcome.IsWin);
            Assert.AreEqual(TurnLimit - 1, outcome.UnusedTurns);
        }

        [Test]
        public void MatchFinished_SeesTheRunAlreadyMovedOn()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            run.MatchFinished += outcome =>
            {
                Assert.IsNull(run.ActiveMatch);
                Assert.AreEqual(1, run.CompletedMatches);
                Assert.AreEqual(0, run.StageIndex);
                Assert.AreEqual(1, run.MatchIndex);
                Assert.IsTrue(run.CanStartMatch);
            };
            run.StartMatch();

            Win(run);

            Assert.AreEqual(1, run.CompletedMatches);
        }

        [Test]
        public void GoldChangedDuringAMatch_IsCarriedToTheNextOne()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            RunMatch first = run.StartMatch();

            first.Context.AddGold(0, -2); // A Toll Booth, say.
            first.Context.AddScore(0, Target);

            int expected = StartingGold - 2 + WinGold + (TurnLimit - 1) * GoldPerUnusedTurn;
            Assert.AreEqual(expected, run.Gold);
            Assert.AreEqual(expected, run.StartMatch().Context.GetGold(0));
        }

        [Test]
        public void Loss_EndsTheRun_WithoutAReward()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            MatchOutcome outcome = null;
            List<RunStatus> ended = new List<RunStatus>();
            run.MatchFinished += result => outcome = result;
            run.RunEnded += ended.Add;
            RunMatch match = run.StartMatch();

            // Only the player's turns are counted: run them all out without scoring.
            while (!match.Board.IsMatchOver)
                match.Board.EndTurn();

            Assert.IsFalse(outcome.IsWin);
            Assert.AreEqual(0, outcome.RewardGold);
            Assert.AreEqual(StartingGold, run.Gold);
            Assert.AreEqual(RunStatus.Lost, run.Status);
            CollectionAssert.AreEqual(new[] { RunStatus.Lost }, ended);
            Assert.IsFalse(run.CanStartMatch);
            Assert.Throws<InvalidOperationException>(() => run.StartMatch());
        }

        [Test]
        public void WinningEveryMatch_WalksThroughTheStagesInOrder_AndWinsTheRun()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom());
            List<RunStatus> ended = new List<RunStatus>();
            run.RunEnded += ended.Add;
            List<string> played = new List<string>();
            List<int> tileCounts = new List<int>();

            while (run.CanStartMatch)
            {
                RunMatch match = run.StartMatch();
                played.Add($"{match.StageIndex}-{match.MatchIndex}");
                tileCounts.Add(CountTiles(match.Board));
                Win(run);
            }

            CollectionAssert.AreEqual(
                new[] { "0-0", "0-1", "0-2", "1-0", "1-1", "1-2", "2-0", "2-1", "2-2" }, played);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1, 1, 2, 2, 2 }, tileCounts, "Tiles stay for the rest of the run.");
            Assert.AreEqual(RunStatus.Won, run.Status);
            Assert.AreEqual(9, run.CompletedMatches);
            CollectionAssert.AreEqual(new[] { RunStatus.Won }, ended);
        }

        [Test]
        public void PlayingFromSeatOne_TheTargetAndTheDeckFollowThePlayer()
        {
            RunState run = new RunState(ThreeByThree(), new FixedRandom(), playerId: 1);
            RunMatch match = run.StartMatch();

            Assert.AreEqual(1, match.Board.Rules.ScoringPlayerId);
            Assert.AreEqual(StartingCheckers, CountCheckers(match.Board, 1));
            Assert.AreEqual(StartingGold, match.Context.GetGold(1));

            match.Context.AddScore(1, Target);

            Assert.AreEqual(1, run.CompletedMatches);
        }

        #endregion

        #region Helpers

        private static MatchDefinition Match(int target = Target, int turnLimit = TurnLimit, int opponentCheckers = 5,
                                             bool isBoss = false)
        {
            return new MatchDefinition(target, turnLimit, opponentCheckers, isBoss);
        }

        private static StageDefinition Stage(params TilePlacement[] newTiles)
        {
            return Stage(new[] { Match() }, newTiles);
        }

        private static StageDefinition Stage(MatchDefinition match)
        {
            return Stage(new[] { match });
        }

        private static StageDefinition Stage(MatchDefinition[] matches, params TilePlacement[] newTiles)
        {
            return new StageDefinition(matches, newTiles);
        }

        private static RunEconomy Economy()
        {
            return new RunEconomy(StartingGold, WinGold, BossWinGold, GoldPerUnusedTurn);
        }

        private static RunDefinition Run(params StageDefinition[] stages)
        {
            return new RunDefinition(stages, StartingCheckers, FinalStretch, Economy());
        }

        /// <summary>The first game mode's shape: 3 stages of 3 matches ending in a boss, each stage adding one tile.</summary>
        private static RunDefinition ThreeByThree()
        {
            MatchDefinition[] matches = { Match(), Match(), Match(isBoss: true) };

            return Run(
                Stage(matches),
                Stage(matches, TilePlacement.Of<SwampTile>(8)),
                Stage(matches, TilePlacement.Of<FogTile>(14)));
        }

        /// <summary>Wins the active match on the spot: abilities can score directly, so the target is just added.</summary>
        private static void Win(RunState run)
        {
            run.ActiveMatch.Context.AddScore(run.PlayerId, run.ActiveMatch.Definition.TargetScore);
        }

        private static int CountCheckers(GameState board, int playerId)
        {
            int count = 0;
            for (int i = 0; i < GameState.NodeCount; i++)
                count += board.Nodes[i].CountFor(playerId);

            return count;
        }

        private static List<T> FindCheckers<T>(GameState board) where T : BaseChecker
        {
            List<T> found = new List<T>();
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                foreach (BaseChecker checker in board.Nodes[i].Checkers)
                {
                    if (checker is T match)
                        found.Add(match);
                }
            }

            return found;
        }

        private static int CountTiles(GameState board)
        {
            int count = 0;
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                if (board.Nodes[i].ActiveTile != null)
                    count++;
            }

            return count;
        }

        private static void AssertLayout(IReadOnlyList<TilePlacement> layout, params (int Point, string EffectId)[] expected)
        {
            Assert.AreEqual(expected.Length, layout.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Point, layout[i].Point);
                Assert.AreEqual(expected[i].EffectId, layout[i].CreateTile().EffectId);
            }
        }

        #endregion
    }
}
