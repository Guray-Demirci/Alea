using System;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.View.Tests
{
    /// <summary>Which dice card shows which face: one card per die face, a double on the middle four.</summary>
    public class DiceCardFacesTests
    {
        private readonly int[] _faces = new int[DiceCardFaces.CardCount];

        [Test]
        public void BeforeTheFirstRoll_EveryCardIsFaceDown()
        {
            DiceCardFaces.Compute((DiceRoll)null, _faces);
            Assert.AreEqual(new[] { 0, 0, 0, 0, 0, 0 }, _faces);
        }

        [Test]
        public void EachDieInHand_TurnsUpItsOwnCard()
        {
            DiceCardFaces.Compute(false, new[] { 5, 2 }, _faces);
            Assert.AreEqual(new[] { 0, 2, 0, 0, 5, 0 }, _faces);
        }

        [Test]
        public void ASpentDie_TurnsItsCardBackDown()
        {
            DiceCardFaces.Compute(false, new[] { 5 }, _faces);
            Assert.AreEqual(new[] { 0, 0, 0, 0, 5, 0 }, _faces);
        }

        [Test]
        public void ADouble_TurnsUpTheMiddleFour()
        {
            DiceCardFaces.Compute(true, new[] { 3, 3, 3, 3 }, _faces);
            Assert.AreEqual(new[] { 0, 3, 3, 3, 3, 0 }, _faces);
        }

        [Test]
        public void ADouble_TurnsItsCardsDownFromTheRight_AsTheMovesAreSpent()
        {
            DiceCardFaces.Compute(true, new[] { 6, 6 }, _faces);
            Assert.AreEqual(new[] { 0, 6, 6, 0, 0, 0 }, _faces);
        }

        [Test]
        public void ExtraMovesOnADouble_StillShowFourCards()
        {
            DiceCardFaces.Compute(true, new[] { 2, 2, 2, 2, 2 }, _faces);
            Assert.AreEqual(new[] { 0, 2, 2, 2, 2, 0 }, _faces);
        }

        [Test]
        public void ARerolledDouble_ShowsEachDieOnItsOwnCard()
        {
            // Dice Holder rerolled the three moves left of a 6-6.
            DiceCardFaces.Compute(true, new[] { 2, 5, 5 }, _faces);
            Assert.AreEqual(new[] { 0, 2, 0, 0, 5, 0 }, _faces);
        }

        [Test]
        public void ARepeatedValueWithoutADouble_ShowsOneCard()
        {
            DiceCardFaces.Compute(false, new[] { 4, 4 }, _faces);
            Assert.AreEqual(new[] { 0, 0, 0, 4, 0, 0 }, _faces);
        }

        [Test]
        public void NoMovesLeft_EveryCardIsFaceDown()
        {
            DiceCardFaces.Compute(true, new int[0], _faces);
            Assert.AreEqual(new[] { 0, 0, 0, 0, 0, 0 }, _faces);
        }

        [Test]
        public void ValuesThatAreNoDieFace_AreIgnored()
        {
            DiceCardFaces.Compute(false, new[] { 0, 7 }, _faces);
            Assert.AreEqual(new[] { 0, 0, 0, 0, 0, 0 }, _faces);
        }

        [Test]
        public void EveryCardIsSetAgain_NothingIsLeftFromTheLastCall()
        {
            DiceCardFaces.Compute(false, new[] { 1, 6 }, _faces);
            DiceCardFaces.Compute(false, new[] { 3 }, _faces);
            Assert.AreEqual(new[] { 0, 0, 3, 0, 0, 0 }, _faces);
        }

        [Test]
        public void TheFaces_MustHaveOneEntryPerCard()
        {
            Assert.Throws<ArgumentException>(() => DiceCardFaces.Compute(false, new[] { 1 }, new int[5]));
            Assert.Throws<ArgumentException>(() => DiceCardFaces.Compute(false, new[] { 1 }, null));
        }

        [Test]
        public void ARealDoubleRoll_TurnsUpTheMiddleFour()
        {
            GameState board = new GameState();
            MatchContext context = new MatchContext(board, new SystemRandomProvider(1));

            LoadedDice dice = new LoadedDice();
            dice.Load(4, 4);
            context.RegisterDiceModifier(0, dice);

            DiceCardFaces.Compute(context.RollDice(0), _faces);
            Assert.AreEqual(new[] { 0, 4, 4, 4, 4, 0 }, _faces);
        }
    }
}
