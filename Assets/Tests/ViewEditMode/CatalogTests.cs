using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;

namespace Tavla.View.Tests
{
    /// <summary>The Inspector dropdowns and the Core classes they stand for.</summary>
    public class CatalogTests
    {
        private static CheckerKind[] CheckerKinds => (CheckerKind[])Enum.GetValues(typeof(CheckerKind));

        [Test]
        public void EveryCheckerKind_MakesANewSpecialCheckerWithATypeIdOfItsOwn()
        {
            HashSet<string> typeIds = new HashSet<string>();

            foreach (CheckerKind kind in CheckerKinds)
            {
                BaseChecker first = CheckerCatalog.GetFactory(kind)(1);
                BaseChecker second = CheckerCatalog.GetFactory(kind)(1);

                Assert.IsInstanceOf<SpecialChecker>(first, kind.ToString());
                Assert.AreEqual(1, first.OwnerId, kind.ToString());
                Assert.AreNotSame(first, second, $"{kind}: checkers keep per-match state, so every call makes a new one.");
                Assert.IsTrue(typeIds.Add(first.TypeId), $"{kind} shares the type id {first.TypeId}.");
                Assert.AreEqual(first.TypeId, CheckerCatalog.GetTypeId(kind), kind.ToString());
            }
        }

        [Test]
        public void EveryTileKind_MakesAFreshTileOnEveryCall()
        {
            HashSet<string> effectIds = new HashSet<string>();

            foreach (TileKind kind in (TileKind[])Enum.GetValues(typeof(TileKind)))
            {
                Func<ITileEffect> create = TileCatalog.GetFactory(kind);
                ITileEffect tile = create();

                Assert.AreNotSame(tile, create(), $"{kind}: tiles keep per-match state, so every match needs its own.");
                Assert.IsTrue(effectIds.Add(tile.EffectId), $"{kind} shares the effect id {tile.EffectId}.");
            }
        }

        [Test]
        public void EverySpecialChecker_HasAShopCode()
        {
            HashSet<string> coded = new HashSet<string>();
            foreach (ShopTexts.ItemCode code in new ShopTexts().Codes)
                coded.Add(code.ItemId);

            foreach (CheckerKind kind in CheckerKinds)
                Assert.IsTrue(coded.Contains(CheckerCatalog.GetTypeId(kind)), $"{kind} has no code in ShopTexts.");
        }
    }
}
