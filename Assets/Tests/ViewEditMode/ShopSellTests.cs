using System.Reflection;
using NUnit.Framework;
using Tavla.Core.Checkers;
using Tavla.Core.Run;
using Tavla.Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>
    /// Selling on the shop screen, on the real art: the stall opens the list of the deck's special checkers and
    /// extra plain checkers in the panel above it, the first click on a row rings it and the second one sells it. Clicks go to the hit
    /// areas' mouse callbacks, as the mouse would send them.
    /// </summary>
    public class ShopSellTests
    {
        private const int Gold = 50;
        private static readonly int[] TierPrices = { 4, 6, 8, 10 };

        private static readonly MethodInfo OnMouseDown =
            typeof(ShopHitArea).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnMouseEnter =
            typeof(ShopHitArea).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnMouseExit =
            typeof(ShopHitArea).GetMethod("OnMouseExit", BindingFlags.Instance | BindingFlags.NonPublic);

        private ShopArtSet _art;
        private CheckerArtSet _checkerArt;
        private Sprite _plainArt;
        private BoardLayout _layout;
        private Sprite _solid;
        private ShopView _view;
        private ShopPresenter _presenter;
        private RunState _run;
        private Shop _shop;

        private GameObject SellPanel => _view.transform.Find("SellPanel").gameObject;
        private string Info => _view.transform.Find("Info").GetComponent<PixelLabel>().Text;

        [SetUp]
        public void SetUp()
        {
            _art = AssetDatabase.LoadAssetAtPath<ShopArtSet>(DataAssetTests.ShopArtPath);
            _checkerArt = AssetDatabase.LoadAssetAtPath<CheckerArtSet>(DataAssetTests.CheckerArtPath);
            _plainArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/player_checker.png");
            _layout = BoardLayout.CreateDefault();
            _solid = BoardSceneBuilder.CreateSolidSprite();
            _view = ShopView.Create(_art, _solid, _layout);
            _presenter = new ShopPresenter(_view, new ShopTexts(), _checkerArt, _plainArt);

            _run = new RunState(CreateDefinition(), new SystemRandomProvider(20261014));
            _run.StartMatch();
            _run.ActiveMatch.Context.AddScore(_run.PlayerId, _run.ActiveMatch.Definition.TargetScore);
            _shop = _run.OpenShop();
            _presenter.Show(_shop, _run, null);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            Object.DestroyImmediate(_view.gameObject);
            Object.DestroyImmediate(_solid.texture);
            Object.DestroyImmediate(_solid);
            Object.DestroyImmediate(_layout);
        }

        [Test]
        public void TheStall_IsDrawnInTheBottomRightCorner_AndTheListStartsClosed()
        {
            Assert.AreSame(_art.SellButton, _view.transform.Find("SellButton").GetComponent<SpriteRenderer>().sprite);
            Assert.IsNotNull(_view.transform.Find("Sell").GetComponent<BoxCollider2D>(), "The stall is clickable.");
            Assert.IsFalse(SellPanel.activeSelf);
        }

        [Test]
        public void TheStall_GlowsOnlyWhileThePointerIsOnIt()
        {
            Transform glow = _view.transform.Find("SellButtonGlow");
            Assert.IsNotNull(glow, "The stall's art is readable, so it has a glow.");
            Assert.IsFalse(glow.gameObject.activeSelf);

            Point("Reroll", OnMouseEnter);
            Assert.IsFalse(glow.gameObject.activeSelf, "Another button does not light the stall.");

            Point("Sell", OnMouseEnter);
            Point("Reroll", OnMouseExit); // The new Enter may come before the old Exit.
            Assert.IsTrue(glow.gameObject.activeSelf);

            Point("Sell", OnMouseExit);
            Assert.IsFalse(glow.gameObject.activeSelf);
        }

        [Test]
        public void TheStallsGlow_GoesOut_WhenTheShopCloses()
        {
            Point("Sell", OnMouseEnter);

            _view.SetVisible(false);
            _view.SetVisible(true);

            Assert.IsFalse(_view.transform.Find("SellButtonGlow").gameObject.activeSelf);
        }

        [Test]
        public void WithNothingToSell_TheStallSaysSo_AndOpensNothing()
        {
            Click("Sell");

            Assert.IsFalse(SellPanel.activeSelf);
            Assert.AreEqual(new ShopTexts().NothingToSell, Info);
        }

        [Test]
        public void TheStall_ListsTheDecksSpecialCheckers_ThenTheExtraPlainOnes()
        {
            DeckEntry first = Buy(ShopSlot.SpecialChecker1);
            DeckEntry second = Buy(ShopSlot.SpecialChecker2);
            Buy(ShopSlot.BasicChecker);

            Click("Sell");

            Assert.IsTrue(SellPanel.activeSelf);
            AssertRow(0, first);
            AssertRow(1, second);
            AssertPlainRow(2, 1);
            Assert.IsFalse(Row(3) != null && Row(3).gameObject.activeSelf);
        }

        [Test]
        public void TheExtraPlainCheckers_ShareOneRow_ThatCountsDownAsTheyAreSold()
        {
            Buy(ShopSlot.BasicChecker);
            Buy(ShopSlot.BasicChecker);
            int gold = _run.Gold;
            Click("Sell");
            AssertPlainRow(0, 2);

            Click("SellPanel/SellSlot_0");
            Click("SellPanel/SellSlot_0");

            Assert.AreEqual(gold + 1, _run.Gold, "Bought for 2, sold for 1.");
            AssertPlainRow(0, 1);

            Click("SellPanel/SellSlot_0");
            Click("SellPanel/SellSlot_0");

            Assert.AreEqual(gold + 2, _run.Gold);
            Assert.IsFalse(SellPanel.activeSelf, "The plain checkers the run started with are not for sale.");
        }

        [Test]
        public void TheFirstRow_LiesInTheSunkenPanelAboveTheStall()
        {
            Buy(ShopSlot.SpecialChecker1);
            Click("Sell");

            Vector3 disc = Row(0).Find("Art").position;
            Vector3 expected = _layout.PixelToWorld(285, 48);
            Assert.AreEqual(expected.x, disc.x, 1e-4f);
            Assert.AreEqual(expected.y, disc.y, 1e-4f);
        }

        [Test]
        public void ACheckerIsSold_OnlyOnTheSecondClick()
        {
            DeckEntry entry = Buy(ShopSlot.SpecialChecker1);
            int gold = _run.Gold;
            Click("Sell");

            Click("SellPanel/SellSlot_0");

            Assert.AreEqual(gold, _run.Gold, "One click only rings it.");
            Assert.IsTrue(_run.OwnsChecker(entry.TypeId));
            Assert.AreSame(_checkerArt.SelectedRing, RingOf(0));
            Assert.IsNotNull(RingOf(0));

            Click("SellPanel/SellSlot_0");

            Assert.AreEqual(gold + entry.PurchasePrice / 2, _run.Gold);
            Assert.IsFalse(_run.OwnsChecker(entry.TypeId));
            Assert.AreEqual(string.Format(new ShopTexts().SoldChecker, "", entry.PurchasePrice / 2), Info);
        }

        [Test]
        public void SellingTheLastChecker_ClosesTheList()
        {
            Buy(ShopSlot.SpecialChecker1);
            Click("Sell");

            Click("SellPanel/SellSlot_0");
            Click("SellPanel/SellSlot_0");

            Assert.IsFalse(SellPanel.activeSelf);
        }

        [Test]
        public void SellingOneChecker_MovesTheNextOneUp()
        {
            Buy(ShopSlot.SpecialChecker1);
            DeckEntry second = Buy(ShopSlot.SpecialChecker2);
            Click("Sell");

            Click("SellPanel/SellSlot_0");
            Click("SellPanel/SellSlot_0");

            Assert.IsTrue(SellPanel.activeSelf);
            AssertRow(0, second);
            Assert.IsNull(RingOf(0), "The ring went with the sold checker.");
            Assert.IsFalse(Row(1).gameObject.activeSelf);
        }

        [Test]
        public void AClickElsewhere_LetsTheRingedCheckerGo()
        {
            DeckEntry entry = Buy(ShopSlot.SpecialChecker1);
            Click("Sell");
            Click("SellPanel/SellSlot_0");

            Click("SpecialChecker1"); // The sold shelf: nothing is bought.
            Assert.IsNull(RingOf(0));

            Click("SellPanel/SellSlot_0");
            Assert.IsTrue(_run.OwnsChecker(entry.TypeId), "That click only rang it again.");
        }

        [Test]
        public void TheStall_ClosesTheListItOpened()
        {
            Buy(ShopSlot.SpecialChecker1);

            Click("Sell");
            Click("Sell");

            Assert.IsFalse(SellPanel.activeSelf);
        }

        private DeckEntry Buy(ShopSlot slot)
        {
            Assert.AreEqual(PurchaseResult.Success, _shop.Buy(slot));
            return _run.Deck[_run.Deck.Count - 1];
        }

        /// <summary>What the mouse does to a hit area. SendMessage would refuse in edit mode.</summary>
        private void Click(string path)
        {
            Transform target = _view.transform.Find(path);
            Assert.IsNotNull(target, path);
            OnMouseDown.Invoke(target.GetComponent<ShopHitArea>(), null);
        }

        /// <summary>The pointer entering or leaving a hit area.</summary>
        private void Point(string path, MethodInfo callback)
        {
            Transform target = _view.transform.Find(path);
            Assert.IsNotNull(target, path);
            callback.Invoke(target.GetComponent<ShopHitArea>(), null);
        }

        private Transform Row(int index)
        {
            return _view.transform.Find("SellPanel/SellSlot_" + index);
        }

        private Sprite RingOf(int index)
        {
            return Row(index).Find("Ring").GetComponent<SpriteRenderer>().sprite;
        }

        private void AssertRow(int index, DeckEntry entry)
        {
            Transform row = Row(index);
            Assert.IsTrue(row.gameObject.activeSelf, $"Row {index}.");
            Assert.AreSame(_checkerArt.GetSkin(entry.TypeId).Normal, row.Find("Art").GetComponent<SpriteRenderer>().sprite,
                           $"Row {index}.");
            Assert.AreEqual("+" + entry.PurchasePrice / 2 + "G", row.Find("Price").GetComponent<PixelLabel>().Text,
                            $"Row {index}.");
            Assert.AreEqual(string.Empty, row.Find("Label").GetComponent<PixelLabel>().Text, "It has art: no code.");
        }

        /// <summary>The row of the extra plain checkers: the player's plain checker with their number over it.</summary>
        private void AssertPlainRow(int index, int count)
        {
            Transform row = Row(index);
            Assert.IsTrue(row.gameObject.activeSelf, $"Row {index}.");
            Assert.AreSame(_plainArt, row.Find("Art").GetComponent<SpriteRenderer>().sprite);
            Assert.AreEqual("x" + count, row.Find("Label").GetComponent<PixelLabel>().Text);
            Assert.AreEqual("+1G", row.Find("Price").GetComponent<PixelLabel>().Text);
        }

        /// <summary>One stage of easy matches with a shop of four special checkers; a win pays nothing.</summary>
        private static RunDefinition CreateDefinition()
        {
            ShopItem[] specials =
            {
                ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new ArmoredChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new NomadChecker(ownerId)),
                ShopItem.SpecialChecker(3, ownerId => new CloverChecker(ownerId))
            };
            ShopCatalog catalog = new ShopCatalog(specials, new ShopItem[0], new ShopItem[0],
                                                  new ShopPrices(2, TierPrices, 4, 15, new[] { 1, 3 }));

            MatchDefinition[] matches = { new MatchDefinition(10, 5, 5), new MatchDefinition(10, 5, 5) };
            return new RunDefinition(new[] { new StageDefinition(matches) }, 5, 0, new RunEconomy(Gold, 0, 0, 0), catalog);
        }
    }
}
