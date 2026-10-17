using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Run;

namespace Tavla.View
{
    /// <summary>
    /// The shop screen, the Merchant Table (in_game_shop_ready): the table art, a clickable area per shelf and
    /// button, and what lies on them — cushions, the checkers for sale and their prices. DUMB PUPPET like
    /// <see cref="HudView"/>: it does not know what is for sale or what anything costs — <see cref="ShopPresenter"/>
    /// tells it what to show; it only knows WHERE and how.
    ///
    /// The table and the ROLL button are full 320x180 canvases like the board, so they sit at the origin. A special
    /// checker shelf is a cushion with a price tag hanging from it, and the cushion's colour shows the tier of the
    /// checker on it, so its sprite is swapped per tier (<see cref="ShopArtSet"/>). Cushion art and checker art both
    /// pivot on the centre of the cushion's disc, so that one point places both.
    ///
    /// The areas below were measured pixel by pixel from the scheme (x right, y DOWN, as in Aseprite). If the artist
    /// moves a shelf or a button, update its entry in <see cref="Areas"/>. The card and charm shelves have no place
    /// on the table yet: Core still stocks them, and every call about them is ignored until the art adds them.
    ///
    /// SELLING: the merchant's stall in the bottom right corner, beside the board, is a button (a full canvas, like
    /// ROLL). It opens a list of the player's checkers in the sunken panel of the frame right above it, one row each
    /// — the checker on the left with a short text over it, what it sells for on the right, on a dark backing.
    /// Which checkers are listed, and what clicking one does, is the presenter's business. The stall glows while the
    /// pointer is on it (<see cref="HoverGlow"/>), so it reads as something to click.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopView : MonoBehaviour
    {
        // In front of the board (z 0) and the checkers (z -1): the shop's colliders must win every click.
        private const float RootZ = -5f;

        private static readonly Vector2Int NoAnchor = new Vector2Int(-1, -1);

        private static readonly Color PriceColor = Color.black;
        private static readonly Color UnaffordableColor = new Color32(200, 40, 40, 255);

        // Light enough to read on every cushion's disc, from tier 1's brown to tier 3's dark purple.
        private static readonly Color CodeColor = new Color32(240, 228, 200, 255);

        private static readonly Color InfoColor = Color.white;
        private static readonly Color InfoStripColor = new Color(0f, 0f, 0f, 0.75f);

        // Greys out the ROLL button once no reroll is left.
        private static readonly Color DisabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        /// <summary>
        /// One shelf or button. Pixel anchors are rows and columns of the canvas; points are corners between
        /// pixels. A label is centred horizontally on its anchor and its capitals vertically on the anchor row.
        /// </summary>
        private sealed class Area
        {
            public ShopTarget Target;

            /// <summary>The clickable box.</summary>
            public RectInt Rect;

            /// <summary>
            /// Centre of the cushion's disc, where the item for sale lies: a point between four pixels.
            /// <see cref="NoAnchor"/> for a button.
            /// </summary>
            public Vector2Int Item = NoAnchor;

            /// <summary>Is the cushion drawn here in its tier's colour? The plain checker's is part of the table.</summary>
            public bool TierCushion;

            /// <summary>The price, in the empty part of the hanging tag — never on its yellow diamond.</summary>
            public Vector2Int Price = NoAnchor;
        }

        // Every special checker shelf is the same art (tierNyastık_wprice): a 22x21 cushion with its 16x17 tag
        // hanging below. Offsets from the shelf's top-left pixel:
        private static readonly Vector2Int ShelfSize = new Vector2Int(22, 38);
        private static readonly Vector2Int ShelfDisc = new Vector2Int(11, 10);
        private static readonly Vector2Int ShelfTagPrice = new Vector2Int(8, 27);

        // The plain checker's cushion, part of the table art. Same cushion as a shelf's, with the tag to its right.
        private static readonly Vector2Int BasicCushion = new Vector2Int(210, 113);

        private static readonly Area[] Areas =
        {
            SpecialShelf(ShopTarget.SpecialChecker1, 117, 63),
            SpecialShelf(ShopTarget.SpecialChecker2, 149, 63),
            SpecialShelf(ShopTarget.SpecialChecker3, 181, 63),

            new Area
            {
                Target = ShopTarget.BasicChecker,
                Rect = new RectInt(BasicCushion.x, BasicCushion.y, 37, 21),
                Item = BasicCushion + ShelfDisc,
                Price = new Vector2Int(239, 119)
            },

            // ROLL: the next reroll price goes in the tag hanging to the right of the button.
            new Area { Target = ShopTarget.Reroll, Rect = new RectInt(205, 64, 45, 14), Price = new Vector2Int(242, 69) },
            new Area { Target = ShopTarget.Next, Rect = new RectInt(205, 81, 31, 14) },

            // The stall, on the frame below the sunken panel that holds the sell list.
            new Area { Target = ShopTarget.Sell, Rect = new RectInt(282, 135, 35, 36) }
        };

        // A dark strip under the table for item names and messages; the table has no room for them.
        private static readonly RectInt InfoStrip = new RectInt(117, 142, 124, 9);
        private static readonly Vector2Int InfoAnchor = new Vector2Int(179, 146);

        // The sell list: the bright wood inside the frame's sunken panel above the stall (its shaded rim, x 275-316
        // and y 36-124, stays visible as a border). Rows run down from the top, each one a clickable box as wide as
        // the panel. Five rows of 16 px fill it — the deck holds five special checkers at most — and more rows
        // squeeze closer. The checker's disc (12 px) is centred in its row; x below are pixel boundaries.
        private static readonly RectInt SellPanel = new RectInt(277, 40, 36, 80);
        private const int SellRowPitch = 16;
        private const int SellDiscX = 285;
        private const int SellPriceX = 302;

        private static readonly Color SellPanelColor = new Color(0f, 0f, 0f, 0.6f);

        // The yellow of the price tags' diamonds: what the checker brings, in gold.
        private static readonly Color SellPriceColor = new Color32(229, 220, 6, 255);

        private static Area SpecialShelf(ShopTarget target, int x, int y)
        {
            Vector2Int origin = new Vector2Int(x, y);
            return new Area
            {
                Target = target,
                Rect = new RectInt(origin, ShelfSize),
                Item = origin + ShelfDisc,
                TierCushion = true,
                Price = origin + ShelfTagPrice
            };
        }

        private sealed class AreaParts
        {
            public SpriteRenderer Cushion;
            public SpriteRenderer Item;
            public PixelLabel Code;
            public PixelLabel Price;
        }

        /// <summary>One row of the sell list. Its root is the row's clickable box, centred on the row.</summary>
        private sealed class SellSlot
        {
            public GameObject Root;
            public BoxCollider2D Hit;
            public SpriteRenderer Art;
            public SpriteRenderer Ring;
            public PixelLabel Label;
            public PixelLabel Price;
        }

        // Indexed by ShopTarget; null for a target the table has no place for yet.
        private readonly AreaParts[] _parts = new AreaParts[Enum.GetValues(typeof(ShopTarget)).Length];

        // Made on demand, never destroyed: a row the list no longer needs is only switched off.
        private readonly List<SellSlot> _sellSlots = new List<SellSlot>();

        private ShopArtSet _art;
        private BoardLayout _layout;
        private SpriteRenderer _rollButton;
        private HoverGlow _sellGlow;
        private PixelLabel _info;
        private GameObject _sellPanel;
        private int _sellSlotCount;
        private ShopTarget? _hovered;
        private int? _hoveredSellSlot;

        /// <summary>A shelf or button was clicked.</summary>
        public event Action<ShopTarget> Clicked;

        /// <summary>The pointer moved onto a shelf or button; null when it left them all.</summary>
        public event Action<ShopTarget?> HoverChanged;

        /// <summary>A row of the sell list was clicked; the row's index, from the top.</summary>
        public event Action<int> SellSlotClicked;

        /// <summary>The pointer moved onto a row of the sell list; null when it left them all.</summary>
        public event Action<int?> SellSlotHoverChanged;

        #region Building

        /// <summary>Builds the screen in code from the shop art; it starts hidden.</summary>
        /// <param name="solid">1x1 white sprite (1 world unit) used for the info strip.</param>
        public static ShopView Create(ShopArtSet art, Sprite solid, BoardLayout layout)
        {
            if (art == null)
                throw new ArgumentNullException(nameof(art));

            GameObject root = new GameObject("Shop");
            root.transform.position = new Vector3(0f, 0f, RootZ);

            ShopView view = root.AddComponent<ShopView>();
            view.Build(art, solid, layout);
            root.SetActive(false);
            return view;
        }

        private void Build(ShopArtSet art, Sprite solid, BoardLayout layout)
        {
            _art = art;
            _layout = layout;

            CreateSprite("Table", Vector3.zero, art.Table, SortingOrders.ShopTable);
            _rollButton = CreateSprite("RollButton", Vector3.zero, art.RollButton, SortingOrders.ShopShelf);
            CreateSprite("SellButton", Vector3.zero, art.SellButton, SortingOrders.ShopShelf);
            _sellGlow = HoverGlow.Create("SellButtonGlow", transform, art.SellButton, SortingOrders.ShopShelfGlow);

            CreateRect("InfoStrip", InfoStrip, solid, InfoStripColor, SortingOrders.ShopInfoStrip);
            _info = CreateLabel("Info", RowAnchor(InfoAnchor), InfoColor, PixelTextSize.Regular);

            _sellPanel = CreateChild("SellPanel", Vector3.zero);
            CreateRect("Backing", SellPanel, solid, SellPanelColor, SortingOrders.ShopSellPanel, _sellPanel.transform);
            _sellPanel.SetActive(false);

            foreach (Area area in Areas)
            {
                ShopTarget target = area.Target;
                GameObject hit = CreateChild(target.ToString(), RectCenter(area.Rect));
                hit.AddComponent<BoxCollider2D>().size = RectSize(area.Rect);
                hit.AddComponent<ShopHitArea>().Initialize(() => HandleClick(target), () => HandlePointerEnter(target),
                                                           () => HandlePointerExit(target));

                AreaParts parts = new AreaParts();

                if (area.Item != NoAnchor)
                {
                    Vector3 disc = _layout.PixelToWorld(area.Item.x, area.Item.y);

                    if (area.TierCushion)
                        parts.Cushion = CreateSprite("Cushion_" + area.Target, disc, art.GetCushion(ShopItem.MinTier), SortingOrders.ShopShelf);

                    parts.Item = CreateSprite("Item_" + area.Target, disc, null, SortingOrders.ShopItemArt);
                    parts.Code = CreateLabel("Code_" + area.Target, disc, CodeColor, PixelTextSize.Small);
                }

                // Tags are too small for regular text.
                if (area.Price != NoAnchor)
                    parts.Price = CreateLabel("Price_" + area.Target, RowAnchor(area.Price), PriceColor, PixelTextSize.Small);

                _parts[(int)area.Target] = parts;
            }
        }

        /// <param name="parent">Null: the shop's root.</param>
        private GameObject CreateChild(string childName, Vector3 localPosition, Transform parent = null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>A sprite placed by its pivot. A null sprite draws nothing until one is set.</summary>
        private SpriteRenderer CreateSprite(string spriteName, Vector3 localPosition, Sprite sprite, int sortingOrder,
                                            Transform parent = null)
        {
            SpriteRenderer spriteRenderer = CreateChild(spriteName, localPosition, parent).AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        /// <summary>A solid rectangle over the given pixel area.</summary>
        private void CreateRect(string rectName, RectInt rect, Sprite solid, Color color, int sortingOrder,
                                Transform parent = null)
        {
            SpriteRenderer rectRenderer = CreateSprite(rectName, RectCenter(rect), solid, sortingOrder, parent);
            Vector2 size = RectSize(rect);
            rectRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            rectRenderer.color = color;
        }

        /// <summary>A label whose capitals are centred on the given position.</summary>
        private PixelLabel CreateLabel(string labelName, Vector3 localPosition, Color color, PixelTextSize size,
                                       Transform parent = null)
        {
            PixelLabel label = CreateChild(labelName, localPosition, parent).AddComponent<PixelLabel>();
            label.Configure(_layout.PixelsPerUnit, 0, SortingOrders.ShopText, color, PixelTextAlign.Center, size);
            return label;
        }

        /// <summary>A new row for the sell list, inside the panel; <see cref="SetSellSlotCount"/> places it.</summary>
        private SellSlot CreateSellSlot(int index)
        {
            SellSlot slot = new SellSlot { Root = CreateChild("SellSlot_" + index, Vector3.zero, _sellPanel.transform) };
            slot.Hit = slot.Root.AddComponent<BoxCollider2D>();
            slot.Root.AddComponent<ShopHitArea>().Initialize(() => HandleSellSlotClick(index),
                                                             () => HandleSellSlotEnter(index),
                                                             () => HandleSellSlotExit(index));

            // The root sits on the panel's middle column; the disc and the price are offsets from it.
            float panelMiddle = SellPanel.x + SellPanel.width * 0.5f;
            Vector3 disc = new Vector3((SellDiscX - panelMiddle) / _layout.PixelsPerUnit, 0f, 0f);
            Vector3 price = new Vector3((SellPriceX - panelMiddle) / _layout.PixelsPerUnit, 0f, 0f);
            Transform root = slot.Root.transform;

            slot.Art = CreateSprite("Art", disc, null, SortingOrders.ShopItemArt, root);
            slot.Ring = CreateSprite("Ring", disc, null, SortingOrders.ShopItemRing, root);
            slot.Label = CreateLabel("Label", disc, CodeColor, PixelTextSize.Small, root);
            slot.Price = CreateLabel("Price", price, SellPriceColor, PixelTextSize.Regular, root);
            return slot;
        }

        /// <summary>Where a label anchored on this pixel goes: the left edge of its column, the middle of its row.</summary>
        private Vector3 RowAnchor(Vector2Int anchor)
        {
            return _layout.PixelToWorld(anchor.x, anchor.y + 0.5f);
        }

        private Vector3 RectCenter(RectInt rect)
        {
            return _layout.PixelToWorld(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        }

        private Vector2 RectSize(RectInt rect)
        {
            return new Vector2(rect.width, rect.height) / _layout.PixelsPerUnit;
        }

        #endregion

        #region Presenter API

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);

            // A hidden area never reports the pointer leaving, so forget it here.
            if (!visible)
            {
                _hovered = null;
                _hoveredSellSlot = null;
                UpdateGlow();
            }
        }

        /// <summary>
        /// Colours a special checker shelf's cushion by the tier of what is (or was) on it. 0 is an empty shelf,
        /// drawn with the plain tier 1 cushion. Ignored by a shelf without a cushion of its own.
        /// </summary>
        public void SetShelfTier(ShopTarget shelf, int tier)
        {
            SpriteRenderer cushion = PartsOf(shelf)?.Cushion;
            if (cushion != null)
                cushion.sprite = _art.GetCushion(Mathf.Clamp(tier, ShopItem.MinTier, ShopItem.MaxTier));
        }

        /// <summary>
        /// What lies on a shelf: the item's art, or — for an item without art — its short code. Null art and an
        /// empty code leave the cushion bare (a sold or empty shelf).
        /// </summary>
        public void SetShelfItem(ShopTarget shelf, Sprite art, string code)
        {
            AreaParts parts = PartsOf(shelf);
            if (parts == null || parts.Item == null)
                return;

            parts.Item.sprite = art;
            parts.Code.SetText(art == null ? (code ?? string.Empty) : string.Empty);
        }

        /// <summary>The price on a shelf's or the ROLL button's tag. Empty text hides it.</summary>
        public void SetPrice(ShopTarget target, string text, bool affordable)
        {
            PixelLabel price = PartsOf(target)?.Price;
            if (price == null)
                return;

            price.SetText(text);
            price.SetColor(affordable ? PriceColor : UnaffordableColor);
        }

        /// <summary>The ROLL button greys out once no reroll is left.</summary>
        public void SetRerollEnabled(bool enabled)
        {
            _rollButton.color = enabled ? Color.white : DisabledColor;
        }

        /// <summary>The line under the table: item names, hints and messages.</summary>
        public void SetInfo(string text)
        {
            _info.SetText(text);
        }

        private AreaParts PartsOf(ShopTarget target)
        {
            return _parts[(int)target];
        }

        /// <summary>Opens or closes the sell list in the panel above the stall.</summary>
        public void SetSellPanelVisible(bool visible)
        {
            _sellPanel.SetActive(visible);

            if (!visible)
                _hoveredSellSlot = null;
        }

        /// <summary>
        /// How many rows the sell list shows, from the top of the panel. Up to five rows keep their full height;
        /// more squeeze closer so that they all fit.
        /// </summary>
        public void SetSellSlotCount(int count)
        {
            _sellSlotCount = Mathf.Max(0, count);

            // An even pitch keeps the 12 px disc centred on whole pixels.
            int pitch = SellRowPitch;
            if (_sellSlotCount * pitch > SellPanel.height)
                pitch = Mathf.Max(2, (SellPanel.height / _sellSlotCount) & ~1);

            while (_sellSlots.Count < _sellSlotCount)
                _sellSlots.Add(CreateSellSlot(_sellSlots.Count));

            for (int i = 0; i < _sellSlots.Count; i++)
            {
                SellSlot slot = _sellSlots[i];
                bool shown = i < _sellSlotCount;
                slot.Root.SetActive(shown);

                if (!shown)
                    continue;

                int top = SellPanel.y + i * pitch;
                slot.Root.transform.localPosition =
                    _layout.PixelToWorld(SellPanel.x + SellPanel.width * 0.5f, top + pitch * 0.5f);
                slot.Hit.size = new Vector2(SellPanel.width, pitch) / _layout.PixelsPerUnit;
            }

            // A row switched off never reports the pointer leaving.
            if (_hoveredSellSlot >= _sellSlotCount)
                _hoveredSellSlot = null;
        }

        /// <summary>
        /// One row of the sell list: the checker's art, a short text over its disc (a code for a checker without
        /// art, or how many there are; empty for none), what it sells for, and the ring drawn around it (null for none).
        /// </summary>
        public void SetSellSlot(int index, Sprite art, string label, string price, Sprite ring)
        {
            if (index < 0 || index >= _sellSlotCount)
                return;

            SellSlot slot = _sellSlots[index];
            slot.Art.sprite = art;
            slot.Label.SetText(label);
            slot.Price.SetText(price);
            slot.Ring.sprite = ring;
        }

        #endregion

        #region Pointer (called by the hit areas)

        private void HandleClick(ShopTarget target)
        {
            Clicked?.Invoke(target);
        }

        private void HandlePointerEnter(ShopTarget target)
        {
            _hovered = target;
            UpdateGlow();
            HoverChanged?.Invoke(target);
        }

        private void HandlePointerExit(ShopTarget target)
        {
            // Moving straight from one area to the next may deliver the new Enter before the old Exit.
            if (_hovered != target)
                return;

            _hovered = null;
            UpdateGlow();
            HoverChanged?.Invoke(null);
        }

        /// <summary>The stall glows while the pointer is on it.</summary>
        private void UpdateGlow()
        {
            if (_sellGlow != null)
                _sellGlow.SetShown(_hovered == ShopTarget.Sell);
        }

        private void HandleSellSlotClick(int index)
        {
            SellSlotClicked?.Invoke(index);
        }

        private void HandleSellSlotEnter(int index)
        {
            _hoveredSellSlot = index;
            SellSlotHoverChanged?.Invoke(index);
        }

        private void HandleSellSlotExit(int index)
        {
            // The rows touch, so the next row's Enter may come before this one's Exit.
            if (_hoveredSellSlot != index)
                return;

            _hoveredSellSlot = null;
            SellSlotHoverChanged?.Invoke(null);
        }

        #endregion
    }
}
