using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Run;

namespace Tavla.View
{
    /// <summary>
    /// CORE -> SHOP SCREEN bridge, the shop's counterpart of <see cref="HudPresenter"/>: the only place that
    /// knows WHAT the shop screen shows. Core's <see cref="Shop"/> decides what is for sale, what it costs and
    /// whether it can be bought; <see cref="ShopView"/> knows where to draw. This class turns one into the other —
    /// down to the art each item is drawn with — and turns clicks into <see cref="Shop.Buy"/> /
    /// <see cref="Shop.Reroll"/> / <see cref="Shop.Sell"/> calls; no rule is decided here.
    ///
    /// Selling: the stall opens and closes the list of the checkers Core would buy back — a row per special checker,
    /// then one row for all the extra plain checkers, with their number over the disc. A row is sold with two
    /// clicks — the first one rings it and names the price, the second one sells it (one checker of the plain row)
    /// — so a stray click never costs the player a checker. Any other click on the shop lets the ringed row go.
    ///
    /// While the shop is open the HUD's gold shows the run's purse, since no match (and so no MatchContext) is
    /// being played.
    /// </summary>
    public sealed class ShopPresenter
    {
        private static readonly ShopSlot[] Slots = (ShopSlot[])Enum.GetValues(typeof(ShopSlot));

        private readonly ShopView _view;
        private readonly ShopTexts _texts;
        private readonly CheckerArtSet _checkerArt;
        private readonly Sprite _basicCheckerArt;

        // What Core would buy back, read again on every refresh, and the sell list's rows, top to bottom: one per
        // special checker, then one for all the extra plain checkers, which stands for the one Core sells first.
        private readonly List<DeckEntry> _sellable = new List<DeckEntry>();
        private readonly List<DeckEntry> _sellRows = new List<DeckEntry>();
        private int _extraPlainCount;

        private Shop _shop;
        private RunState _run;
        private HudPresenter _hud;
        private bool _sellListOpen;

        // The checker of the sell list clicked once: the next click on it sells it.
        private DeckEntry _armedSale;

        /// <summary>The player pressed NEXT. The owner closes the shop and starts the next match.</summary>
        public event Action NextRequested;

        /// <param name="checkerArt">The special checkers' art; null draws them by their short codes.</param>
        /// <param name="basicCheckerArt">The player's plain checker, drawn on its shelf; null leaves the shelf bare.</param>
        public ShopPresenter(ShopView view, ShopTexts texts, CheckerArtSet checkerArt = null, Sprite basicCheckerArt = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _texts = texts ?? new ShopTexts();
            _checkerArt = checkerArt;
            _basicCheckerArt = basicCheckerArt;

            _view.Clicked += HandleClicked;
            _view.HoverChanged += HandleHoverChanged;
            _view.SellSlotClicked += HandleSellSlotClicked;
            _view.SellSlotHoverChanged += HandleSellSlotHoverChanged;
        }

        public bool IsShowing => _shop != null;

        /// <summary>Shows the screen for a freshly opened shop.</summary>
        /// <param name="hud">May be null when the scene has no HUD.</param>
        public void Show(Shop shop, RunState run, HudPresenter hud)
        {
            Hide();

            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _hud = hud;
            _sellListOpen = false;
            _armedSale = null;

            _shop.Changed += Refresh;
            _view.SetVisible(true);
            _view.SetInfo(_texts.Hint);

            if (_hud != null)
                _hud.SetStatus(_texts.HudTask);

            Refresh();
        }

        /// <summary>Hides the screen and lets go of the shop. Does not close the shop itself.</summary>
        public void Hide()
        {
            if (_shop != null)
                _shop.Changed -= Refresh;

            _shop = null;
            _run = null;
            _hud = null;
            _sellable.Clear();
            _sellRows.Clear();
            _armedSale = null;

            // During scene teardown the view may already be destroyed (Unity's == reports that as null).
            if (_view != null)
                _view.SetVisible(false);
        }

