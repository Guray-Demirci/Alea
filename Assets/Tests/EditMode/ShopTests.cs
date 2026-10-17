using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Relics;
using Tavla.Core.Run;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>
    /// The shop between matches: the catalog, stocking the shelves, buying, rerolling, the deck and relic rules,
    /// and the Phoenix (luxury) and Sacrificial (discount) effects (<see cref="Shop"/>, <see cref="ShopCatalog"/>).
    /// </summary>
    public class ShopTests
    {
        private const int BasicPrice = 2;
        private const int CardPrice = 4;
        private const int CharmPrice = 15;
        private static readonly int[] TierPrices = { 4, 6, 8, 10 };
        private static readonly int[] RerollPrices = { 1, 3, 5, 7, 9 };

        private static readonly ShopSlot[] SpecialSlots =
            { ShopSlot.SpecialChecker1, ShopSlot.SpecialChecker2, ShopSlot.SpecialChecker3 };

        #region Catalog

        [Test]
        public void ShopItem_TakesItsIdAndNameFromTheChecker()
        {
            ShopItem catalyst = ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId));

            Assert.AreEqual(CatalystChecker.Id, catalyst.ItemId);
            Assert.AreEqual("Catalyst Checker", catalyst.DisplayName);
            Assert.AreEqual(1, catalyst.CheckerCount);
        }

        [Test]
        public void ShopItem_NamesTheCheckerTypeItAddsToTheDeck_AndRelicsNone()
        {
            Assert.AreEqual(CatalystChecker.Id, ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId)).CheckerTypeId);
            Assert.AreEqual(DefaultChecker.Id, ShopItem.BasicChecker().CheckerTypeId);
            Assert.IsNull(Card().CheckerTypeId);
            Assert.IsNull(ShopItem.Charm(() => new TestCharm()).CheckerTypeId);
        }

        [Test]
        public void ShopItem_RejectsBadTiersAndCheckersThatCannotBeSold()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ShopItem.SpecialChecker(0, ownerId => new CatalystChecker(ownerId)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ShopItem.SpecialChecker(ShopItem.MaxTier + 1, ownerId => new CatalystChecker(ownerId)));
            Assert.Throws<ArgumentException>(() => ShopItem.SpecialChecker(1, ownerId => new DefaultChecker(ownerId)));
            Assert.Throws<ArgumentException>(() => ShopItem.SpecialChecker(1, ownerId => null));
        }

        [Test]
        public void Catalog_RejectsItemsOfTheWrongKindOrListedTwice()
        {
            ShopPrices prices = Prices();
            ShopItem catalyst = ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId));

            Assert.Throws<ArgumentException>(() => new ShopCatalog(new[] { Card() }, new ShopItem[0], new ShopItem[0], prices));
            Assert.Throws<ArgumentException>(() => new ShopCatalog(new[] { catalyst, catalyst }, new ShopItem[0], new ShopItem[0], prices));
            Assert.Throws<ArgumentNullException>(() => new ShopCatalog(new ShopItem[0], new ShopItem[0], new ShopItem[0], null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ShopCatalog(new ShopItem[0], new ShopItem[0], new ShopItem[0], prices, -1));
        }

        [Test]
        public void Prices_NeedOnePricePerTier_AndNoNegatives()
        {
            Assert.Throws<ArgumentException>(() => new ShopPrices(BasicPrice, new[] { 4, 6, 8 }, CardPrice, CharmPrice, RerollPrices));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ShopPrices(-1, TierPrices, CardPrice, CharmPrice, RerollPrices));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ShopPrices(BasicPrice, TierPrices, CardPrice, CharmPrice, new[] { 1, -3 }));
        }

        #endregion

        #region Opening and closing

        [Test]
        public void TheShop_OpensOnlyBetweenMatches()
        {
            RunState run = CreateRun(gold: 0);
            run.StartMatch();

            Assert.IsFalse(run.CanOpenShop);
            Assert.Throws<InvalidOperationException>(() => run.OpenShop());

            WinActiveMatch(run);

            Assert.IsTrue(run.CanOpenShop);
            Assert.AreSame(run.OpenShop(), run.ActiveShop);
        }

        [Test]
        public void TheNextMatch_WaitsForTheShopToClose()
        {
            RunState run = CreateRunInShop(gold: 0);
            Shop shop = run.ActiveShop;

            Assert.IsFalse(run.CanStartMatch);
            Assert.Throws<InvalidOperationException>(() => run.StartMatch());

            shop.Close();

            Assert.IsNull(run.ActiveShop);
            Assert.AreEqual(PurchaseResult.ShopClosed, shop.Buy(ShopSlot.BasicChecker));
            Assert.AreEqual(RerollResult.ShopClosed, shop.Reroll());
            Assert.IsNotNull(run.StartMatch());
        }

        [Test]
        public void ARunWithoutAShop_CannotOpenOne()
        {
            RunState run = new RunState(CreateDefinition(0, shop: null), new FixedRandom());
            run.StartMatch();
            WinActiveMatch(run);

            Assert.IsFalse(run.CanOpenShop);
            Assert.Throws<InvalidOperationException>(() => run.OpenShop());
        }

        #endregion

        #region Shelves

        [Test]
        public void Shelves_HoldThreeDifferentSpecialCheckers_ThePlainChecker_ACardAndACharm()
        {
            Shop shop = CreateRunInShop(gold: 0).ActiveShop;

            HashSet<string> specials = new HashSet<string>
            {
                shop.GetOffer(ShopSlot.SpecialChecker1).Item.ItemId,
                shop.GetOffer(ShopSlot.SpecialChecker2).Item.ItemId,
                shop.GetOffer(ShopSlot.SpecialChecker3).Item.ItemId
            };

            Assert.AreEqual(3, specials.Count);
            Assert.AreEqual(ShopItemKind.BasicChecker, shop.GetOffer(ShopSlot.BasicChecker).Item.Kind);
            Assert.AreEqual(BasicPrice, shop.GetOffer(ShopSlot.BasicChecker).Price);
            Assert.AreEqual(ShopItemKind.Card, shop.GetOffer(ShopSlot.Card).Item.Kind);
            Assert.AreEqual(ShopItemKind.Charm, shop.GetOffer(ShopSlot.Charm).Item.Kind);
        }

        [Test]
        public void SpecialCheckers_ArePricedByTier()
        {
            Shop shop = CreateRunInShop(gold: 0).ActiveShop;

            foreach (ShopSlot slot in new[] { ShopSlot.SpecialChecker1, ShopSlot.SpecialChecker2, ShopSlot.SpecialChecker3 })
            {
                ShopOffer offer = shop.GetOffer(slot);
                Assert.AreEqual(TierPrices[offer.Item.Tier - 1], offer.Price, offer.Item.ItemId);
            }
        }

        [Test]
        public void AnEmptyCatalogList_LeavesItsShelvesEmpty()
        {
            ShopCatalog catalog = new ShopCatalog(new ShopItem[0], new ShopItem[0], new ShopItem[0], Prices());
            Shop shop = CreateRunInShop(gold: 10, catalog: catalog).ActiveShop;

            Assert.IsNull(shop.GetOffer(ShopSlot.SpecialChecker1));
            Assert.IsNull(shop.GetOffer(ShopSlot.Card));
            Assert.AreEqual(PurchaseResult.EmptySlot, shop.Buy(ShopSlot.Charm));
            Assert.IsNotNull(shop.GetOffer(ShopSlot.BasicChecker), "The plain checker is always for sale.");
        }

        #endregion

        #region Buying

        [Test]
        public void BuyingASpecialChecker_SpendsTheGold_AndPutsItInTheDeck()
        {
            RunState run = CreateRunInShop(gold: 20);
            Shop shop = run.ActiveShop;
            ShopOffer offer = shop.GetOffer(ShopSlot.SpecialChecker1);
            int changes = 0;
            shop.Changed += () => changes++;

            Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.SpecialChecker1));

            Assert.AreEqual(20 - offer.Price, run.Gold);
            Assert.AreEqual(Starting + 1, run.Deck.Count);
            Assert.AreEqual(offer.Item.ItemId, run.Deck[run.Deck.Count - 1].TypeId);
            Assert.AreEqual(1, run.SpecialCheckerCount);
            Assert.IsTrue(offer.IsSold);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(PurchaseResult.AlreadySold, shop.Buy(ShopSlot.SpecialChecker1));
        }

        [Test]
        public void TheBoughtChecker_PlaysInTheNextMatch()
        {
            RunState run = CreateRunInShop(gold: 20);
            string bought = run.ActiveShop.GetOffer(ShopSlot.SpecialChecker1).Item.ItemId;
            run.ActiveShop.Buy(ShopSlot.SpecialChecker1);
            run.ActiveShop.Close();

            GameState board = run.StartMatch().Board;

            Assert.AreEqual(1, CountCheckersOfType(board, bought));
        }

        [Test]
        public void WithoutEnoughGold_NothingChanges()
        {
            RunState run = CreateRunInShop(gold: 1);
            Shop shop = run.ActiveShop;

            Assert.AreEqual(PurchaseResult.NotEnoughGold, shop.CheckPurchase(ShopSlot.BasicChecker));
            Assert.AreEqual(PurchaseResult.NotEnoughGold, shop.Buy(ShopSlot.BasicChecker));

            Assert.AreEqual(1, run.Gold);
            Assert.AreEqual(Starting, run.Deck.Count);
            Assert.IsFalse(shop.GetOffer(ShopSlot.BasicChecker).IsSold);
        }

        [Test]
        public void ThePlainChecker_NeverSellsOut_WhileTheGoldLasts()
        {
            RunState run = CreateRunInShop(gold: 7);
            Shop shop = run.ActiveShop;

            for (int i = 0; i < 3; i++)
                Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.BasicChecker), $"Purchase {i + 1}");

            Assert.AreEqual(Starting + 3, run.Deck.Count);
            Assert.AreEqual(7 - 3 * BasicPrice, run.Gold);
            Assert.IsFalse(shop.GetOffer(ShopSlot.BasicChecker).IsSold);
            Assert.AreEqual(PurchaseResult.NotEnoughGold, shop.Buy(ShopSlot.BasicChecker), "1 gold left.");
        }

        [Test]
        public void ThePlainChecker_IsNotASpecialChecker()
        {
            RunState run = CreateRunInShop(gold: 20);

            run.ActiveShop.Buy(ShopSlot.BasicChecker);

            Assert.AreEqual(Starting + 1, run.Deck.Count);
            Assert.AreEqual(0, run.SpecialCheckerCount);
        }

        [Test]
        public void TheDeck_HoldsAtMostTheAllowedSpecialCheckers()
        {
            RunState run = CreateRunInShop(gold: 50, catalog: Catalog(StandardSpecials(), maxSpecialCheckers: 1));
            Shop shop = run.ActiveShop;

            Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.SpecialChecker1));
            Assert.AreEqual(PurchaseResult.TooManySpecialCheckers, shop.Buy(ShopSlot.SpecialChecker2));
            Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.BasicChecker), "Plain checkers are not limited.");
        }

        [Test]
        public void AFullDeck_TakesNoMoreCheckers()
        {
            RunState run = CreateRunInShop(gold: 50);
            while (run.Deck.Count < GameState.MaxCheckersPerPlayer)
                run.AddToDeck(ownerId => new DefaultChecker(ownerId));

            Assert.AreEqual(PurchaseResult.DeckFull, run.ActiveShop.Buy(ShopSlot.BasicChecker));
            Assert.AreEqual(PurchaseResult.DeckFull, run.ActiveShop.Buy(ShopSlot.SpecialChecker1));
            Assert.AreEqual(PurchaseResult.Success, run.ActiveShop.Buy(ShopSlot.Card), "Relics do not take a deck slot.");
        }

        [Test]
        public void ABoughtCharm_IsOwned_AndNeverOfferedAgain()
        {
            RunState run = CreateRunInShop(gold: 50);
            Shop shop = run.ActiveShop;

            Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.Charm));
            Assert.IsTrue(run.OwnsRelic(TestCharm.Id));

            shop.Reroll();
            Assert.IsNull(shop.GetOffer(ShopSlot.Charm), "The only charm is owned.");
        }

        [Test]
        public void ABoughtSpecialChecker_IsNeverOfferedAgain()
        {
            // Four specials for three shelves: once one is owned, every restock shows exactly the other three.
            ShopItem[] specials =
            {
                ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new ArmoredChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new NomadChecker(ownerId)),
                ShopItem.SpecialChecker(3, ownerId => new CloverChecker(ownerId))
            };
            RunState run = CreateRunInShop(gold: 100, catalog: Catalog(specials));
            Shop shop = run.ActiveShop;
            string bought = shop.GetOffer(ShopSlot.SpecialChecker1).Item.ItemId;

            Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.SpecialChecker1));

            while (shop.RerollsLeft > 0)
            {
                Assert.AreEqual(RerollResult.Success, shop.Reroll());
                foreach (ShopSlot slot in SpecialSlots)
                    Assert.AreNotEqual(bought, shop.GetOffer(slot).Item.ItemId, "The deck holds one of each.");
            }
        }

        [Test]
        public void ASpecialCheckerTheDeckHolds_CannotBeBought()
        {
            // Three specials for three shelves: all of them are on show.
            ShopItem[] specials =
            {
                ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new ArmoredChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new NomadChecker(ownerId))
            };
            RunState run = CreateRunInShop(gold: 100, catalog: Catalog(specials));
            Shop shop = run.ActiveShop;
            ShopSlot armored = Array.Find(SpecialSlots, slot => shop.GetOffer(slot).Item.ItemId == ArmoredChecker.Id);

            // The checker reaches the deck while its shelf is on show.
            run.AddToDeck(ownerId => new ArmoredChecker(ownerId));

            Assert.AreEqual(PurchaseResult.AlreadyOwned, shop.Buy(armored));
            Assert.AreEqual(100, run.Gold);
        }

        [Test]
        public void ACharm_BendsOnlyItsOwnersDice()
        {
            RunState run = CreateRunInShop(gold: 50);
            run.ActiveShop.Buy(ShopSlot.Charm);
            run.ActiveShop.Close();

            MatchContext context = run.StartMatch().Context;

            Assert.AreEqual(6, context.RollDice(0).First, "The charm loads the player's dice.");
            Assert.AreEqual(1, context.RollDice(1).First, "FixedRandom rolls 1 for the opponent.");
        }

        [Test]
        public void AUsedUpCard_LeavesTheRelicsAfterTheMatch()
        {
            RunState run = CreateRunInShop(gold: 50);
            run.ActiveShop.Buy(ShopSlot.Card);
            run.ActiveShop.Close();
            Assert.AreEqual(1, run.Relics.Count);

            RunMatch match = run.StartMatch();
            ((MeridianShiftRelic)run.Relics[0]).TryActivate(match.Context, 6);
            Assert.AreEqual(6, match.Context.RollDice(0).Second, "The armed card pins the second die.");
            WinActiveMatch(run);

            Assert.AreEqual(0, run.Relics.Count);
        }

        #endregion

        #region Selling

        [Test]
        public void TheSellPrice_IsHalfThePurchasePrice_RoundedDown_ButAtLeast1()
        {
            Assert.AreEqual(5, ShopPrices.GetSellPrice(10));
            Assert.AreEqual(2, ShopPrices.GetSellPrice(5));
            Assert.AreEqual(1, ShopPrices.GetSellPrice(1), "Anything that cost gold brings some back.");
            Assert.AreEqual(0, ShopPrices.GetSellPrice(0), "A free checker brings nothing.");
        }

        [Test]
        public void SellingASpecialChecker_PaysHalfItsPrice_AndTakesItOutOfTheDeck()
        {
            RunState run = CreateRunInShop(gold: 20);
            Shop shop = run.ActiveShop;
            ShopOffer offer = shop.GetOffer(ShopSlot.SpecialChecker1);
            shop.Buy(ShopSlot.SpecialChecker1);
            DeckEntry bought = run.Deck[run.Deck.Count - 1];
            int changes = 0;
            shop.Changed += () => changes++;

            Assert.AreEqual(offer.Price, bought.PurchasePrice);
            Assert.AreEqual(offer.Price / 2, shop.GetSellPrice(bought));
            Assert.AreEqual(SaleResult.Success, shop.Sell(bought));

            Assert.AreEqual(20 - offer.Price + offer.Price / 2, run.Gold);
            Assert.AreEqual(Starting, run.Deck.Count);
            Assert.AreEqual(0, run.SpecialCheckerCount);
            Assert.IsFalse(run.OwnsChecker(offer.Item.CheckerTypeId));
            Assert.AreEqual(1, changes);
            Assert.AreEqual(SaleResult.NotInDeck, shop.Sell(bought), "It is sold already.");
        }

        [Test]
        public void TheSellPrice_IsHalfOfWhatWasPaid_DiscountIncluded()
        {
            RunState run = CreateRun(gold: 50);
            run.StartMatch().Context.AddShopDiscount(2);
            WinActiveMatch(run);
            Shop shop = run.OpenShop();
            ShopOffer offer = shop.GetOffer(ShopSlot.SpecialChecker1);

            shop.Buy(ShopSlot.SpecialChecker1);

            int paid = TierPrices[offer.Item.Tier - 1] - 2;
            Assert.AreEqual(paid, run.Deck[run.Deck.Count - 1].PurchasePrice);
            Assert.AreEqual(paid / 2, shop.GetSellPrice(run.Deck[run.Deck.Count - 1]));
        }

        [Test]
        public void TheSpecialCheckers_AreListedForSale_InDeckOrder_ThenTheExtraPlainOnes()
        {
            RunState run = CreateRunInShop(gold: 50);
            Shop shop = run.ActiveShop;
            shop.Buy(ShopSlot.BasicChecker);
            shop.Buy(ShopSlot.SpecialChecker1);
            shop.Buy(ShopSlot.SpecialChecker2);
            List<DeckEntry> sellable = new List<DeckEntry>();

            shop.GetSellableCheckers(sellable);

            Assert.AreEqual(3, sellable.Count);
            Assert.AreEqual(shop.GetOffer(ShopSlot.SpecialChecker1).Item.CheckerTypeId, sellable[0].TypeId);
            Assert.AreEqual(shop.GetOffer(ShopSlot.SpecialChecker2).Item.CheckerTypeId, sellable[1].TypeId);
            Assert.IsFalse(sellable[2].IsSpecial);
            Assert.AreEqual(1, shop.ExtraPlainCheckerCount);
        }

        [Test]
        public void ThePlainCheckersTheRunStartedWith_CannotBeSold()
        {
            RunState run = CreateRunInShop(gold: 50);
            Shop shop = run.ActiveShop;
            List<DeckEntry> sellable = new List<DeckEntry>();

            shop.GetSellableCheckers(sellable);

            Assert.AreEqual(0, sellable.Count);
            Assert.AreEqual(0, shop.ExtraPlainCheckerCount);
            Assert.AreEqual(SaleResult.NotForSale, shop.Sell(run.Deck[0]));
            Assert.AreEqual(Starting, run.Deck.Count);
        }

        [Test]
        public void ExtraPlainCheckers_SellForHalfTheirPrice_DownToTheOnesTheRunStartedWith()
        {
            RunState run = CreateRunInShop(gold: 10);
            Shop shop = run.ActiveShop;
            shop.Buy(ShopSlot.BasicChecker);
            shop.Buy(ShopSlot.BasicChecker);
            List<DeckEntry> sellable = new List<DeckEntry>();

            for (int i = 0; i < 2; i++)
            {
                shop.GetSellableCheckers(sellable);
                Assert.AreEqual(2 - i, sellable.Count);
                Assert.AreEqual(BasicPrice / 2, shop.GetSellPrice(sellable[0]));
                Assert.AreEqual(SaleResult.Success, shop.Sell(sellable[0]), $"Sale {i + 1}");
            }

            Assert.AreEqual(10 - 2 * BasicPrice + 2 * (BasicPrice / 2), run.Gold);
            Assert.AreEqual(Starting, run.Deck.Count);
            Assert.AreEqual(0, shop.ExtraPlainCheckerCount);
            Assert.AreEqual(SaleResult.NotForSale, shop.Sell(run.Deck[0]), "None to spare any more.");
        }

        [Test]
        public void TheExtraPlainChecker_ListedForSale_IsTheDearestOne()
        {
            // The bought one is added last, behind the five free ones the run started with.
            RunState run = CreateRunInShop(gold: 10);
            Shop shop = run.ActiveShop;
            shop.Buy(ShopSlot.BasicChecker);
            List<DeckEntry> sellable = new List<DeckEntry>();

            shop.GetSellableCheckers(sellable);

            Assert.AreEqual(1, sellable.Count);
            Assert.AreSame(run.Deck[run.Deck.Count - 1], sellable[0]);
            Assert.AreEqual(BasicPrice, sellable[0].PurchasePrice);
        }

        [Test]
        public void Selling_FreesAPlaceUnderTheSpecialCheckerLimit()
        {
            RunState run = CreateRunInShop(gold: 50, catalog: Catalog(StandardSpecials(), maxSpecialCheckers: 1));
            Shop shop = run.ActiveShop;
            shop.Buy(ShopSlot.SpecialChecker1);
            Assert.AreEqual(PurchaseResult.TooManySpecialCheckers, shop.CheckPurchase(ShopSlot.SpecialChecker2));

            shop.Sell(run.Deck[run.Deck.Count - 1]);

            Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.SpecialChecker2));
        }

        [Test]
        public void ASoldChecker_CanBeOfferedAgain_AfterAReroll()
        {
            // Four specials for three shelves: once one is sold back, a reroll can bring all four.
            ShopItem[] specials =
            {
                ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new ArmoredChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new NomadChecker(ownerId)),
                ShopItem.SpecialChecker(3, ownerId => new CloverChecker(ownerId))
            };
            RunState run = CreateRunInShop(gold: 100, catalog: Catalog(specials));
            Shop shop = run.ActiveShop;
            string sold = ItemOn(shop, ShopSlot.SpecialChecker1);
            shop.Buy(ShopSlot.SpecialChecker1);
            shop.Sell(run.Deck[run.Deck.Count - 1]);

            Assert.IsTrue(shop.GetOffer(ShopSlot.SpecialChecker1).IsSold, "The shelves on show stay as they are.");

            shop.Reroll();

            Assert.AreEqual(sold, ItemOn(shop, ShopSlot.SpecialChecker1), "FixedRandom draws the first candidate.");
        }

        [Test]
        public void Selling_LeavesTheDeckAtTheFewestCheckersAMatchDeals()
        {
            // Two plain checkers blown up: the deck is filled back to the minimum with the Armored in it, so
            // selling the Armored takes it below the minimum again.
            RunState run = CreateRun(gold: 0);
            run.AddToDeck(ownerId => new ArmoredChecker(ownerId));
            GameState board = run.StartMatch().Board;
            int destroyed = 0;
            for (int i = 0; i < GameState.NodeCount && destroyed < 2; i++)
            {
                foreach (BaseChecker checker in new List<BaseChecker>(board.Nodes[i].Checkers))
                {
                    if (destroyed < 2 && checker is DefaultChecker && checker.OwnerId == run.PlayerId)
                    {
                        board.DestroyChecker(checker, i);
                        destroyed++;
                    }
                }
            }

            WinActiveMatch(run);
            Assert.AreEqual(Starting, run.Deck.Count);

            Shop shop = run.OpenShop();
            Assert.AreEqual(SaleResult.Success, shop.Sell(new List<DeckEntry>(run.Deck).Find(entry => entry.IsSpecial)));

            Assert.AreEqual(Starting, run.Deck.Count);
            Assert.AreEqual(0, run.SpecialCheckerCount);
            Assert.AreEqual(0, run.Gold, "Given to the deck for free, it sells for nothing.");
        }

        [Test]
        public void NothingIsSold_OnceTheShopIsClosed()
        {
            RunState run = CreateRunInShop(gold: 20);
            Shop shop = run.ActiveShop;
            shop.Buy(ShopSlot.SpecialChecker1);
            DeckEntry bought = run.Deck[run.Deck.Count - 1];
            shop.Close();

            Assert.AreEqual(SaleResult.ShopClosed, shop.Sell(bought));
            Assert.IsTrue(run.OwnsChecker(bought.TypeId));
        }

        #endregion

        #region Rerolling

        [Test]
        public void Rerolls_Cost1_3_5_7_9_ThenRunOut()
        {
            RunState run = CreateRunInShop(gold: 100);
            Shop shop = run.ActiveShop;
            ShopOffer basic = shop.GetOffer(ShopSlot.BasicChecker);
            List<int> paid = new List<int>();

            while (shop.NextRerollPrice.HasValue)
            {
                int before = run.Gold;
                Assert.AreEqual(RerollResult.Success, shop.Reroll());
                paid.Add(before - run.Gold);
            }

            CollectionAssert.AreEqual(RerollPrices, paid);
            Assert.AreEqual(0, shop.RerollsLeft);
            Assert.AreEqual(RerollResult.NoRerollsLeft, shop.Reroll());
            Assert.AreSame(basic, shop.GetOffer(ShopSlot.BasicChecker), "Rerolls leave the plain checker alone.");
        }

        [Test]
        public void ARerollWithoutTheGold_ChangesNothing()
        {
            RunState run = CreateRunInShop(gold: 0);
            Shop shop = run.ActiveShop;
            ShopOffer before = shop.GetOffer(ShopSlot.SpecialChecker1);

            Assert.AreEqual(RerollResult.NotEnoughGold, shop.Reroll());
            Assert.AreSame(before, shop.GetOffer(ShopSlot.SpecialChecker1));
            Assert.AreEqual(0, shop.RerollsUsed);
        }

        [Test]
        public void EveryVisit_StartsTheRerollPricesAgain()
        {
            RunState run = CreateRunInShop(gold: 100);
            run.ActiveShop.Reroll();
            run.ActiveShop.Reroll();
            run.ActiveShop.Close();
            run.StartMatch();
            WinActiveMatch(run);

            Assert.AreEqual(RerollPrices[0], run.OpenShop().NextRerollPrice);
        }

        [Test]
        public void ARerollRestocks_SoldShelvesToo()
        {
            RunState run = CreateRunInShop(gold: 100);
            Shop shop = run.ActiveShop;
            shop.Buy(ShopSlot.SpecialChecker1);

            shop.Reroll();

            Assert.IsFalse(shop.GetOffer(ShopSlot.SpecialChecker1).IsSold);
        }

        #endregion

        #region Tier odds

        [Test]
        public void TierOdds_NeedOneWeightPerTier_NoNegatives_AndSomeWeight()
        {
            Assert.Throws<ArgumentNullException>(() => new ShopTierOdds(null));
            Assert.Throws<ArgumentException>(() => new ShopTierOdds(new[] { 70, 25, 5 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Odds(70, -1, 5, 0));
            Assert.Throws<ArgumentException>(() => Odds(0, 0, 0, 0));
        }

        [Test]
        public void TierOdds_RollEachTierAsOftenAsItsWeight_AndAWeightlessOneNever()
        {
            int[] counts = RollMany(Odds(70, 25, 5, 0), ShopItem.MinTier);

            Assert.AreEqual(0.70, Share(counts, 1), 0.02);
            Assert.AreEqual(0.25, Share(counts, 2), 0.02);
            Assert.AreEqual(0.05, Share(counts, 3), 0.01);
            Assert.AreEqual(0, counts[4]);
        }

        [Test]
        public void ALuxuryRoll_SkipsTier1_AndTheOtherTiersShareItsChance()
        {
            int[] counts = RollMany(Odds(70, 20, 10, 0), Shop.LuxuryMinTier);

            Assert.AreEqual(0, counts[1]);
            Assert.AreEqual(2 / 3.0, Share(counts, 2), 0.02);
            Assert.AreEqual(1 / 3.0, Share(counts, 3), 0.02);
            Assert.AreEqual(0, Odds(1, 0, 0, 0).RollTier(new FixedRandom(), Shop.LuxuryMinTier), "No tier is left to roll.");
        }

        [Test]
        public void ATierWithoutWeight_IsNeverOffered_EvenToFillAShelf()
        {
            Shop shop = CreateRunInShop(gold: 0, odds: Odds(0, 0, 0, 1)).ActiveShop;

            Assert.AreEqual(DoomsdayChecker.Id, ItemOn(shop, ShopSlot.SpecialChecker1));
            Assert.IsNull(shop.GetOffer(ShopSlot.SpecialChecker2), "The only tier 4 checker is already on show.");
            Assert.IsNull(shop.GetOffer(ShopSlot.SpecialChecker3));
        }

        [Test]
        public void ARolledTierWithNothingLeft_GivesWayToTheNearestAllowedTierBelow_ThenAbove()
        {
            // Tiers 1 and 3 hold one checker each; tier 2's two are left out by the odds.
            ShopTierOdds odds = Odds(1, 0, 1, 0);

            Shop high = CreateRunInShop(gold: 0, odds: odds, random: new HighRandom()).ActiveShop;
            Assert.AreEqual(CloverChecker.Id, ItemOn(high, ShopSlot.SpecialChecker1), "Rolled tier 3.");
            Assert.AreEqual(CatalystChecker.Id, ItemOn(high, ShopSlot.SpecialChecker2), "Tier 3 again, used up: down to 1.");
            Assert.IsNull(high.GetOffer(ShopSlot.SpecialChecker3));

            Shop low = CreateRunInShop(gold: 0, odds: odds).ActiveShop;
            Assert.AreEqual(CatalystChecker.Id, ItemOn(low, ShopSlot.SpecialChecker1), "Rolled tier 1.");
            Assert.AreEqual(CloverChecker.Id, ItemOn(low, ShopSlot.SpecialChecker2), "Tier 1 again, used up: up to 3.");
            Assert.IsNull(low.GetOffer(ShopSlot.SpecialChecker3));
        }

        [Test]
        public void ALuxuryShop_InAStageThatOnlyWeighsTier1_StillFillsItsShelvesWithBetterCheckers()
        {
            RunState run = CreateRun(gold: 0, odds: Odds(1, 0, 0, 0));
            run.StartMatch().Context.TriggerLuxuryShop(0);
            WinActiveMatch(run);

            Shop shop = run.OpenShop();

            foreach (ShopSlot slot in SpecialSlots)
                Assert.GreaterOrEqual(shop.GetOffer(slot).Item.Tier, Shop.LuxuryMinTier);
        }

        [Test]
        public void TheShop_FollowsTheOddsOfTheStageItsNextMatchBelongsTo()
        {
            StageDefinition commonStage = EasyStage(2, Odds(1, 0, 0, 0));
            StageDefinition specialStage = EasyStage(1, Odds(0, 0, 0, 1));
            RunDefinition definition = new RunDefinition(new[] { commonStage, specialStage }, Starting, 0,
                                                         new RunEconomy(0, 0, 0, 0), Catalog(StandardSpecials()));
            RunState run = new RunState(definition, new FixedRandom());

            run.StartMatch();
            WinActiveMatch(run);
            Assert.AreEqual(1, run.OpenShop().GetOffer(ShopSlot.SpecialChecker1).Item.Tier, "Stage 1's second match is next.");
            run.ActiveShop.Close();

            run.StartMatch();
            WinActiveMatch(run);
            Assert.AreEqual(4, run.OpenShop().GetOffer(ShopSlot.SpecialChecker1).Item.Tier, "Stage 1 is over: stage 2's odds.");
        }

        /// <summary>Tier counts of many rolls with a fixed seed, so the result never changes; index 0 counts "no tier".</summary>
        private static int[] RollMany(ShopTierOdds odds, int minTier)
        {
            SystemRandomProvider random = new SystemRandomProvider(20261010);
            int[] counts = new int[ShopItem.MaxTier + 1];

            for (int i = 0; i < RollCount; i++)
                counts[odds.RollTier(random, minTier)]++;

            return counts;
        }

        private const int RollCount = 20000;

        private static double Share(int[] counts, int tier) => counts[tier] / (double)RollCount;

        #endregion

        #region Phoenix and Sacrificial

        [Test]
        public void APhoenixRise_MakesTheNextShopALuxuryShop()
        {
            RunState run = CreateRun(gold: 0);
            RunMatch match = run.StartMatch();
            match.Context.TriggerLuxuryShop(0);
            WinActiveMatch(run);

            Shop shop = run.OpenShop();

            Assert.IsTrue(shop.IsLuxury);
            Assert.IsFalse(run.LuxuryShopPending, "This visit used it up.");
            foreach (ShopSlot slot in new[] { ShopSlot.SpecialChecker1, ShopSlot.SpecialChecker2, ShopSlot.SpecialChecker3 })
            {
                ShopOffer offer = shop.GetOffer(slot);
                Assert.GreaterOrEqual(offer.Item.Tier, Shop.LuxuryMinTier, "No common items.");
                Assert.AreEqual(TierPrices[offer.Item.Tier - 1], offer.Price, "Normal price: a luxury shop is not a sale.");
            }

            Assert.AreEqual(BasicPrice, shop.GetOffer(ShopSlot.BasicChecker).Price);

            shop.Close();
            run.StartMatch();
            WinActiveMatch(run);
            Assert.IsFalse(run.OpenShop().IsLuxury);
        }

        [Test]
        public void ASacrifice_LowersEveryPriceForTheRestOfTheRun_ButNotBelowZero()
        {
            RunState run = CreateRun(gold: 0);
            RunMatch match = run.StartMatch();
            match.Context.AddShopDiscount(3);
            WinActiveMatch(run);

            Shop shop = run.OpenShop();

            Assert.AreEqual(3, run.ShopDiscount);
            Assert.AreEqual(0, shop.GetOffer(ShopSlot.BasicChecker).Price, "2 - 3 is free, not negative.");
            Assert.AreEqual(CardPrice - 3, shop.GetOffer(ShopSlot.Card).Price);
            Assert.AreEqual(RerollPrices[0], shop.NextRerollPrice, "Rerolls are not discounted.");
            Assert.AreEqual(PurchaseResult.Success, shop.Buy(ShopSlot.BasicChecker), "A free item needs no gold.");
        }

        #endregion

        #region Helpers

        private const int Starting = GameState.MinCheckersPerPlayer;

        /// <summary>Tier 1..4 checkers (plus a second tier-2 one), in catalog order.</summary>
        private static ShopItem[] StandardSpecials()
        {
            return new[]
            {
                ShopItem.SpecialChecker(1, ownerId => new CatalystChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new ArmoredChecker(ownerId)),
                ShopItem.SpecialChecker(2, ownerId => new NomadChecker(ownerId)),
                ShopItem.SpecialChecker(3, ownerId => new CloverChecker(ownerId)),
                ShopItem.SpecialChecker(4, ownerId => new DoomsdayChecker(ownerId))
            };
        }

        private static ShopItem Card() => ShopItem.Card(() => new MeridianShiftRelic());

        private static ShopPrices Prices() => new ShopPrices(BasicPrice, TierPrices, CardPrice, CharmPrice, RerollPrices);

        private static ShopCatalog Catalog(ShopItem[] specials, int maxSpecialCheckers = ShopCatalog.DefaultMaxSpecialCheckers)
        {
            return new ShopCatalog(specials, new[] { Card() }, new[] { ShopItem.Charm(() => new TestCharm()) },
                                   Prices(), maxSpecialCheckers);
        }

        /// <summary>Ten easy matches; a win pays nothing, so the gold is exactly what the test gives.</summary>
        private static RunDefinition CreateDefinition(int gold, ShopCatalog shop, ShopTierOdds odds = null)
        {
            return new RunDefinition(new[] { EasyStage(10, odds) }, Starting, 0, new RunEconomy(gold, 0, 0, 0), shop);
        }

        private static StageDefinition EasyStage(int matchCount, ShopTierOdds odds)
        {
            MatchDefinition[] matches = new MatchDefinition[matchCount];
            for (int i = 0; i < matches.Length; i++)
                matches[i] = new MatchDefinition(10, 5, Starting);

            return new StageDefinition(matches, null, odds);
        }

        /// <param name="random">Null: <see cref="FixedRandom"/>, which always draws the first candidate.</param>
        private static RunState CreateRun(int gold, ShopCatalog catalog = null, ShopTierOdds odds = null,
                                          IRandomProvider random = null)
        {
            return new RunState(CreateDefinition(gold, catalog ?? Catalog(StandardSpecials()), odds),
                                random ?? new FixedRandom());
        }

        /// <summary>A run that has won its first match and stands in the shop.</summary>
        private static RunState CreateRunInShop(int gold, ShopCatalog catalog = null, ShopTierOdds odds = null,
                                                IRandomProvider random = null)
        {
            RunState run = CreateRun(gold, catalog, odds, random);
            run.StartMatch();
            WinActiveMatch(run);
            run.OpenShop();
            return run;
        }

        private static ShopTierOdds Odds(int tier1, int tier2, int tier3, int tier4)
        {
            return new ShopTierOdds(new[] { tier1, tier2, tier3, tier4 });
        }

        private static string ItemOn(Shop shop, ShopSlot slot)
        {
            ShopOffer offer = shop.GetOffer(slot);
            return offer != null ? offer.Item.ItemId : null;
        }

        private static void WinActiveMatch(RunState run)
        {
            run.ActiveMatch.Context.AddScore(run.PlayerId, run.ActiveMatch.Definition.TargetScore);
        }

        private static int CountCheckersOfType(GameState board, string typeId)
        {
            int count = 0;
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                foreach (BaseChecker checker in board.Nodes[i].Checkers)
                {
                    if (checker.TypeId == typeId)
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The mirror of <see cref="FixedRandom"/>: every roll comes out at its highest, so a tier roll reaches the
        /// last tier with weight and a draw takes the last candidate.
        /// </summary>
        private sealed class HighRandom : IRandomProvider
        {
            public int Range(int minInclusive, int maxExclusive) => maxExclusive - 1;

            public bool Chance(float probability) => false;
        }

        /// <summary>A charm whose effect is easy to see: the owner always rolls 6-6.</summary>
        private sealed class TestCharm : PermanentRelic
        {
            public const string Id = "relic_test_loaded_dice";

            public override string ItemId => Id;
            public override string DisplayName => "Loaded Dice";

            public override void ModifyRoll(DiceRoll roll, IRandomProvider random)
            {
                roll.Set(6, 6);
            }
        }

        #endregion
    }
}
