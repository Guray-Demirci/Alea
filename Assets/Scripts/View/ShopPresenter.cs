using System;
using UnityEngine;
using Tavla.Core.Run;

namespace Tavla.View
{
    /// <summary>
    /// CORE -> SHOP SCREEN bridge, the shop's counterpart of <see cref="HudPresenter"/>: the only place that
    /// knows WHAT the shop screen shows. Core's <see cref="Shop"/> decides what is for sale, what it costs and
    /// whether it can be bought; <see cref="ShopView"/> knows where to draw. This class turns one into the other —
    /// down to the art each item is drawn with — and turns clicks into <see cref="Shop.Buy"/> /
    /// <see cref="Shop.Reroll"/> calls; no rule is decided here.
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

        private Shop _shop;
        private RunState _run;
        private HudPresenter _hud;

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

            _shop.Changed += Refresh;
            _view.SetVisible(true);
            _view.SetInfo(_texts.Hint);

            if (_hud != null)
                _hud.SetTask(_texts.HudTask);

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
        }

        #region Refresh

        /// <summary>Re-reads every shelf from Core: what is on it, its price and whether the player can afford it.</summary>
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
        }

        #endregion

        #region View listeners

        private void HandleClicked(ShopTarget target)
        {
            if (_shop == null)
                return;

            switch (target)
            {
                case ShopTarget.Next:
                    NextRequested?.Invoke();
                    return;

                case ShopTarget.Reroll:
                    RerollResult reroll = _shop.Reroll();
                    _view.SetInfo(reroll == RerollResult.Success ? DescribeReroll() : DescribeFailure(reroll));
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
            }

            _view.SetInfo(DescribeOffer(_shop.GetOffer((ShopSlot)target.Value)));
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

        /// <summary>The item's name in capitals, like the rest of the shop's text.</summary>
        private string NameOf(ShopItem item)
        {
            if (item.Kind == ShopItemKind.BasicChecker)
                return _texts.BasicChecker;

            return item.DisplayName.ToUpperInvariant();
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
                    CheckerSkin skin = _checkerArt != null ? _checkerArt.GetSkin(item.CheckerTypeId) : null;
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
