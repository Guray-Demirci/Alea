using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Relics;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>The dice relics: charms that shape every roll and cards that act on the roll they are armed for.</summary>
    public class RelicTests
    {
        [Test]
        public void TwinWinds_OnALuckyRoll_TurnsDiceOneApartIntoADoubleOfTheHigherDie()
        {
            DiceRoll roll = new DiceRoll();
            roll.Set(4, 5);

            new TwinWindsRelic().ModifyRoll(roll, new LuckyRandom());

            Assert.AreEqual(5, roll.First);
            Assert.AreEqual(5, roll.Second);
            Assert.AreEqual(4, roll.RemainingMoves.Count, "A double grants four moves.");
        }

        [Test]
        public void TwinWinds_OnAnUnluckyRoll_LeavesTheDiceAlone()
        {
            DiceRoll roll = new DiceRoll();
            roll.Set(4, 5);

            new TwinWindsRelic().ModifyRoll(roll, new FixedRandom());

            Assert.IsFalse(roll.IsDouble);
        }

        [Test]
        public void TwinWinds_IgnoresDiceThatAreNotOneApart()
        {
            DiceRoll roll = new DiceRoll();
            roll.Set(2, 5);

            new TwinWindsRelic().ModifyRoll(roll, new LuckyRandom());

            Assert.AreEqual(2, roll.First);
            Assert.AreEqual(5, roll.Second);
        }

        [Test]
        public void StormReport_EngravesTheChosenNumberOverOneErasedFace()
        {
            List<int> faces = new List<int> { 1, 2, 3, 4, 5, 6 };

            new StormReportRelic(1, 6).ModifyFaces(faces);

            CollectionAssert.AreEqual(new[] { 6, 2, 3, 4, 5, 6 }, faces);
        }

        [Test]
        public void StormReport_WithoutTheErasedFaceInThePool_ChangesNothing()
        {
            List<int> faces = new List<int> { 6, 2, 3, 4, 5, 6 };

            new StormReportRelic(1, 5).ModifyFaces(faces);

            CollectionAssert.AreEqual(new[] { 6, 2, 3, 4, 5, 6 }, faces);
        }

        [Test]
        public void StormReport_ShapesOnlyItsOwnersDice()
        {
            MatchContext context;
            TestBoard.CreateEmpty(out context); // FixedRandom always lands on the first face of the pool.
            context.RegisterDiceModifier(0, new StormReportRelic(1, 6));

            Assert.AreEqual(6, context.RollDice(0).First, "The 1 was engraved as a 6.");
            Assert.AreEqual(1, context.RollDice(1).First, "The other player's dice keep their faces.");
        }

        [Test]
        public void CartographersCheat_ArmedCard_TurnsTheNextRollIntoAPool()
        {
            MatchContext context;
            TestBoard.CreateEmpty(new SteppingRandom(), out context); // The first roll is 1-2, the next 3-4.
            CartographersCheatRelic card = new CartographersCheatRelic();
            context.RegisterDiceModifier(0, card);

            Assert.IsTrue(card.TryActivate(context, 0));
            DiceRoll roll = context.RollDice(0);

            Assert.IsTrue(roll.FreeSplit);
            Assert.AreEqual(3, roll.FreeTotal);
            Assert.IsTrue(card.IsDepleted, "A single-use card spends its charge when it is armed.");
            Assert.IsFalse(context.RollDice(0).FreeSplit, "It acts on one roll only.");
        }

        [Test]
        public void CartographersCheat_ThePoolIsSplitAcrossCheckers()
        {
            MatchContext context;
            GameState state = TestBoard.CreateEmpty(new SteppingRandom(), out context); // Rolls 1-2.
            TestBoard.PlaceDefault(state, 0, 0);
            TestBoard.PlaceDefault(state, 10, 0);
            CartographersCheatRelic card = new CartographersCheatRelic();
            context.RegisterDiceModifier(0, card);
            card.TryActivate(context, 0);
            context.RollDice(0);

            Assert.IsTrue(state.TryMove(10, 12), "Two points of the pool of three.");
            Assert.IsTrue(state.TryMove(0, 1), "The last point.");
            Assert.IsFalse(context.CurrentRoll.HasMovesLeft);
        }
    }
}
