using System;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>Score, economy and dice rules of <see cref="MatchContext"/>.</summary>
    public class MatchContextTests
    {
        private MatchContext _context;

        [SetUp]
        public void SetUp()
        {
            TestBoard.CreateEmpty(out _context);
        }

        [Test]
        public void AddScore_AddsToTheTotalAndReturnsTheAmountWritten()
        {
            Assert.AreEqual(10, _context.AddScore(0, 10));
            Assert.AreEqual(5, _context.AddScore(0, 5));

            Assert.AreEqual(15, _context.GetScore(0));
            Assert.AreEqual(0, _context.GetScore(1), "The other player's score is untouched.");
        }

        [Test]
        public void AddGold_DebtIsConvertedIntoAScorePenalty()
        {
            _context.AddGold(0, 2);
            _context.AddGold(0, -5);

            Assert.AreEqual(0, _context.GetGold(0));
            Assert.AreEqual(-3 * MatchContext.ScorePenaltyPerMissingGold, _context.GetScore(0));
        }

        [Test]
        public void PinNextDie_IsAppliedToTheNextRollOnly()
        {
            _context.PinNextDie(0, 4);

            Assert.AreEqual(4, _context.RollDice(0).First);
            Assert.AreEqual(1, _context.RollDice(0).First, "FixedRandom rolls 1 once the pin is used up.");
        }

        [Test]
        public void LoadedDice_SettleEveryRollOfTheirPlayerUntilUnloaded()
        {
            LoadedDice dice = new LoadedDice();
            dice.Load(6, 3);
            _context.RegisterDiceModifier(0, dice);

            for (int i = 0; i < 2; i++)
            {
                DiceRoll roll = _context.RollDice(0);
                Assert.AreEqual(6, roll.First);
                Assert.AreEqual(3, roll.Second);
            }

            Assert.AreEqual(1, _context.RollDice(1).First, "The other player's dice are not loaded.");

            dice.Unload();
            Assert.AreEqual(1, _context.RollDice(0).First, "FixedRandom rolls 1 once the dice are unloaded.");
        }

        [Test]
        public void LoadedDice_OnADouble_GrantFourMoves()
        {
            LoadedDice dice = new LoadedDice();
            dice.Load(5, 5);
            _context.RegisterDiceModifier(0, dice);

            Assert.AreEqual(4, _context.RollDice(0).RemainingMoves.Count);
        }

        [Test]
        public void LoadedDice_ThePinnedDieStillApplies()
        {
            LoadedDice dice = new LoadedDice();
            dice.Load(6, 6);
            _context.RegisterDiceModifier(0, dice);
            _context.PinNextDie(0, 2);

            DiceRoll roll = _context.RollDice(0);

            Assert.AreEqual(2, roll.First, "The Anchor's pin settles a die after the relics, loaded dice included.");
            Assert.AreEqual(6, roll.Second);
        }

        [Test]
        public void LoadedDice_RejectFacesOffTheDie()
        {
            LoadedDice dice = new LoadedDice();

            Assert.Throws<ArgumentOutOfRangeException>(() => dice.Load(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => dice.Load(2, GameState.MaxDieValue + 1));
            Assert.IsFalse(dice.IsLoaded);
        }

        [Test]
        public void Double_GrantsFourMoves()
        {
            DiceRoll roll = new DiceRoll();
            roll.Set(4, 4);

            Assert.IsTrue(roll.IsDouble);
            Assert.AreEqual(4, roll.RemainingMoves.Count);
        }

        [Test]
        public void SeededRandom_IsReproducible()
        {
            SystemRandomProvider a = new SystemRandomProvider(1234);
            SystemRandomProvider b = new SystemRandomProvider(1234);

            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.Range(1, GameState.MaxDieValue + 1), b.Range(1, GameState.MaxDieValue + 1));
        }
    }
}