        /// <summary>Disconnects from the view for good (scene teardown).</summary>
        public void Dispose()
        {
            Hide();
            _view.Clicked -= HandleClicked;
            _view.HoverChanged -= HandleHoverChanged;
            _view.SellSlotClicked -= HandleSellSlotClicked;
            _view.SellSlotHoverChanged -= HandleSellSlotHoverChanged;
        }

        #region Refresh

        /// <summary>
        /// Re-reads every shelf from Core — what is on it, its price and whether the player can afford it — and the
        /// sell list.
        /// </summary>
        private void Refresh()
        {
            if (_shop == null)
                return;

            foreach (ShopSlot slot in Slots)
            {
                ShopTarget target = (ShopTarget)slot;
                ShopOffer offer = _shop.GetOffer(slot);
                bool forSale = offer != null && !offer.IsSold;

                // A sold shelf keeps its cushion's colour: the tier of what was there stays readable.
                _view.SetShelfTier(target, offer != null ? offer.Item.Tier : 0);
                _view.SetShelfItem(target, forSale ? ArtOf(offer.Item) : null, forSale ? CodeOf(offer.Item) : string.Empty);
                _view.SetPrice(target, forSale ? offer.Price.ToString() : string.Empty,
                               _shop.CheckPurchase(slot) != PurchaseResult.NotEnoughGold);
            }

            int? rerollPrice = _shop.NextRerollPrice;
            _view.SetRerollEnabled(rerollPrice.HasValue);
            _view.SetPrice(ShopTarget.Reroll, rerollPrice.HasValue ? rerollPrice.Value.ToString() : string.Empty,
                           rerollPrice.HasValue && _run.Gold >= rerollPrice.Value);

            if (_hud != null)
                _hud.ShowGold(_run.Gold);

            RefreshSellList();
        }

        /// <summary>
        /// Re-reads the checkers Core would buy back and draws them, if the list is open. The list closes by itself
        /// once nothing is left to sell, and a ringed checker that left the deck is no longer ringed.
        /// </summary>
        private void RefreshSellList()
        {
            _shop.GetSellableCheckers(_sellable);

            _sellRows.Clear();
            _extraPlainCount = 0;
            foreach (DeckEntry entry in _sellable)
            {
                if (!entry.IsSpecial && _extraPlainCount++ > 0)
                    continue;

                _sellRows.Add(entry);
            }

            if (_sellRows.Count == 0)
                _sellListOpen = false;

            if (_armedSale != null && !_sellRows.Contains(_armedSale))
                _armedSale = null;

            _view.SetSellPanelVisible(_sellListOpen);
            if (!_sellListOpen)
                return;

            Sprite ring = _checkerArt != null ? _checkerArt.SelectedRing : null;

            _view.SetSellSlotCount(_sellRows.Count);
            for (int i = 0; i < _sellRows.Count; i++)
            {
                DeckEntry entry = _sellRows[i];
                _view.SetSellSlot(i, ArtOf(entry), LabelOf(entry),
                                  string.Format(_texts.SellPrice, _shop.GetSellPrice(entry)),
                                  entry == _armedSale ? ring : null);
            }
        }

        #endregion

        #region View listeners

        private void HandleClicked(ShopTarget target)
        {
            if (_shop == null)
                return;

            DisarmSale();

            switch (target)
            {
                case ShopTarget.Next:
                    NextRequested?.Invoke();
                    return;

                case ShopTarget.Reroll:
                    RerollResult reroll = _shop.Reroll();
                    _view.SetInfo(reroll == RerollResult.Success ? DescribeReroll() : DescribeFailure(reroll));
                    return;

                case ShopTarget.Sell:
                    ToggleSellList();
                    return;
            }

            ShopSlot slot = (ShopSlot)target;
            ShopOffer offer = _shop.GetOffer(slot);
            PurchaseResult result = _shop.Buy(slot);

            _view.SetInfo(result == PurchaseResult.Success
                ? string.Format(_texts.Bought, NameOf(offer.Item))
                : DescribeFailure(result, offer));
        }

