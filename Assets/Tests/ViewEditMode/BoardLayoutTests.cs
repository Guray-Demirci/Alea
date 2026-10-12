using NUnit.Framework;
using Tavla.Core.Board;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>The board geometry: where every point and every checker is drawn.</summary>
    public class BoardLayoutTests
    {
        private BoardLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = BoardLayout.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_layout);
        }

        [Test]
        public void PointIndices_RunAlongTheTopRow_ThenBackAlongTheBottomRow()
        {
            Assert.IsTrue(_layout.IsTopRow(0));
            Assert.IsTrue(_layout.IsTopRow(11));
            Assert.IsFalse(_layout.IsTopRow(12));

            Assert.AreEqual(0, _layout.GetColumn(0));
            Assert.AreEqual(11, _layout.GetColumn(11));
            Assert.AreEqual(11, _layout.GetColumn(12));
            Assert.AreEqual(0, _layout.GetColumn(23));
        }

        [Test]
        public void IsLightPoint_MatchesTheTrianglesOfPointsPng()
        {
            Texture2D points = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(points.LoadImage(System.IO.File.ReadAllBytes("Assets/Sprites/points.png")));

                for (int i = 0; i < GameState.NodeCount; i++)
                {
                    // 18 px from the centre toward the base, where the triangle is wide: the light ones are beige
                    // (#C5A37D), the dark ones dark red (#834848).
                    Vector3 centre = _layout.GetNodeCenter(i);
                    int x = Mathf.RoundToInt(centre.x * _layout.PixelsPerUnit + 160f);
                    int row = Mathf.RoundToInt(90f - centre.y * _layout.PixelsPerUnit) + (_layout.IsTopRow(i) ? -18 : 18);

                    // Texture rows run bottom-up.
                    bool light = points.GetPixel(x, points.height - 1 - row).grayscale > 0.5f;
                    Assert.AreEqual(light, _layout.IsLightPoint(i), $"Point {i}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(points);
            }
        }

        [Test]
        public void TheTileCountBadge_SitsInsideItsTriangle_ClearOfFourStackedCheckers()
        {
            float halfBadge = _layout.GetTileCountSize().y * 0.5f;
            float halfHeight = _layout.GetNodeSize().y * 0.5f;
            float halfChecker = _layout.CheckerWorldSize * 0.5f;

            for (int point = 0; point < GameState.NodeCount; point++)
            {
                Vector3 badge = _layout.GetTileCountPosition(point);
                Vector3 centre = _layout.GetNodeCenter(point);
                Vector3 fourth = _layout.GetCheckerPosition(point, 3, 4);

                Assert.AreEqual(centre.x, badge.x, 1e-4f, $"Point {point}: on its column.");
                Assert.LessOrEqual(Mathf.Abs(badge.y - centre.y) + halfBadge, halfHeight + 1e-4f, $"Point {point}.");
                Assert.GreaterOrEqual(Mathf.Abs(badge.y - fourth.y), halfBadge + halfChecker - 1e-4f, $"Point {point}.");
            }
        }

        [Test]
        public void EveryPoint_HasACentreOfItsOwn()
        {
            for (int a = 0; a < GameState.NodeCount; a++)
            {
                for (int b = a + 1; b < GameState.NodeCount; b++)
                    Assert.AreNotEqual(_layout.GetNodeCenter(a), _layout.GetNodeCenter(b), $"Points {a} and {b}.");
            }
        }

        [Test]
        public void TheCentreOfTheCanvas_IsTheWorldOrigin()
        {
            Assert.AreEqual(Vector3.zero, _layout.PixelToWorld(160f, 90f));
        }

        // The user's layout: 17 px cards, 14 px apart, at x 92, 123, 154 left of the bar and 189, 220, 251 right of it.
        [TestCase(0, 100.5f)]
        [TestCase(1, 131.5f)]
        [TestCase(2, 162.5f)]
        [TestCase(3, 197.5f)]
        [TestCase(4, 228.5f)]
        [TestCase(5, 259.5f)]
        public void DiceCards_SpreadAcrossTheMiddleOfTheBoard(int slot, float centreX)
        {
            Assert.AreEqual(_layout.PixelToWorld(centreX, 90f), _layout.GetDiceCardPosition(slot));
        }

        [Test]
        public void DiceCards_LeaveTheBarFree_ForTheHitCheckers()
        {
            const float halfCard = 8.5f / 12f;
            float barX = _layout.GetBarPosition(0, 0, 1).x;
            float clearance = halfCard + _layout.CheckerWorldSize * 0.5f;

            Assert.GreaterOrEqual(barX - _layout.GetDiceCardPosition(2).x, clearance);
            Assert.GreaterOrEqual(_layout.GetDiceCardPosition(3).x - barX, clearance);
        }

        [Test]
        public void DiceCards_ThereAreSixSlots()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _layout.GetDiceCardPosition(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _layout.GetDiceCardPosition(6));
        }

        [Test]
        public void EveryStack_UpToAWholeSide_StaysInsideItsTriangle()
        {
            float halfHeight = _layout.GetNodeSize().y * 0.5f;
            float halfChecker = _layout.CheckerWorldSize * 0.5f;

            for (int point = 0; point < GameState.NodeCount; point++)
            {
                float centreY = _layout.GetNodeCenter(point).y;

                for (int count = 1; count <= GameState.MaxCheckersPerPlayer; count++)
                {
                    for (int k = 0; k < count; k++)
                    {
                        float offset = Mathf.Abs(_layout.GetCheckerPosition(point, k, count).y - centreY);
                        Assert.LessOrEqual(offset + halfChecker, halfHeight + 1e-4f,
                                           $"Point {point}, checker {k} of {count}.");
                    }
                }
            }
        }
    }
}
