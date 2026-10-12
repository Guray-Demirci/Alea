using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>
    /// The dice cards turning over, played frame by frame on the real art. Every flip is 6 frames, so a turn shows the
    /// 5 frames after the one it starts from and is over after 4 frame times. Times are counted in frames, so the
    /// tests hold whatever speed the art plays at.
    /// </summary>
    public class DiceCardsViewTests
    {
        // A whole turn and half a frame more, so frame rounding never decides a test.
        private float Turn => Frames(4.5f);

        private DiceCardArtSet _art;
        private BoardLayout _layout;
        private DiceCardsView _view;

        private Sprite Back => _art.GetSprite(DiceCardFaces.FaceDown);

        [SetUp]
        public void SetUp()
        {
            _art = AssetDatabase.LoadAssetAtPath<DiceCardArtSet>(DataAssetTests.DiceCardArtPath);
            _layout = BoardLayout.CreateDefault();
            _view = DiceCardsView.Create(_art, _layout);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_view.gameObject);
            Object.DestroyImmediate(_layout);
        }

        [Test]
        public void TheCards_StartFaceDown_InTheirSlots()
        {
            for (int i = 0; i < DiceCardFaces.CardCount; i++)
            {
                Assert.AreSame(Back, SpriteOf(i), $"Card {i + 1}.");
                Assert.AreEqual(_layout.GetDiceCardPosition(i), _view.transform.GetChild(i).position, $"Card {i + 1}.");
            }
        }

        [Test]
        public void ADeal_PlaysTheFlipForwards_OnTheRolledCards()
        {
            _view.Deal(new[] { 0, 2, 0, 0, 5, 0 });
            Assert.AreSame(_art.GetFlip(2)[1], SpriteOf(1), "The flip starts at once.");

            _view.Tick(Frames(2.5f));
            Assert.AreSame(_art.GetFlip(2)[3], SpriteOf(1));

            _view.Tick(Frames(2f));
            AssertFaces(0, 2, 0, 0, 5, 0);
        }

        [Test]
        public void ASpentDie_PlaysTheFlipBackwards()
        {
            DealAndSettle(0, 2, 0, 0, 5, 0);

            _view.Show(new[] { 0, 0, 0, 0, 5, 0 });
            Assert.AreSame(_art.GetFlip(2)[4], SpriteOf(1));

            _view.Tick(Turn);
            AssertFaces(0, 0, 0, 0, 5, 0);
        }

        [Test]
        public void ANewRoll_TurnsTheLeftoverCardsDownFirst()
        {
            DealAndSettle(0, 0, 0, 0, 5, 0);

            _view.Deal(new[] { 0, 3, 3, 3, 3, 0 });
            Assert.AreSame(_art.GetFlip(5)[4], SpriteOf(4), "The leftover 5 turns down...");
            Assert.AreSame(Back, SpriteOf(1), "...while the new faces wait.");

            _view.Tick(Turn);
            for (int i = 1; i <= 4; i++)
                Assert.AreSame(_art.GetFlip(3)[1], SpriteOf(i), $"Card {i + 1} starts turning up.");

            _view.Tick(Turn);
            AssertFaces(0, 3, 3, 3, 3, 0);
        }

        [Test]
        public void OneFaceToAnother_GoesThroughTheBack()
        {
            // A 6-6 whose moves Dice Holder rerolled to 2-5.
            DealAndSettle(0, 6, 6, 6, 6, 0);

            _view.Show(new[] { 0, 2, 0, 0, 5, 0 });
            _view.Tick(Turn);
            Assert.AreSame(_art.GetFlip(2)[1], SpriteOf(1), "Down, then up again as the 2.");
            Assert.AreSame(Back, SpriteOf(2));

            _view.Tick(Turn);
            AssertFaces(0, 2, 0, 0, 5, 0);
        }

        [Test]
        public void ATurnUnderWay_FinishesBeforeTheNextOne()
        {
            _view.Show(new[] { 0, 0, 4, 0, 0, 0 });
            _view.Tick(Frames(1.5f));

            _view.Show(new[] { 0, 0, 0, 0, 0, 0 });
            _view.Tick(Frames(3f));
            Assert.AreSame(_art.GetFlip(4)[4], SpriteOf(2), "Turned all the way up, now on its way down.");

            _view.Tick(Turn);
            AssertFaces(0, 0, 0, 0, 0, 0);
        }

        [Test]
        public void FacesShownDuringADeal_AreDealtInstead()
        {
            DealAndSettle(0, 0, 0, 0, 5, 0);

            _view.Deal(new[] { 1, 0, 0, 0, 0, 0 });
            _view.Show(new[] { 0, 0, 0, 0, 0, 6 });
            _view.Tick(Turn);
            _view.Tick(Turn);

            AssertFaces(0, 0, 0, 0, 0, 6);
        }

        [Test]
        public void LayingFaceDown_StopsEveryTurn_AtOnce()
        {
            _view.Deal(new[] { 0, 2, 0, 0, 5, 0 });
            _view.Tick(Frames(1.5f));

            _view.LayFaceDown();
            AssertFaces(0, 0, 0, 0, 0, 0);

            _view.Tick(Turn);
            AssertFaces(0, 0, 0, 0, 0, 0);
        }

        private float Frames(float count) => count * _art.FrameSeconds;

        private void DealAndSettle(params int[] faces)
        {
            _view.Deal(faces);
            _view.Tick(Turn);
        }

        private void AssertFaces(params int[] faces)
        {
            for (int i = 0; i < faces.Length; i++)
                Assert.AreSame(_art.GetSprite(faces[i]), SpriteOf(i), $"Card {i + 1}.");
        }

        private Sprite SpriteOf(int card)
        {
            return _view.transform.GetChild(card).GetComponent<SpriteRenderer>().sprite;
        }
    }
}
