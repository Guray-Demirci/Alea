using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Abilities;
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
        /// <summary>The Siege Checker's "+2 points", on the game's score scale.</summary>
        private const int SiegeBonus = 2 * GameState.ScoreScale;

        /// <summary>The Navigator Checker's footprints: a capture there is worth 2 and a bear-off 4.</summary>
        private const int FootprintCaptureScore = 2 * GameState.ScoreScale;
        private const int FootprintBearOffScore = 4 * GameState.ScoreScale;

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
        public void PhoenixChecker_SkipsAFullStartingPoint()
        {
            int home = GameState.GetHomeEntryIndex(1);
            int next = home + GameState.GetDirection(1);
            for (int i = 0; i < GameState.StackLimit; i++)
                TestBoard.PlaceDefault(_state, home, 1);

            TestBoard.PlaceDefault(_state, 0, 0);
            PhoenixChecker phoenix = TestBoard.Place(_state, 3, new PhoenixChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(next, _state.FindNodeIndexOf(phoenix));
            Assert.AreEqual(GameState.StackLimit, _state.Nodes[home].CheckerCount);
            Assert.AreEqual(0, _state.GetBarCount(1));
        }

        [Test]
        public void PhoenixChecker_SkipsEveryFullPointInARow()
        {
            int home = GameState.GetHomeEntryIndex(1);
            int direction = GameState.GetDirection(1);
            for (int i = 0; i < GameState.StackLimit; i++)
            {
                TestBoard.PlaceDefault(_state, home, 1);
                TestBoard.PlaceDefault(_state, home + direction, 1);
            }

            TestBoard.PlaceDefault(_state, 0, 0);
            PhoenixChecker phoenix = TestBoard.Place(_state, 3, new PhoenixChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(home + 2 * direction, _state.FindNodeIndexOf(phoenix));
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
        public void SpikePit_AnArmoredChecker_PassesUnharmed_AndTheSpikesStay()
        {
            ArmoredChecker armored = TestBoard.Place(_state, 0, new ArmoredChecker(0));
            DefaultChecker plain = TestBoard.PlaceDefault(_state, 0, 0);
            _state.PlaceTile(3, new SpikePitTile());
            TestBoard.SetDice(_context, 3, 3);

            Assert.IsTrue(_state.TryMove(armored, 3));
            Assert.AreEqual(3, _state.FindNodeIndexOf(armored));
            Assert.IsInstanceOf<SpikePitTile>(_state.Nodes[3].ActiveTile, "The armor does not break the spikes.");

            Assert.IsTrue(_state.TryMove(plain, 3));
            Assert.AreEqual(GameState.BarIndex, _state.FindNodeIndexOf(plain), "The next checker is impaled as usual.");
            Assert.AreEqual(3, _state.FindNodeIndexOf(armored));
        }

        // FixedRandom makes the Maelstrom pick point 0.
        [Test]
        public void Maelstrom_FlingsALandingChecker()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 2, 0);
            _state.PlaceTile(5, new MaelstromTile());
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(0, _state.FindNodeIndexOf(checker));
        }

        [Test]
        public void Maelstrom_PassesAFullPointOnToTheNextOne()
        {
            for (int i = 0; i < GameState.StackLimit; i++)
            {
                TestBoard.PlaceDefault(_state, 0, 0);
                TestBoard.PlaceDefault(_state, 1, 1);
            }

            DefaultChecker checker = TestBoard.PlaceDefault(_state, 2, 0);
            _state.PlaceTile(5, new MaelstromTile());
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(2, _state.FindNodeIndexOf(checker), "Points 0 and 1 are full.");
            Assert.AreEqual(GameState.StackLimit, _state.Nodes[0].CheckerCount);
        }

        [Test]
        public void Maelstrom_PassingOnWrapsAroundTheBoard()
        {
            for (int i = 0; i < GameState.StackLimit; i++)
                TestBoard.PlaceDefault(_state, 0, 0);

            // Player 1 runs 23 -> 0, so the point after 0 is 23.
            MaelstromTile maelstrom = new MaelstromTile();
            _state.PlaceTile(5, maelstrom);
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 5, 1);

            maelstrom.OnCheckerLanded(_context, _state.Nodes[5], checker);

            Assert.AreEqual(GameState.NodeCount - 1, _state.FindNodeIndexOf(checker));
        }

        [Test]
        public void Maelstrom_APlainCheckerThrownOntoAMadePoint_IsBrokenThere()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 2, 0);
            TestBoard.PlaceDefault(_state, 0, 1);
            TestBoard.PlaceDefault(_state, 0, 1);
            _state.PlaceTile(5, new MaelstromTile());
            List<string> crashes = new List<string>();
            _state.AbilityTriggered += (abilityId, reported, nodeIndex) =>
            {
                if (abilityId == AbilityIds.TeleportCrash && ReferenceEquals(reported, checker))
                    crashes.Add(abilityId + "@" + nodeIndex);
            };
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(GameState.BarIndex, _state.FindNodeIndexOf(checker));
            Assert.AreEqual(2, _state.Nodes[0].CountFor(1), "The made point stands.");
            CollectionAssert.AreEqual(new[] { AbilityIds.TeleportCrash + "@0" }, crashes);
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(1), "The break scores for the opponent.");
        }

        [Test]
        public void Maelstrom_ADoomsdayThrownOntoAMadePoint_BreaksIt()
        {
            DoomsdayChecker doomsday = TestBoard.Place(_state, 2, new DoomsdayChecker(0));
            TestBoard.PlaceDefault(_state, 0, 1);
            TestBoard.PlaceDefault(_state, 0, 1);
            _state.PlaceTile(5, new MaelstromTile());
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(0, _state.FindNodeIndexOf(doomsday));
            Assert.AreEqual(1, _state.Nodes[0].CheckerCount);
            Assert.AreEqual(2, _state.GetBarCount(1));
            Assert.AreEqual(2 * GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void Maelstrom_ACheckerThrownOntoALoneEnemy_HitsIt()
        {
            DefaultChecker checker = TestBoard.PlaceDefault(_state, 2, 0);
            DefaultChecker blot = TestBoard.PlaceDefault(_state, 0, 1);
            _state.PlaceTile(5, new MaelstromTile());
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(0, _state.FindNodeIndexOf(checker));
            Assert.AreEqual(GameState.BarIndex, _state.FindNodeIndexOf(blot));
            Assert.AreEqual(1, _state.Nodes[0].CheckerCount);
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void Maelstrom_PassesAnArmoredCheckerOverAMadePoint()
        {
            ArmoredChecker armored = TestBoard.Place(_state, 2, new ArmoredChecker(0));
            TestBoard.PlaceDefault(_state, 0, 1);
            TestBoard.PlaceDefault(_state, 0, 1);
            _state.PlaceTile(5, new MaelstromTile());
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(1, _state.FindNodeIndexOf(armored), "It can neither land on nor break on point 0.");
            Assert.AreEqual(2, _state.Nodes[0].CountFor(1));
        }

        [Test]
        public void GoldenChecker_EarnsNoGoldForTheCheckerItCaptures()
        {
            TestBoard.Place(_state, 0, new GoldenChecker(0));
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(0, _context.GetGold(0));
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(0), "The capture still scores normally.");
        }

        [Test]
        public void GoldenChecker_EarnsGoldWhenBorneOff()
        {
            TestBoard.Place(_state, 20, new GoldenChecker(0));
            TestBoard.PlaceDefault(_state, 0, 0); // Keeps player 0 in the match after the bear-off.
            TestBoard.SetDice(_context, 4, 2);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.AreEqual(2, _context.GetGold(0));
        }

        [Test]
        public void GoldenChecker_BearOffThatReachesTheTarget_StillEarnsGold()
        {
            _state = TestBoard.CreateWithRules(MatchRules.ForTarget(GameState.BearOffScore, 10, 3), out _context);
            int goldAtMatchEnd = -1;
            _state.MatchEnded += winner => goldAtMatchEnd = _context.GetGold(0);

            TestBoard.Place(_state, 20, new GoldenChecker(0));
            TestBoard.PlaceDefault(_state, 0, 0); // Keeps player 0 in play: only the target ends the match.
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 4, 2);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.IsTrue(_state.IsMatchOver);
            Assert.AreEqual(0, _state.WinnerId);
            Assert.AreEqual(2, goldAtMatchEnd, "The run settles the purse inside MatchEnded; the gold must be in it.");
        }

        [Test]
        public void SiegeChecker_AFriendlyCheckerMakingItsPoint_EarnsTheBonus()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.Place(_state, 3, new SiegeChecker(0));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(SiegeBonus, _context.GetScore(0));
        }

        [Test]
        public void SiegeChecker_AThirdCheckerJoiningTheMadePoint_EarnsNothing()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.PlaceDefault(_state, 2, 0);
            TestBoard.Place(_state, 5, new SiegeChecker(0));
            TestBoard.SetDice(_context, 5, 3);

            Assert.IsTrue(_state.TryMove(0, 5));
            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(SiegeBonus, _context.GetScore(0), "Only the arrival that made the point counts.");
        }

        [Test]
        public void SiegeChecker_MovingOntoAFriendlyChecker_EarnsNothing()
        {
            TestBoard.Place(_state, 0, new SiegeChecker(0));
            TestBoard.PlaceDefault(_state, 3, 0);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(0, _context.GetScore(0), "The Siege must be waiting on the point, not arriving.");
        }

        [Test]
        public void SacrificialChecker_IsDestroyedAndDiscountsTheShopByOneGold()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            SacrificialChecker sacrificial = TestBoard.Place(_state, 3, new SacrificialChecker(1));
            TestBoard.PlaceDefault(_state, 10, 1); // Keeps player 1 in the match after the sacrifice.
            TestBoard.SetDice(_context, 3, 5);
            List<BaseChecker> lost = new List<BaseChecker>();
            _state.CheckerLostForGood += (checker, point) => lost.Add(checker);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(0, _state.GetBarCount(1));
            Assert.AreEqual(GameState.InvalidIndex, _state.FindNodeIndexOf(sacrificial));
            Assert.AreEqual(1, _context.ShopDiscount);
            CollectionAssert.AreEqual(new[] { sacrificial }, lost, "Destroyed for good: in a run it leaves the deck.");
        }

        [Test]
        public void CatalystChecker_DoublesOnlyTheScoreItEarnsItself()
        {
            TestBoard.Place(_state, 0, new CatalystChecker(0));
            TestBoard.PlaceDefault(_state, 1, 0);
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.PlaceDefault(_state, 6, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));
            Assert.AreEqual(2 * GameState.CaptureScore, _context.GetScore(0));

            Assert.IsTrue(_state.TryMove(1, 6));
            Assert.AreEqual(3 * GameState.CaptureScore, _context.GetScore(0), "Another checker's capture is not doubled.");
        }

        [Test]
        public void CatalystChecker_DoublesItsBearOffScore()
        {
            TestBoard.Place(_state, 20, new CatalystChecker(0));
            TestBoard.PlaceDefault(_state, 0, 0); // Keeps player 0 in the match after the bear-off.
            TestBoard.SetDice(_context, 4, 2);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.AreEqual(2 * GameState.BearOffScore, _context.GetScore(0));
        }

        [Test]
        public void DoubleDownChecker_WhenTheBonusMisses_KeepsTheRemainingMoves()
        {
            TestBoard.Place(_state, 0, new DoubleDownChecker(0));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            CollectionAssert.AreEqual(new[] { 5 }, _context.CurrentRoll.RemainingMoves);
        }

        [Test]
        public void DoubleDownChecker_WhenTheBonusHits_GrantsAnExtraMove()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            TestBoard.Place(state, 0, new DoubleDownChecker(0));
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));

            CollectionAssert.AreEquivalent(new[] { 5, 3 }, context.CurrentRoll.RemainingMoves);
        }

        [Test]
        public void AnchorChecker_EnteringFromTheBar_PinsNothing()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.Place(_state, 3, new AnchorChecker(1));
            TestBoard.SetDice(_context, 3, 5);
            Assert.IsTrue(_state.TryMove(0, 3)); // Player 0 hits the Anchor; it goes to the bar.

            _state.EndTurn();
            TestBoard.SetDice(_context, 4, 2);
            Assert.IsTrue(_state.TryMove(GameState.BarIndex, GameState.NodeCount - 4));

            Assert.AreEqual(1, _context.RollDice(1).First, "A captured checker enters as a plain one; FixedRandom rolls 1.");
        }

        [Test]
        public void DrunkenChecker_StaggersOnePointPastTheChosenPoint()
        {
            DrunkenChecker drunken = TestBoard.Place(_state, 0, new DrunkenChecker(0));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(4, _state.FindNodeIndexOf(drunken), "FixedRandom staggers it one point long.");
            CollectionAssert.AreEqual(new[] { 5 }, _context.CurrentRoll.RemainingMoves, "It still pays the chosen die.");
        }

        [Test]
        public void DrunkenChecker_CanStaggerShort()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            DrunkenChecker drunken = TestBoard.Place(state, 0, new DrunkenChecker(0));
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));

            Assert.AreEqual(2, state.FindNodeIndexOf(drunken));
        }

        [Test]
        public void DrunkenChecker_LandsWhereAimed_WhenItCannotStaggerThere()
        {
            DrunkenChecker drunken = TestBoard.Place(_state, 0, new DrunkenChecker(0));
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(3, _state.FindNodeIndexOf(drunken), "The point past it is made by the opponent.");
        }

        [Test]
        public void DrunkenChecker_CapturesAnOpenEnemyItStaggersOnto()
        {
            TestBoard.Place(_state, 0, new DrunkenChecker(0));
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(1, _state.GetBarCount(1));
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void GunpowderChecker_CapturedByLanding_SendsTheCapturerToTheBarToo()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.Place(_state, 3, new GunpowderChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(1, _state.GetBarCount(1));
            Assert.AreEqual(1, _state.GetBarCount(0), "The capturer was blown up.");
            Assert.IsTrue(_state.Nodes[3].IsEmpty);
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(1), "Blowing up the capturer counts as a capture.");
        }

        [Test]
        public void GunpowderChecker_CapturedFromAfar_LeavesTheCapturerAlone()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            CloverChecker clover = TestBoard.Place(state, 0, new CloverChecker(0));
            TestBoard.Place(state, 2, new GunpowderChecker(1));
            TestBoard.SetDice(context, 1, 5);

            Assert.IsTrue(state.TryMove(0, 1)); // LuckyRandom: the Clover strikes the enemy just ahead.

            Assert.AreEqual(1, state.GetBarCount(1));
            Assert.AreEqual(1, state.FindNodeIndexOf(clover));
        }

        [Test]
        public void VoodooChecker_SwapsPlacesWithAFriendlyCheckerBehindIt()
        {
            DefaultChecker partner = TestBoard.PlaceDefault(_state, 2, 0);
            VoodooChecker voodoo = TestBoard.Place(_state, 5, new VoodooChecker(0));
            TestBoard.SetDice(_context, 3, 6);

            Assert.IsTrue(_state.TryMove(5, 2));

            Assert.AreEqual(2, _state.FindNodeIndexOf(voodoo));
            Assert.AreEqual(5, _state.FindNodeIndexOf(partner));
            CollectionAssert.AreEqual(new[] { 6 }, _context.CurrentRoll.RemainingMoves);
        }

        [Test]
        public void VoodooChecker_CannotGoBackWithoutAPartner()
        {
            TestBoard.Place(_state, 8, new VoodooChecker(0));
            TestBoard.PlaceDefault(_state, 5, 1);
            TestBoard.SetDice(_context, 3, 6);

            Assert.IsFalse(_state.CanMove(8, 5), "An enemy is no partner.");
            Assert.IsFalse(_state.CanMove(8, 2), "Nor is an empty point.");
        }

        [Test]
        public void VoodooChecker_MovingForwardOntoAFriend_IsAnOrdinaryMove()
        {
            TestBoard.Place(_state, 2, new VoodooChecker(0));
            TestBoard.PlaceDefault(_state, 5, 0);
            TestBoard.SetDice(_context, 3, 6);

            Assert.IsTrue(_state.TryMove(2, 5));

            Assert.AreEqual(2, _state.Nodes[5].CountFor(0));
            Assert.IsTrue(_state.Nodes[2].IsEmpty);
        }

        [Test]
        public void OrdinaryChecker_CannotSwapBackwards()
        {
            TestBoard.PlaceDefault(_state, 2, 0);
            TestBoard.PlaceDefault(_state, 5, 0);
            TestBoard.SetDice(_context, 3, 6);

            Assert.IsFalse(_state.CanMove(5, 2));
        }

        [Test]
        public void RecklessChecker_IgnoresTheTilesItLandsOnAndPasses()
        {
            RecklessChecker reckless = TestBoard.Place(_state, 0, new RecklessChecker(0));
            _state.PlaceTile(2, new TollBoothTile());
            _state.PlaceTile(3, new SpikePitTile());
            _state.PlaceTile(5, new DungeonTile(startClosed: true));
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsTrue(_state.TryMove(0, 3));
            Assert.AreEqual(3, _state.FindNodeIndexOf(reckless), "The spikes do not hurt it.");
            Assert.IsNotNull(_state.Nodes[3].ActiveTile, "Nor does it break them.");
            Assert.AreEqual(0, _context.GetScore(0), "It pays no toll (which, without gold, would cost score).");

            Assert.IsTrue(_state.TryMove(3, 5), "A closed Dungeon does not keep it out.");
        }

        [Test]
        public void RecklessChecker_KeepsTheScoreOfItsCaptureOnPlunderCove()
        {
            TestBoard.Place(_state, 0, new RecklessChecker(0));
            _state.PlaceTile(3, new PlunderCoveTile());
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.PlaceDefault(_state, 20, 1); // Keeps player 1 in the match after the capture.
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void TeleporterChecker_JumpsBeyondTheDice_ForEveryDieLeft()
        {
            TeleporterChecker teleporter = TestBoard.Place(_state, 0, new TeleporterChecker(0));
            TestBoard.SetDice(_context, 2, 3);

            Assert.IsTrue(_state.CanReach(0, 12));
            Assert.IsTrue(_state.TryMove(0, 12));

            Assert.AreEqual(12, _state.FindNodeIndexOf(teleporter));
            Assert.IsFalse(_context.CurrentRoll.HasMovesLeft);
        }

        [Test]
        public void TeleporterChecker_PlaysNormallyWhereADieReaches()
        {
            TestBoard.Place(_state, 0, new TeleporterChecker(0));
            TestBoard.SetDice(_context, 2, 3);

            Assert.IsTrue(_state.TryMove(0, 2));

            CollectionAssert.AreEqual(new[] { 3 }, _context.CurrentRoll.RemainingMoves);
        }

        [Test]
        public void TeleporterChecker_StillRespectsBlockades_AndOthersCannotTeleport()
        {
            TestBoard.Place(_state, 0, new TeleporterChecker(0));
            TestBoard.PlaceDefault(_state, 1, 0);
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 2, 3);

            Assert.IsFalse(_state.CanReach(0, 12), "A made point stays closed.");
            Assert.IsTrue(_state.CanReach(0, 14));
            Assert.IsFalse(_state.CanReach(1, 14), "A default checker has no free move.");
        }

        [Test]
        public void SecondChanceChecker_SacrificedForAnExtraDie()
        {
            SecondChanceChecker secondChance = TestBoard.Place(_state, 5, new SecondChanceChecker(0));
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 4);
            int sacrificed = 0;
            _state.CheckerSacrificed += (checker, point) => sacrificed++;

            Assert.IsTrue(_state.TrySacrificeChecker(5));

            Assert.AreEqual(GameState.InvalidIndex, _state.FindNodeIndexOf(secondChance));
            CollectionAssert.AreEqual(new[] { 3, 4, 1 }, _context.CurrentRoll.RemainingMoves, "FixedRandom rolls a 1.");
            Assert.AreEqual(1, sacrificed);
        }

        [Test]
        public void SecondChanceChecker_OnlyTheTopmostOwnCheckerOnItsTurnCanBeSacrificed()
        {
            TestBoard.Place(_state, 5, new SecondChanceChecker(0));
            TestBoard.PlaceDefault(_state, 5, 0);
            TestBoard.Place(_state, 10, new SecondChanceChecker(1));
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 4);

            Assert.IsFalse(_state.CanSacrificeChecker(5), "A default checker sits on top of it.");
            Assert.IsFalse(_state.CanSacrificeChecker(10), "Not on the opponent's checker.");
            Assert.IsFalse(_state.CanSacrificeChecker(0), "A default checker has nothing to sacrifice for.");
        }

        [Test]
        public void GrapplingChecker_HooksTheOpenEnemyAheadFirst()
        {
            TestBoard.Place(_state, 0, new GrapplingChecker(0));
            TestBoard.PlaceDefault(_state, 2, 1);
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.IsTrue(_state.Nodes[4].IsEmpty);
            Assert.AreEqual(1, _state.Nodes[2].CountFor(1), "One hook per landing.");
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void GrapplingChecker_HooksBehindIt_ButNotAMadePoint()
        {
            TestBoard.Place(_state, 0, new GrapplingChecker(0));
            TestBoard.PlaceDefault(_state, 2, 1);
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.IsTrue(_state.Nodes[2].IsEmpty);
            Assert.AreEqual(2, _state.Nodes[4].CountFor(1));
        }

        [Test]
        public void CloverChecker_StrikesTheEnemyJustAhead_BeforeTheOneBehind()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            CloverChecker clover = TestBoard.Place(state, 0, new CloverChecker(0));
            DefaultChecker behind = TestBoard.PlaceDefault(state, 2, 1);
            DefaultChecker ahead = TestBoard.PlaceDefault(state, 4, 1);
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));

            Assert.AreEqual(GameState.BarIndex, state.FindNodeIndexOf(ahead));
            Assert.AreEqual(2, state.FindNodeIndexOf(behind), "One strike per move.");
            Assert.AreEqual(3, state.FindNodeIndexOf(clover), "With an enemy next to it, it never hits itself.");
        }

        [Test]
        public void CloverChecker_PassesOverAnArmoredChecker_ToStrikeBehind()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            TestBoard.Place(state, 0, new CloverChecker(0));
            DefaultChecker behind = TestBoard.PlaceDefault(state, 2, 1);
            ArmoredChecker armored = TestBoard.Place(state, 4, new ArmoredChecker(1));
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));

            Assert.AreEqual(GameState.BarIndex, state.FindNodeIndexOf(behind));
            Assert.AreEqual(4, state.FindNodeIndexOf(armored));
        }

        [Test]
        public void CloverChecker_NextToAnEnemyItCannotCapture_NeitherStrikesNorHitsItself()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            CloverChecker clover = TestBoard.Place(state, 0, new CloverChecker(0));
            ArmoredChecker armored = TestBoard.Place(state, 4, new ArmoredChecker(1));
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));

            Assert.AreEqual(3, state.FindNodeIndexOf(clover));
            Assert.AreEqual(4, state.FindNodeIndexOf(armored));
        }

        [Test]
        public void CloverChecker_WithNoEnemyNextToIt_MayHitItself_ButNoOneScores()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            CloverChecker clover = TestBoard.Place(state, 0, new CloverChecker(0));
            DefaultChecker twoAhead = TestBoard.PlaceDefault(state, 5, 1);
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));

            Assert.AreEqual(GameState.BarIndex, state.FindNodeIndexOf(clover));
            Assert.AreEqual(5, state.FindNodeIndexOf(twoAhead), "It reaches only one point ahead.");
            Assert.AreEqual(0, context.GetScore(0));
            Assert.AreEqual(0, context.GetScore(1));
        }

        [Test]
        public void CloverChecker_WhenTheRollMisses_LeavesEveryoneInPlace()
        {
            CloverChecker clover = TestBoard.Place(_state, 0, new CloverChecker(0));
            DefaultChecker ahead = TestBoard.PlaceDefault(_state, 4, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3)); // FixedRandom: every chance roll misses.

            Assert.AreEqual(3, _state.FindNodeIndexOf(clover));
            Assert.AreEqual(4, _state.FindNodeIndexOf(ahead));
        }

        [Test]
        public void PoisonedRumChecker_TheCapturerRollsOneDieNextTurn()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.Place(_state, 3, new PoisonedRumChecker(1));
            TestBoard.SetDice(_context, 3, 5);
            Assert.IsTrue(_state.TryMove(0, 3));

            DiceRoll poisoned = _context.RollDice(0);
            Assert.AreEqual(1, poisoned.RemainingMoves.Count);
            Assert.AreEqual(0, poisoned.Second);

            Assert.AreEqual(4, _context.RollDice(0).RemainingMoves.Count, "Only one roll: FixedRandom's 1-1 is a double again.");
        }

        [Test]
        public void DiceHolderChecker_MovedWithTheFirstDie_RerollsTheOther()
        {
            TestBoard.Place(_state, 0, new DiceHolderChecker(0));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            CollectionAssert.AreEqual(new[] { 1 }, _context.CurrentRoll.RemainingMoves, "FixedRandom rolls a 1.");
        }

        [Test]
        public void DiceHolderChecker_MovedWithTheSecondDie_RerollsNothing()
        {
            TestBoard.Place(_state, 0, new DiceHolderChecker(0));
            TestBoard.PlaceDefault(_state, 10, 0);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(10, 15));
            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.IsFalse(_context.CurrentRoll.HasMovesLeft);
        }

        [Test]
        public void KrakenChecker_OnADouble_CapturesEveryOpenEnemy()
        {
            TestBoard.Place(_state, 0, new KrakenChecker(0));
            TestBoard.PlaceDefault(_state, 5, 1);
            TestBoard.PlaceDefault(_state, 10, 1);
            TestBoard.PlaceDefault(_state, 15, 1);
            TestBoard.PlaceDefault(_state, 15, 1);

            DiceRoll roll = _context.RollDice(0); // FixedRandom: 1-1.

            Assert.IsTrue(roll.IsDouble);
            Assert.AreEqual(2, _state.GetBarCount(1));
            Assert.AreEqual(2, _state.Nodes[15].CountFor(1), "A made point is not open.");
            Assert.AreEqual(2 * GameState.CaptureScore, _context.GetScore(0));
            Assert.AreEqual(4, roll.RemainingMoves.Count, "The dice stay in hand.");
        }

        [Test]
        public void KrakenChecker_SleepsWithoutADouble()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new SteppingRandom(), out context);
            TestBoard.Place(state, 0, new KrakenChecker(0));
            TestBoard.PlaceDefault(state, 5, 1);

            Assert.IsFalse(context.RollDice(0).IsDouble);
            Assert.AreEqual(0, state.GetBarCount(1));
        }

        [Test]
        public void KrakenChecker_SleepsThroughTheOpponentsDouble()
        {
            TestBoard.Place(_state, 0, new KrakenChecker(0));
            TestBoard.PlaceDefault(_state, 5, 1);

            Assert.IsTrue(_context.RollDice(1).IsDouble);
            Assert.AreEqual(0, _state.GetBarCount(1), "Only its owner's roll wakes a Kraken.");
        }

        [Test]
        public void MutinyChecker_TurnsTheCapturedCheckerIntoItsOwn()
        {
            TestBoard.Place(_state, 0, new MutinyChecker(0));
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.PlaceDefault(_state, 20, 1);
            TestBoard.SetDice(_context, 3, 5);
            BaseChecker added = null;
            _state.CheckerAdded += (checker, point) => added = checker;

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(2, _state.Nodes[3].CountFor(0), "The convert and the Mutiny make a point.");
            Assert.AreEqual(0, _state.GetBarCount(1));
            Assert.IsNotNull(added);
            Assert.AreEqual(0, added.OwnerId);
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void MutinyChecker_CannotConvertAPhoenix()
        {
            TestBoard.Place(_state, 0, new MutinyChecker(0));
            PhoenixChecker phoenix = TestBoard.Place(_state, 3, new PhoenixChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(GameState.GetHomeEntryIndex(1), _state.FindNodeIndexOf(phoenix));
            Assert.AreEqual(1, _state.Nodes[3].CountFor(0));
        }

        [Test]
        public void MutinyChecker_WithFifteenCheckers_CapturesNormally()
        {
            TestBoard.Place(_state, 0, new MutinyChecker(0));
            for (int i = 0; i < GameState.MaxCheckersPerPlayer - 1; i++)
                TestBoard.PlaceDefault(_state, 10 + i / GameState.StackLimit, 0);
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(1, _state.GetBarCount(1));
            Assert.AreEqual(1, _state.Nodes[3].CountFor(0));
        }

        [Test]
        public void HammerChecker_SmashesAPointOfTwo()
        {
            TestBoard.Place(_state, 0, new HammerChecker(0));
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(2, _state.GetBarCount(1));
            Assert.AreEqual(2 * GameState.CaptureScore, _context.GetScore(0));
        }

        [Test]
        public void HammerChecker_CannotEnterAPointOfThree_OrOneWithAnArmoredChecker()
        {
            TestBoard.Place(_state, 0, new HammerChecker(0));
            for (int i = 0; i < 3; i++)
                TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.PlaceDefault(_state, 5, 1);
            TestBoard.Place(_state, 5, new ArmoredChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsFalse(_state.CanMove(0, 3));
            Assert.IsFalse(_state.CanMove(0, 5));
        }

        [Test]
        public void BowmanChecker_FirstMoveOfTheTurn_ArmsOneArrowPerPointMoved()
        {
            BowmanChecker bowman = TestBoard.Place(_state, 0, new BowmanChecker(0));
            for (int i = 0; i < 4; i++)
                TestBoard.PlaceDefault(_state, 10 + i, 1);
            List<string> reported = new List<string>();
            _state.AbilityTriggered += (abilityId, checker, nodeIndex) => reported.Add(abilityId);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.IsTrue(_state.HasPendingVolley);
            Assert.AreSame(bowman, _state.PendingVolleyShooter);
            Assert.AreEqual(3, _state.PendingVolleySize);
            CollectionAssert.Contains(reported, AbilityIds.BowmanVolley);
        }

        [Test]
        public void BowmanChecker_EnteringFromTheBar_ShootsNothing_ButItsNextMoveDoes()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            BowmanChecker bowman = TestBoard.Place(_state, 3, new BowmanChecker(1));
            for (int i = 0; i < 4; i++)
                TestBoard.PlaceDefault(_state, 5 + i, 0);
            TestBoard.SetDice(_context, 3, 5);
            Assert.IsTrue(_state.TryMove(0, 3)); // Player 0 hits the Bowman; it goes to the bar.
            Assert.IsTrue(bowman.IsReentering);

            _state.EndTurn();
            TestBoard.SetDice(_context, 6, 2);
            Assert.IsTrue(_state.TryMove(GameState.BarIndex, GameState.NodeCount - 6));

            Assert.IsFalse(_state.HasPendingVolley, "A captured checker enters as a plain one.");
            Assert.IsFalse(bowman.IsReentering, "Once in, it has its abilities back.");

            Assert.IsTrue(_state.TryMove(bowman, GameState.NodeCount - 8));
            Assert.IsTrue(_state.HasPendingVolley, "The entry was not its move of the turn; this one is.");
            Assert.AreEqual(2, _state.PendingVolleySize);
        }

        [Test]
        public void ACapturedChecker_SentBackToTheBarByItsEntry_StaysPlainUntilItEntersAgain()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            RecklessChecker reckless = TestBoard.Place(_state, 3, new RecklessChecker(1));
            _state.PlaceTile(GameState.NodeCount - 6, new SpikePitTile());
            TestBoard.SetDice(_context, 3, 5);
            Assert.IsTrue(_state.TryMove(0, 3)); // Player 0 hits the Reckless; it goes to the bar.

            _state.EndTurn();
            TestBoard.SetDice(_context, 6, 2);
            Assert.IsTrue(_state.TryMove(GameState.BarIndex, GameState.NodeCount - 6));

            Assert.AreEqual(GameState.BarIndex, _state.FindNodeIndexOf(reckless), "It entered plain, so the spikes caught it.");
            Assert.IsTrue(reckless.IsReentering);

            Assert.IsTrue(_state.TryMove(GameState.BarIndex, GameState.NodeCount - 2));
            Assert.IsFalse(reckless.IsReentering);
        }

        [Test]
        public void BowmanChecker_Volley_IsFiredBeforeAnythingElse_AndReachesMadePoints()
        {
            TestBoard.Place(_state, 0, new BowmanChecker(0));
            DefaultChecker other = TestBoard.PlaceDefault(_state, 1, 0);
            DefaultChecker blot = TestBoard.PlaceDefault(_state, 10, 1);
            DefaultChecker madeLow = TestBoard.PlaceDefault(_state, 15, 1);
            DefaultChecker madeHigh = TestBoard.PlaceDefault(_state, 15, 1);
            TestBoard.PlaceDefault(_state, 20, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.IsTrue(_state.HasAnyLegalMove(), "The waiting volley keeps the turn going.");
            Assert.IsFalse(_state.CanPlayChecker(other));
            Assert.IsFalse(_state.TryMove(1, 6));
            Assert.AreEqual(MoveError.VolleyPending, _lastRejection);

            Assert.IsTrue(_state.TryFireVolley(new BaseChecker[] { blot, madeLow, madeHigh }));

            Assert.IsFalse(_state.HasPendingVolley);
            Assert.AreEqual(3, _state.GetBarCount(1), "Arrows reach a made point too.");
            Assert.AreEqual(3 * GameState.CaptureScore, _context.GetScore(0));
            Assert.IsTrue(_state.TryMove(1, 6), "Play goes on with the dice left.");
        }

        [Test]
        public void BowmanChecker_Volley_RejectsAWrongChoice_AndKeepsWaiting()
        {
            TestBoard.Place(_state, 0, new BowmanChecker(0));
            DefaultChecker own = TestBoard.PlaceDefault(_state, 5, 0);
            DefaultChecker first = TestBoard.PlaceDefault(_state, 10, 1);
            DefaultChecker second = TestBoard.PlaceDefault(_state, 12, 1);
            ArmoredChecker armored = TestBoard.Place(_state, 14, new ArmoredChecker(1));
            TestBoard.PlaceDefault(_state, 16, 1);
            TestBoard.SetDice(_context, 2, 5);

            Assert.IsTrue(_state.TryMove(0, 2));

            Assert.IsFalse(_state.CanVolleyTarget(armored), "An Armored checker cannot be captured.");
            Assert.IsFalse(_state.CanVolleyTarget(own));
            Assert.IsFalse(_state.TryFireVolley(new BaseChecker[] { first }), "Two arrows need two targets.");
            Assert.IsFalse(_state.TryFireVolley(new BaseChecker[] { first, first }));
            Assert.IsFalse(_state.TryFireVolley(new BaseChecker[] { first, armored }));
            Assert.IsFalse(_state.TryFireVolley(new BaseChecker[] { first, own }));

            Assert.IsTrue(_state.HasPendingVolley);
            Assert.AreEqual(0, _state.GetBarCount(1));
            Assert.IsTrue(_state.TryFireVolley(new BaseChecker[] { first, second }));
        }

        [Test]
        public void BowmanChecker_ShootsOnlyWithTheFirstDieItIsPlayedWith_EachTurn()
        {
            TestBoard.Place(_state, 0, new BowmanChecker(0));
            DefaultChecker target = TestBoard.PlaceDefault(_state, 15, 1);
            for (int i = 0; i < 3; i++)
                TestBoard.PlaceDefault(_state, 16 + i, 1);
            TestBoard.SetDice(_context, 1, 2);

            Assert.IsTrue(_state.TryMove(0, 1));
            Assert.AreEqual(1, _state.PendingVolleySize);
            Assert.IsTrue(_state.TryFireVolley(new BaseChecker[] { target }));

            Assert.IsTrue(_state.TryMove(1, 3));
            Assert.IsFalse(_state.HasPendingVolley, "Its second die of the turn shoots nothing.");

            _state.EndTurn();
            _state.EndTurn();
            TestBoard.SetDice(_context, 2, 4);

            Assert.IsTrue(_state.TryMove(3, 5));
            Assert.AreEqual(2, _state.PendingVolleySize, "A new turn, a new volley.");
        }

        [Test]
        public void BowmanChecker_CompoundMove_ShootsWithItsFirstDie_OnceTheWholeMoveIsDone()
        {
            BowmanChecker bowman = TestBoard.Place(_state, 0, new BowmanChecker(0));
            for (int i = 0; i < 5; i++)
                TestBoard.PlaceDefault(_state, 12 + i, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 8));

            Assert.AreEqual(8, _state.FindNodeIndexOf(bowman), "The volley does not stop the second step.");
            Assert.AreEqual(3, _state.PendingVolleySize);
        }

        [Test]
        public void BowmanChecker_HasNoMoreTargetsThanTheBoardOffers()
        {
            TestBoard.Place(_state, 0, new BowmanChecker(0));
            DefaultChecker only = TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 5, 3);

            Assert.IsTrue(_state.TryMove(0, 5));

            Assert.AreEqual(1, _state.PendingVolleySize);
            Assert.IsTrue(_state.TryFireVolley(new BaseChecker[] { only }));
        }

        [Test]
        public void BowmanChecker_WithNothingToHit_ArmsNoVolley()
        {
            TestBoard.Place(_state, 0, new BowmanChecker(0));
            TestBoard.Place(_state, 12, new ArmoredChecker(1));
            TestBoard.SetDice(_context, 5, 3);

            Assert.IsTrue(_state.TryMove(0, 5));

            Assert.IsFalse(_state.HasPendingVolley);
            Assert.IsTrue(_state.TryMove(5, 8), "Nothing waits, so play goes on.");
        }

        [Test]
        public void BowmanChecker_VolleyNotFiredByTheEndOfTheTurn_IsLost()
        {
            TestBoard.Place(_state, 0, new BowmanChecker(0));
            TestBoard.PlaceDefault(_state, 12, 1);
            TestBoard.SetDice(_context, 3, 5);
            Assert.IsTrue(_state.TryMove(0, 3));

            _state.EndTurn();

            Assert.AreEqual(1, _state.CurrentPlayerId);
            Assert.IsFalse(_state.HasPendingVolley);
        }

        [Test]
        public void BowmanChecker_ArrowsCaptureFromAfar_SoAGunpowderBlowsNobodyUp()
        {
            BowmanChecker bowman = TestBoard.Place(_state, 0, new BowmanChecker(0));
            GunpowderChecker gunpowder = TestBoard.Place(_state, 4, new GunpowderChecker(1));
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));
            Assert.IsTrue(_state.TryFireVolley(new BaseChecker[] { gunpowder }));

            Assert.AreEqual(1, _state.GetBarCount(1));
            Assert.AreEqual(3, _state.FindNodeIndexOf(bowman));
        }

        [Test]
        public void BowmanChecker_VolleyThatReachesTheTarget_EndsTheMatchThere()
        {
            MatchContext context;
            GameState state = TestBoard.CreateWithRules(MatchRules.ForTarget(GameState.CaptureScore, 10, 0), out context);
            TestBoard.Place(state, 0, new BowmanChecker(0));
            DefaultChecker first = TestBoard.PlaceDefault(state, 10, 1);
            DefaultChecker second = TestBoard.PlaceDefault(state, 12, 1);
            TestBoard.SetDice(context, 2, 5);

            Assert.IsTrue(state.TryMove(0, 2));
            Assert.IsTrue(state.TryFireVolley(new BaseChecker[] { first, second }));

            Assert.IsTrue(state.IsMatchOver);
            Assert.AreEqual(1, state.GetBarCount(1), "The arrows after the winning one are lost with the match.");
            Assert.IsFalse(state.HasPendingVolley);
        }

        [Test]
        public void ParasiteChecker_SharesAMadePointWithoutCapturing_UpToTheStackLimit()
        {
            TestBoard.Place(_state, 0, new ParasiteChecker(0));
            for (int i = 0; i < GameState.StackLimit - 1; i++)
                TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(0, _state.GetBarCount(1), "Infiltration captures nothing.");
            Assert.AreEqual(GameState.StackLimit, _state.Nodes[3].CheckerCount);
        }

        [Test]
        public void ParasiteChecker_CannotInfiltrateAFullPoint()
        {
            ParasiteChecker parasite = TestBoard.Place(_state, 0, new ParasiteChecker(0));
            for (int i = 0; i < GameState.StackLimit; i++)
                TestBoard.PlaceDefault(_state, 3, 1);
            List<string> reported = new List<string>();
            _state.AbilityTriggered += (abilityId, checker, nodeIndex) => reported.Add(abilityId);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsFalse(_state.TryMove(0, 3));

            Assert.AreEqual(MoveError.StackFull, _lastRejection);
            Assert.AreEqual(0, _state.FindNodeIndexOf(parasite));
            CollectionAssert.DoesNotContain(reported, AbilityIds.Infiltrate,
                "A landing the stack limit refuses must not report the infiltration.");
        }

        [Test]
        public void ParasiteChecker_CompoundMove_DoesNotPlanOntoAFullPoint()
        {
            TestBoard.Place(_state, 0, new ParasiteChecker(0));
            for (int i = 0; i < GameState.StackLimit; i++)
                TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 2, 1);

            Assert.IsFalse(_state.CanReach(0, 3));
        }

        [Test]
        public void NomadChecker_WithTheDiePointsNeighboursEmpty_AddsThreeToADie()
        {
            NomadChecker nomad = TestBoard.Place(_state, 0, new NomadChecker(0));
            TestBoard.SetDice(_context, 3, 1);

            Assert.IsTrue(_state.TryMove(0, 6)); // The 3 brings it to point 3; points 2 and 4 are empty.

            Assert.AreEqual(6, _state.FindNodeIndexOf(nomad));
            CollectionAssert.AreEqual(new[] { 1 }, _context.CurrentRoll.RemainingMoves);
        }

        [TestCase(4, 0)] // A friendly checker behind the die's point.
        [TestCase(6, 1)] // An enemy checker past it.
        public void NomadChecker_WithACheckerBesideTheDiePoint_GetsNoBonus(int side, int ownerId)
        {
            TestBoard.Place(_state, 2, new NomadChecker(0));
            TestBoard.PlaceDefault(_state, side, ownerId);
            TestBoard.SetDice(_context, 3, 1);

            Assert.IsFalse(_state.CanReach(2, 8), "The 3 brings it to point 5; points 4 and 6 must both be empty.");
        }

        [Test]
        public void NomadChecker_TheNeighboursOfItsOwnPoint_DoNotMatter()
        {
            TestBoard.Place(_state, 2, new NomadChecker(0));
            TestBoard.PlaceDefault(_state, 1, 0);
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 1);

            Assert.IsTrue(_state.TryMove(2, 8));
        }

        [Test]
        public void NomadChecker_WithAOne_ThePointItLeavesIsEmptyBehindIt()
        {
            NomadChecker nomad = TestBoard.Place(_state, 0, new NomadChecker(0));
            TestBoard.SetDice(_context, 1, 2);

            Assert.IsTrue(_state.TryMove(0, 4)); // The 1 brings it to point 1; it has left point 0 by then.

            Assert.AreEqual(4, _state.FindNodeIndexOf(nomad));
            CollectionAssert.AreEqual(new[] { 2 }, _context.CurrentRoll.RemainingMoves);
        }

        [Test]
        public void NomadChecker_WithAOne_TheCheckersItLeavesBehindStillCount()
        {
            NomadChecker nomad = TestBoard.Place(_state, 0, new NomadChecker(0));
            TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 1, 2);

            CollectionAssert.DoesNotContain(GetLegalTargets(nomad), 4);
        }

        [Test]
        public void NomadChecker_EnteringFromTheBar_GetsNoBonus_ButHasItBackOnceIn()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            NomadChecker nomad = TestBoard.Place(_state, 3, new NomadChecker(1));
            TestBoard.SetDice(_context, 3, 5);
            Assert.IsTrue(_state.TryMove(0, 3)); // Player 0 hits the Nomad; it goes to the bar.

            _state.EndTurn();
            TestBoard.SetDice(_context, 3, 1);

            // The 3 enters it on point 21 between empty points, but a captured checker enters as a plain one.
            Assert.IsFalse(_state.CanMove(GameState.BarIndex, GameState.NodeCount - 6));
            Assert.IsTrue(_state.TryMove(GameState.BarIndex, GameState.NodeCount - 3));

            // Once in, the 1 brings it to point 20, between empty points: it goes on to point 17.
            Assert.IsTrue(_state.TryMove(nomad, GameState.NodeCount - 7));
        }

        [Test]
        public void NomadChecker_WhenADieAlsoCoversTheDistance_SpendsTheSmallerDieWithItsBonus()
        {
            TestBoard.Place(_state, 0, new NomadChecker(0));
            TestBoard.SetDice(_context, 6, 3);

            Assert.IsTrue(_state.TryMove(0, 6));

            CollectionAssert.AreEqual(new[] { 6 }, _context.CurrentRoll.RemainingMoves, "The 3 and the bonus pay; the 6 stays.");
        }

        [Test]
        public void NomadChecker_InACompoundMove_PaysEachStepWithItsPlannedDie()
        {
            NomadChecker nomad = TestBoard.Place(_state, 0, new NomadChecker(0));
            TestBoard.SetDice(_context, 5, 2);

            // No single die (nor die + 3) covers 7, so 5 then 2 is planned. The bonus must not spend the 2 on the
            // first step, or the second step would have nothing to pay with.
            Assert.IsTrue(_state.TryMove(0, 7));

            Assert.AreEqual(7, _state.FindNodeIndexOf(nomad));
            Assert.IsFalse(_context.CurrentRoll.HasMovesLeft);
        }

        [Test]
        public void NomadChecker_BearsOffWithItsBonus()
        {
            TestBoard.Place(_state, 20, new NomadChecker(0)); // needs 4
            TestBoard.PlaceDefault(_state, 0, 0); // Keeps player 0 in the match after the bear-off.
            TestBoard.SetDice(_context, 1, 2);

            Assert.IsTrue(_state.TryBearOff(20));

            Assert.AreEqual(1, _state.GetBorneOffCount(0));
            CollectionAssert.AreEqual(new[] { 2 }, _context.CurrentRoll.RemainingMoves, "The 1 and the bonus pay.");
        }

        [Test]
        public void NomadChecker_BearingOff_SkipsADieWhosePointHasACheckerBesideIt()
        {
            TestBoard.Place(_state, 20, new NomadChecker(0)); // needs 4
            TestBoard.PlaceDefault(_state, 22, 0);
            TestBoard.SetDice(_context, 1, 2);

            // The 1 brings it to point 21, next to the checker on 22. The 2 brings it to point 22 itself, whose
            // sides 21 and 23 are empty: 2 + 3 carries it off.
            Assert.IsTrue(_state.TryBearOff(20));

            CollectionAssert.AreEqual(new[] { 1 }, _context.CurrentRoll.RemainingMoves);
        }

        [Test]
        public void NomadChecker_UnderAPlainChecker_IsTheOneThatMoves()
        {
            NomadChecker nomad = TestBoard.Place(_state, 2, new NomadChecker(0));
            DefaultChecker top = TestBoard.PlaceDefault(_state, 2, 0);
            TestBoard.SetDice(_context, 3, 1);

            CollectionAssert.Contains(GetLegalTargets(nomad), 8, "The picked-up Nomad's own targets light up.");
            Assert.IsTrue(_state.TryMove(nomad, 8));

            Assert.AreEqual(8, _state.FindNodeIndexOf(nomad));
            Assert.AreEqual(2, _state.FindNodeIndexOf(top));
        }

        [Test]
        public void NavigatorChecker_MarksEveryPointItLandsOn_WithoutScoring()
        {
            TestBoard.Place(_state, 0, new NavigatorChecker(0));
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsTrue(_state.TryMove(0, 5)); // 3 then 2: the stop on point 3 is a landing too.

            Assert.IsNotNull(_state.Nodes[3].GetMark(0));
            Assert.IsNotNull(_state.Nodes[5].GetMark(0));
            Assert.IsNull(_state.Nodes[0].GetMark(0), "It was dealt there, it did not step there.");
            Assert.IsNull(_state.Nodes[4].GetMark(0), "A point it jumps over is not stepped on.");
            Assert.IsNull(_state.Nodes[5].GetMark(1), "The footprints are its owner's alone.");
            Assert.AreEqual(0, _context.GetScore(0));
        }

        [Test]
        public void NavigatorChecker_AnyOwnCaptureOnAFootprint_ScoresMore()
        {
            NavigatorChecker navigator = TestBoard.Place(_state, 0, new NavigatorChecker(0));
            TestBoard.PlaceDefault(_state, 1, 0);
            TestBoard.SetDice(_context, 3, 2);
            Assert.IsTrue(_state.TryMove(navigator, 3));
            Assert.IsTrue(_state.TryMove(navigator, 5));

            TestBoard.PlaceDefault(_state, 3, 1); // An enemy blot on the footprint the Navigator left.
            TestBoard.SetDice(_context, 2, 6);
            Assert.IsTrue(_state.TryMove(1, 3));

            Assert.AreEqual(FootprintCaptureScore, _context.GetScore(0));
        }

        [Test]
        public void NavigatorChecker_ACaptureAsItLandsOnANewPoint_AlreadyScoresMore()
        {
            TestBoard.Place(_state, 0, new NavigatorChecker(0));
            TestBoard.PlaceDefault(_state, 3, 1);
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsTrue(_state.TryMove(0, 3));

            Assert.AreEqual(FootprintCaptureScore, _context.GetScore(0), "The footprint goes down before the capture scores.");
        }

        [Test]
        public void NavigatorChecker_ABearOffFromAFootprint_ScoresMore()
        {
            NavigatorChecker navigator = TestBoard.Place(_state, 17, new NavigatorChecker(0));
            TestBoard.PlaceDefault(_state, 0, 0); // Keeps player 0 in the match after the bear-off.
            TestBoard.SetDice(_context, 3, 4);

            Assert.IsTrue(_state.TryMove(navigator, 20));
            Assert.IsTrue(_state.TryBearOff(20)); // needs 4

            Assert.AreEqual(FootprintBearOffScore, _context.GetScore(0));
        }

        [Test]
        public void NavigatorChecker_FootprintsOutlastTheNavigator_ButNotForTheOpponent()
        {
            NavigatorChecker navigator = TestBoard.Place(_state, 0, new NavigatorChecker(0));
            TestBoard.SetDice(_context, 3, 2);
            Assert.IsTrue(_state.TryMove(navigator, 3));

            TestBoard.PlaceDefault(_state, 6, 1);
            _state.EndTurn();
            TestBoard.SetDice(_context, 3, 2);
            Assert.IsTrue(_state.TryMove(6, 3)); // Player 1 captures the Navigator on its own footprint.

            Assert.AreEqual(GameState.BarIndex, _state.FindNodeIndexOf(navigator));
            Assert.AreEqual(GameState.CaptureScore, _context.GetScore(1), "The opponent scores as usual there.");
            Assert.IsNotNull(_state.Nodes[3].GetMark(0), "The footprint stays after the Navigator has gone.");
        }

        [Test]
        public void NavigatorChecker_UnderAPlainChecker_IsTheOneThatMoves()
        {
            NavigatorChecker navigator = TestBoard.Place(_state, 0, new NavigatorChecker(0));
            DefaultChecker top = TestBoard.PlaceDefault(_state, 0, 0);
            TestBoard.SetDice(_context, 3, 2);

            Assert.IsTrue(_state.TryMove(navigator, 3));

            Assert.AreEqual(3, _state.FindNodeIndexOf(navigator));
            Assert.AreEqual(0, _state.FindNodeIndexOf(top));
            Assert.IsNotNull(_state.Nodes[3].GetMark(0));
        }

        [Test]
        public void BanditChecker_EarnsAGoldForEveryEnemyThatPassesOverIt()
        {
            TestBoard.Place(_state, 5, new BanditChecker(1));
            TestBoard.PlaceDefault(_state, 2, 0);
            TestBoard.PlaceDefault(_state, 3, 0);
            TestBoard.SetDice(_context, 6, 4);

            Assert.IsTrue(_state.TryMove(2, 8));
            Assert.IsTrue(_state.TryMove(3, 7));

            Assert.AreEqual(2, _context.GetGold(1));
        }

        [Test]
        public void BanditChecker_TakesNothingFromItsOwnSide()
        {
            TestBoard.Place(_state, 5, new BanditChecker(0));
            TestBoard.PlaceDefault(_state, 2, 0);
            TestBoard.SetDice(_context, 6, 4);

            Assert.IsTrue(_state.TryMove(2, 8));

            Assert.AreEqual(0, _context.GetGold(0));
        }

        [Test]
        public void TimeBenderChecker_OnALuckyRoll_SkipsTheOpponentsNextTurn()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new LuckyRandom(), out context);
            TestBoard.Place(state, 0, new TimeBenderChecker(0));
            TestBoard.PlaceDefault(state, 20, 1);
            TestBoard.SetDice(context, 3, 5);

            Assert.IsTrue(state.TryMove(0, 3));
            state.EndTurn();
            Assert.AreEqual(0, state.CurrentPlayerId, "Player 1's turn was skipped.");

            state.EndTurn();
            Assert.AreEqual(1, state.CurrentPlayerId, "Only one turn is skipped.");
        }

        [Test]
        public void TimeBenderChecker_OnAnUnluckyRoll_LeavesTheTurnOrderAlone()
        {
            TestBoard.Place(_state, 0, new TimeBenderChecker(0));
            TestBoard.PlaceDefault(_state, 20, 1);
            TestBoard.SetDice(_context, 3, 5);

            Assert.IsTrue(_state.TryMove(0, 3));
            _state.EndTurn();

            Assert.AreEqual(1, _state.CurrentPlayerId);
        }

        private List<int> GetLegalTargets(BaseChecker checker)
        {
            List<int> targets = new List<int>();
            _state.GetLegalTargets(checker, targets);
            return targets;
        }

        [Test]
        public void Bot_ChoosesALegalMoveWithoutTouchingTheBoard()
        {
            TestBoard.SetupFullBoard(_state);
            TestBoard.SetDice(_context, 3, 1);
            TavlaBot bot = new TavlaBot(0);
            int[] before = TestBoard.CountPerPoint(_state, 0);

            BotMove move = bot.ChooseBestMove(_state);

            Assert.IsTrue(move.IsValid);
            Assert.IsTrue(_state.CanReach(move.FromIndex, move.ToIndex));
            Assert.AreEqual(2, _context.CurrentRoll.RemainingMoves.Count, "Evaluation must not spend real dice.");
            CollectionAssert.AreEqual(before, TestBoard.CountPerPoint(_state, 0), "Evaluation must not move real checkers.");
        }

        [Test]
        public void Bot_DoesNothingWhenItIsNotItsTurn()
        {
            TestBoard.SetupFullBoard(_state);
            TestBoard.SetDice(_context, 3, 1);

            Assert.IsFalse(new TavlaBot(1).ChooseBestMove(_state).IsValid);
        }

        [Test]
        public void Bot_DoesNotKnowTheOpponentsSpecialCheckers()
        {
            TestBoard.PlaceDefault(_state, 0, 0);
            GunpowderChecker gunpowder = TestBoard.Place(_state, 3, new GunpowderChecker(1));
            TestBoard.PlaceDefault(_state, 8, 1);
            TestBoard.PlaceDefault(_state, 8, 1); // Closes the 3+5 move: hit the Gunpowder or step to point 5.
            TestBoard.SetDice(_context, 3, 5);
            TavlaBot bot = new TavlaBot(0);

            BotMove move = bot.ChooseBestMove(_state);

            Assert.AreEqual(3, move.ToIndex, "To the bot a Gunpowder blot is just a blot worth hitting.");
            Assert.AreSame(gunpowder, _state.Nodes[3].Checkers[0], "The bot's plain stand-ins never reach the real board.");

            Assert.IsTrue(bot.PlayOneMove(_state));
            Assert.AreEqual(1, _state.GetBarCount(0), "Not knowing does not spare it: the capturer is blown up.");
        }

        [Test]
        public void Bot_FiresItsVolleyFirst_AtTheCheckersClosestToBearingOff()
        {
            TestBoard.Place(_state, 20, new BowmanChecker(1));
            DefaultChecker far = TestBoard.PlaceDefault(_state, 2, 0);
            DefaultChecker middle = TestBoard.PlaceDefault(_state, 10, 0);
            DefaultChecker near = TestBoard.PlaceDefault(_state, 21, 0);
            DefaultChecker nearest = TestBoard.PlaceDefault(_state, 22, 0);
            _state.EndTurn();
            TestBoard.SetDice(_context, 3, 5);
            Assert.IsTrue(_state.TryMove(20, 17));
            TavlaBot bot = new TavlaBot(1);

            CollectionAssert.AreEqual(new BaseChecker[] { nearest, near, middle }, bot.ChooseVolleyTargets(_state));

            Assert.IsTrue(bot.PlayOneMove(_state));
            Assert.IsFalse(_state.HasPendingVolley);
            Assert.AreEqual(3, _state.GetBarCount(0));
            Assert.AreEqual(2, _state.FindNodeIndexOf(far));
        }
    }
}