        private void HandleHoverChanged(ShopTarget? target)
        {
            if (_shop == null)
                return;

            if (!target.HasValue)
            {
                _view.SetInfo(_texts.Hint);
                return;
            }

            switch (target.Value)
            {
                case ShopTarget.Next:
                    _view.SetInfo(_texts.Next);
                    return;

                case ShopTarget.Reroll:
                    _view.SetInfo(DescribeReroll());
                    return;

                case ShopTarget.Sell:
                    _view.SetInfo(_texts.SellButton);
                    return;
            }

            _view.SetInfo(DescribeOffer(_shop.GetOffer((ShopSlot)target.Value)));
        }

        /// <summary>A checker of the sell list was clicked: the first click rings it, a second one sells it.</summary>
        private void HandleSellSlotClicked(int index)
        {
            if (_shop == null || !_sellListOpen || index < 0 || index >= _sellRows.Count)
                return;

            DeckEntry entry = _sellRows[index];
            if (entry != _armedSale)
            {
                _armedSale = entry;
                RefreshSellList();
                _view.SetInfo(DescribeSale(_texts.SellConfirm, entry));
                return;
            }

            // Read before the sale: afterwards the checker is gone from the deck and the list.
            string sold = DescribeSale(_texts.SoldChecker, entry);
            SaleResult result = _shop.Sell(entry);
            _view.SetInfo(result == SaleResult.Success ? sold : string.Empty);
        }

        private void HandleSellSlotHoverChanged(int? index)
        {
            if (_shop == null)
                return;

            if (!index.HasValue || index.Value >= _sellRows.Count)
            {
                _view.SetInfo(_sellListOpen ? _texts.SellHint : _texts.Hint);
                return;
            }

            DeckEntry entry = _sellRows[index.Value];
            _view.SetInfo(DescribeSale(entry == _armedSale ? _texts.SellConfirm : _texts.SellOffer, entry));
        }

        #endregion

        #region Selling

        /// <summary>The stall: opens the sell list — unless there is nothing to sell — or closes it.</summary>
        private void ToggleSellList()
        {
            if (!_sellListOpen && _sellRows.Count == 0)
            {
                _view.SetInfo(_texts.NothingToSell);
                return;
            }

            _sellListOpen = !_sellListOpen;
            RefreshSellList();
            _view.SetInfo(_sellListOpen ? _texts.SellHint : _texts.SellButton);
        }

        /// <summary>Lets the ringed checker of the sell list go.</summary>
        private void DisarmSale()
        {
            if (_armedSale == null)
                return;

            _armedSale = null;
            RefreshSellList();
        }

        #endregion

        #region Formatting

        private string DescribeOffer(ShopOffer offer)
        {
            if (offer == null)
                return _texts.Empty;

            string name = NameOf(offer.Item);
            if (offer.IsSold)
                return string.Format(_texts.Sold, name);

            return offer.Item.Kind == ShopItemKind.SpecialChecker
                ? string.Format(_texts.SpecialChecker, name, offer.Price, offer.Item.Tier)
                : string.Format(_texts.Item, name, offer.Price);
        }

        private string DescribeReroll()
        {
            int? price = _shop.NextRerollPrice;
            return price.HasValue ? string.Format(_texts.Reroll, price.Value) : _texts.NoRerollsLeft;
        }

        private string DescribeFailure(PurchaseResult result, ShopOffer offer)
        {
            switch (result)
            {
                case PurchaseResult.NotEnoughGold: return _texts.NotEnoughGold;
                case PurchaseResult.DeckFull: return _texts.DeckFull;
                case PurchaseResult.TooManySpecialCheckers:
                    return string.Format(_texts.TooManySpecialCheckers, _run.Definition.Shop.MaxSpecialCheckers);
                case PurchaseResult.AlreadyOwned: return _texts.AlreadyOwned;
                case PurchaseResult.AlreadySold: return string.Format(_texts.Sold, NameOf(offer.Item));
                default: return _texts.Empty;
            }
        }

