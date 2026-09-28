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
        public void AddScore_AppliesMultipliersAndRounds()
        {
            _context.AddScoreMultiplier(0, 1.5f, false);
            _context.AddScoreMultiplier(0, 1.5f, true);

            // 10 x 2.25 = 22.5 -> rounded away from zero.
            Assert.AreEqual(23, _context.AddScore(0, 10));
            Assert.AreEqual(23, _context.GetScore(0));
        }

        [Test]
        public void RollDice_ResetsTheTurnMultiplierButKeepsThePermanentOne()
        {
            _context.AddScoreMultiplier(0, 2f, false);
            _context.AddScoreMultiplier(0, 3f, true);

            _context.RollDice(0);

            Assert.AreEqual(30, _context.AddScore(0, 10));
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
        public void ResetTurnScore_RemovesOnlyThisTurnsScore()
        {
            _context.AddScore(0, 10);
            _context.RollDice(0); // New turn.
            _context.AddScore(0, 5);

            _context.ResetTurnScore(0);

            Assert.AreEqual(10, _context.GetScore(0));
        }

        [Test]
        public void PinNextDie_IsAppliedToTheNextRollOnly()
        {
            _context.PinNextDie(0, 4);

            Assert.AreEqual(4, _context.RollDice(0).First);
            Assert.AreEqual(1, _context.RollDice(0).First, "FixedRandom rolls 1 once the pin is used up.");
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
