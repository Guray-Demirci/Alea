using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Tiles;
using UnityEditor;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>
    /// Points turning into tiles and back, played frame by frame on the real art. The Powder Keg's transitions have
    /// 6 frames between the plain point and the tile, so a change is over after 6 frame times. Times are counted in
    /// frames, so the tests hold whatever speed the art plays at.
    /// </summary>
    public class TilesViewTests
    {
        private const string Keg = PowderKegTile.Id;
        private const int LightPoint = 0;
        private const int DarkPoint = 1;

        // A whole change and half a frame more, so frame rounding never decides a test.
        private float Change => Frames(6.5f);

        private TileArtSet _art;
        private BoardLayout _layout;
        private Sprite _solid;
        private TilesView _view;

        private Sprite KegPoint => _art.GetSprite(Keg);

        [SetUp]
        public void SetUp()
        {
            _art = AssetDatabase.LoadAssetAtPath<TileArtSet>(DataAssetTests.TileArtPath);
            _layout = BoardLayout.CreateDefault();
            _solid = BoardSceneBuilder.CreateSolidSprite();
            _view = TilesView.Create(_art, _solid, _layout);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_view.gameObject);
            Object.DestroyImmediate(_solid.texture);
            Object.DestroyImmediate(_solid);
            Object.DestroyImmediate(_layout);
        }

        [Test]
        public void EveryPoint_StartsPlain_OverItsTriangle()
        {
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Assert.IsNull(RendererOf(i).sprite, $"Point {i}.");
                Assert.AreEqual(_layout.GetNodeCenter(i), RendererOf(i).transform.position, $"Point {i}.");
            }
        }

        [Test]
        public void TheTopRow_ShowsTheArtTurnedHalfRound()
        {
            Assert.IsTrue(RendererOf(5).flipX && RendererOf(5).flipY, "Top row.");
            Assert.IsFalse(RendererOf(17).flipX || RendererOf(17).flipY, "Bottom row: the art is drawn for it.");
        }

        [Test]
        public void ALightPoint_TurnsIntoTheTile_FromLight()
        {
            IReadOnlyList<Sprite> transition = _art.GetTransition(Keg, true);

            _view.Show(LightPoint, Keg);
            Assert.AreSame(transition[0], SpriteOf(LightPoint), "The change starts at once.");

            _view.Tick(Frames(3.5f));
            Assert.AreSame(transition[3], SpriteOf(LightPoint));

            _view.Tick(Frames(3f));
            Assert.AreSame(KegPoint, SpriteOf(LightPoint));
        }

        [Test]
        public void ADarkPoint_TurnsIntoTheTile_FromDark()
        {
            _view.Show(DarkPoint, Keg);
            Assert.AreSame(_art.GetTransition(Keg, false)[0], SpriteOf(DarkPoint));

            _view.Tick(Change);
            Assert.AreSame(KegPoint, SpriteOf(DarkPoint));
        }

        [Test]
        public void ATileThatLeaves_PlaysItsTransitionBackwards()
        {
            ShowAndSettle(DarkPoint, Keg);
            IReadOnlyList<Sprite> transition = _art.GetTransition(Keg, false);

            _view.Show(DarkPoint, null);
            Assert.AreSame(transition[transition.Count - 1], SpriteOf(DarkPoint));

            _view.Tick(Change);
            Assert.IsNull(SpriteOf(DarkPoint), "The plain point again.");
        }

        [Test]
        public void ShowingTheSameTileAgain_DoesNotReplayIt()
        {
            ShowAndSettle(LightPoint, Keg);

            _view.Show(LightPoint, Keg);
            Assert.AreSame(KegPoint, SpriteOf(LightPoint));
        }

        [Test]
        public void ATileWithoutArt_LeavesThePointPlain()
        {
            _view.Show(LightPoint, SwampTile.Id);
            Assert.IsNull(SpriteOf(LightPoint));

            // From a tile without art to one with art: only the arriving tile has frames to play.
            _view.Show(LightPoint, Keg);
            Assert.AreSame(_art.GetTransition(Keg, true)[0], SpriteOf(LightPoint));
        }

        [Test]
        public void AChangeUnderWay_IsCutShort_ByTheNextOne()
        {
            _view.Show(LightPoint, Keg);
            _view.Tick(Frames(2.5f));

            _view.Show(LightPoint, null);
            IReadOnlyList<Sprite> transition = _art.GetTransition(Keg, true);
            Assert.AreSame(transition[transition.Count - 1], SpriteOf(LightPoint), "Leaves from the tile at rest.");

            _view.Tick(Change);
            Assert.IsNull(SpriteOf(LightPoint));
        }

        [Test]
        public void OnlyThePointAskedFor_Changes()
        {
            _view.Show(7, Keg);
            _view.Tick(Change);

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                if (i != 7)
                    Assert.IsNull(SpriteOf(i), $"Point {i}.");
            }
        }

        [Test]
        public void APointOffTheBoard_IsRefused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _view.Show(GameState.NodeCount, Keg));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _view.ShowCount(-1, 3));
        }

        [Test]
        public void TheCount_IsHiddenUntilShown_ThenShowsTheNumberNearTheTip()
        {
            for (int i = 0; i < GameState.NodeCount; i++)
                Assert.IsFalse(CountOf(i).gameObject.activeSelf, $"Point {i}.");

            _view.ShowCount(LightPoint, 2);
            Assert.IsTrue(CountOf(LightPoint).gameObject.activeSelf);
            Assert.AreEqual("2", CountOf(LightPoint).GetComponentInChildren<PixelLabel>(true).Text);
            Assert.AreEqual(_layout.GetTileCountPosition(LightPoint), CountOf(LightPoint).position);

            _view.ShowCount(LightPoint, TilesView.NoCount);
            Assert.IsFalse(CountOf(LightPoint).gameObject.activeSelf);
        }

        private float Frames(float count) => count * _art.FrameSeconds;

        private Transform CountOf(int point) => _view.transform.GetChild(point).Find("Count");

        private void ShowAndSettle(int point, string effectId)
        {
            _view.Show(point, effectId);
            _view.Tick(Change);
        }

        private Sprite SpriteOf(int point) => RendererOf(point).sprite;

        private SpriteRenderer RendererOf(int point)
        {
            return _view.transform.GetChild(point).GetComponent<SpriteRenderer>();
        }
    }
}
