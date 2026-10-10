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
