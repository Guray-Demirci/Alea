using System;
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
            new Area { Target = ShopTarget.Next, Rect = new RectInt(205, 81, 31, 14) }
        };

        // A dark strip under the table for item names and messages; the table has no room for them.
        private static readonly RectInt InfoStrip = new RectInt(117, 142, 124, 9);
        private static readonly Vector2Int InfoAnchor = new Vector2Int(179, 146);

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

        // Indexed by ShopTarget; null for a target the table has no place for yet.
        private readonly AreaParts[] _parts = new AreaParts[Enum.GetValues(typeof(ShopTarget)).Length];

        private ShopArtSet _art;
        private BoardLayout _layout;
        private SpriteRenderer _rollButton;
        private PixelLabel _info;
        private ShopTarget? _hovered;

        /// <summary>A shelf or button was clicked.</summary>
        public event Action<ShopTarget> Clicked;

        /// <summary>The pointer moved onto a shelf or button; null when it left them all.</summary>
        public event Action<ShopTarget?> HoverChanged;

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

            CreateRect("InfoStrip", InfoStrip, solid, InfoStripColor, SortingOrders.ShopInfoStrip);
            _info = CreateLabel("Info", RowAnchor(InfoAnchor), InfoColor, PixelTextSize.Regular);

            foreach (Area area in Areas)
            {
                GameObject hit = CreateChild(area.Target.ToString(), RectCenter(area.Rect));
                hit.AddComponent<BoxCollider2D>().size = RectSize(area.Rect);
                hit.AddComponent<ShopHitArea>().Initialize(this, area.Target);

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

        private GameObject CreateChild(string childName, Vector3 localPosition)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>A sprite placed by its pivot. A null sprite draws nothing until one is set.</summary>
        private SpriteRenderer CreateSprite(string spriteName, Vector3 localPosition, Sprite sprite, int sortingOrder)
        {
            SpriteRenderer spriteRenderer = CreateChild(spriteName, localPosition).AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        /// <summary>A solid rectangle over the given pixel area.</summary>
        private void CreateRect(string rectName, RectInt rect, Sprite solid, Color color, int sortingOrder)
        {
            SpriteRenderer rectRenderer = CreateSprite(rectName, RectCenter(rect), solid, sortingOrder);
            Vector2 size = RectSize(rect);
            rectRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            rectRenderer.color = color;
        }

        /// <summary>A label whose capitals are centred on the given position.</summary>
        private PixelLabel CreateLabel(string labelName, Vector3 localPosition, Color color, PixelTextSize size)
        {
            PixelLabel label = CreateChild(labelName, localPosition).AddComponent<PixelLabel>();
            label.Configure(_layout.PixelsPerUnit, 0, SortingOrders.ShopText, color, PixelTextAlign.Center, size);
            return label;
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
                _hovered = null;
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

        #endregion

        #region Pointer (called by ShopHitArea)

        internal void HandleClick(ShopTarget target)
        {
            Clicked?.Invoke(target);
        }

        internal void HandlePointerEnter(ShopTarget target)
        {
            _hovered = target;
            HoverChanged?.Invoke(target);
        }

        internal void HandlePointerExit(ShopTarget target)
        {
            // Moving straight from one area to the next may deliver the new Enter before the old Exit.
            if (_hovered != target)
                return;

            _hovered = null;
            HoverChanged?.Invoke(null);
        }

        #endregion
    }
}