        private string DescribeFailure(RerollResult result)
        {
            switch (result)
            {
                case RerollResult.NotEnoughGold: return _texts.NotEnoughGold;
                case RerollResult.NoRerollsLeft: return _texts.NoRerollsLeft;
                default: return string.Empty;
            }
        }

        /// <summary>A sell list line: {0} the checker's name, {1} what it sells for.</summary>
        private string DescribeSale(string format, DeckEntry entry)
        {
            return string.Format(format, NameOf(entry), _shop.GetSellPrice(entry));
        }

        /// <summary>The item's name in capitals, like the rest of the shop's text.</summary>
        private string NameOf(ShopItem item)
        {
            if (item.Kind == ShopItemKind.BasicChecker)
                return _texts.BasicChecker;

            return item.DisplayName.ToUpperInvariant();
        }

        /// <summary>A deck checker's name on the sell list: a special one's as the shop sold it.</summary>
        private string NameOf(DeckEntry entry)
        {
            if (!entry.IsSpecial)
                return _texts.ExtraChecker;

            ShopItem item = CatalogItemOf(entry);
            return item != null ? NameOf(item) : entry.TypeId.ToUpperInvariant();
        }

        /// <summary>A deck checker's art on the sell list, as its shelf showed it; null for one without art.</summary>
        private Sprite ArtOf(DeckEntry entry)
        {
            ShopItem item = CatalogItemOf(entry);
            return item != null ? ArtOf(item) : null;
        }

        /// <summary>
        /// The text over a row's disc: how many extra plain checkers can be sold, or the code of a special checker
        /// without art.
        /// </summary>
        private string LabelOf(DeckEntry entry)
        {
            if (!entry.IsSpecial)
                return string.Format(_texts.SellCount, _extraPlainCount);

            ShopItem item = CatalogItemOf(entry);
            return item != null && ArtOf(item) == null ? CodeOf(item) : string.Empty;
        }

        /// <summary>The catalog's item for a checker of the deck; null for a special checker the catalog does not stock.</summary>
        private ShopItem CatalogItemOf(DeckEntry entry)
        {
            if (!entry.IsSpecial)
                return _run.Definition.Shop.BasicChecker;

            foreach (ShopItem item in _run.Definition.Shop.SpecialCheckers)
            {
                if (item.CheckerTypeId == entry.TypeId)
                    return item;
            }

            return null;
        }

        private CheckerSkin SkinOf(string typeId)
        {
            return _checkerArt != null ? _checkerArt.GetSkin(typeId) : null;
        }

        /// <summary>
        /// The art an item is drawn with on its shelf: the player's own checker for the plain one, the checker's art
        /// for a special one. Null for an item without art, which then shows its code.
        /// </summary>
        private Sprite ArtOf(ShopItem item)
        {
            switch (item.Kind)
            {
                case ShopItemKind.BasicChecker:
                    return _basicCheckerArt;

                case ShopItemKind.SpecialChecker:
                    CheckerSkin skin = SkinOf(item.CheckerTypeId);
                    return skin != null ? skin.Normal : null;

                default:
                    return null;
            }
        }

        /// <summary>The short code of a special checker without art; other items have none.</summary>
        private string CodeOf(ShopItem item)
        {
            if (item.Kind != ShopItemKind.SpecialChecker)
                return string.Empty;

            if (_texts.Codes != null)
            {
                foreach (ShopTexts.ItemCode code in _texts.Codes)
                {
                    if (code != null && code.ItemId == item.ItemId)
                        return code.Code;
                }
            }

            string name = item.DisplayName.ToUpperInvariant();
            return name.Length > 2 ? name.Substring(0, 2) : name;
        }

        #endregion
    }
}
