namespace Tavla.View
{
    /// <summary>
    /// Every sorting order used by the View, in one place.
    ///
    /// The layers are relative to each other
    /// (background &lt; board &lt; points &lt; tiles &lt; dice cards &lt; marks &lt; tray &lt; highlight &lt; checkers &lt;
    /// volley marks &lt; breaking checkers &lt; tile counts &lt; bar &lt; HUD),
    /// so scattering the numbers across files made it easy to break the stacking by editing one of them.
    /// Values that are also exposed in the Inspector use these constants as their defaults.
    /// </summary>
    internal static class SortingOrders
    {
        /// <summary>Full-screen background behind the board.</summary>
        public const int ScreenBackground = -200;

        /// <summary>Board artwork.</summary>
        public const int BoardBackground = -100;

        /// <summary>The 24 points (triangles), drawn on the board.</summary>
        public const int BoardPoints = -90;

        /// <summary>Tiles, each drawn over its whole point: above the points, below everything that lies on them.</summary>
        public const int Tiles = -88;

        /// <summary>
        /// The dice cards in the strip between the two rows: above the points, below the marks, which reach into the
        /// strip over the cards' ends and must stay whole.
        /// </summary>
        public const int DiceCards = -85;

        /// <summary>Score marks past the points' tips (Navigator footprints): above the points, below the tray.</summary>
        public const int PointMark = -80;

        /// <summary>Always-visible bear-off tray: above the board, below highlights.</summary>
        public const int BearOffTray = -50;

        /// <summary>Legal-target highlight on a point: above the board, below checkers.</summary>
        public const int NodeHighlight = 0;

        /// <summary>Checkers on a point; the stack index is added on top.</summary>
        public const int CheckerBase = 10;

        /// <summary>
        /// The mark over a checker a volley can hit or is aimed at (Bowman): above every checker on the points, so a
        /// buried target shows through the stack; below a checker being dragged.
        /// </summary>
        public const int VolleyMark = 90;

        /// <summary>
        /// A hit checker breaking apart where it stood (<see cref="HitEffects"/>): above the checkers on the points and
        /// the volley marks, so it is seen over the capturer that has just landed on it; below a checker being dragged.
        /// Where it puts itself back together it takes the place its checker has there.
        /// </summary>
        public const int CheckerBreak = 95;

        /// <summary>
        /// A tile's countdown near its point's tip (Powder Keg): above the checkers stacked on the point, so a full
        /// stack never hides it; below a checker being dragged.
        /// </summary>
        public const int TileCount = 100;

        /// <summary>Checkers on the bar; the stack index is added on top.</summary>
        public const int BarCheckerBase = 200;

        /// <summary>Added to a checker while it is dragged so it floats above everything on the board.</summary>
        public const int DragBoost = 500;

        /// <summary>SortingGroup order of the HUD panel (in front of the checkers).</summary>
        public const int HudPanel = 200;

        /// <summary>HUD text inside the panel's SortingGroup (above the importer's layer orders).</summary>
        public const int HudTextInGroup = 100;

        /// <summary>The shop's Merchant Table: above the board, the bar and the HUD.</summary>
        public const int ShopTable = 300;

        /// <summary>What stands on the table: the shelf cushions, the ROLL button and the sell stall beside the board.</summary>
        public const int ShopShelf = 305;

        /// <summary>The glow of the sell stall while the pointer is on it (<see cref="HoverGlow"/>): over the stall.</summary>
        public const int ShopShelfGlow = 306;

        /// <summary>The dark backing of the list of checkers to sell: behind the checkers on it.</summary>
        public const int ShopSellPanel = 307;

        /// <summary>The checkers for sale, lying on their cushions, and the player's checkers on the sell list.</summary>
        public const int ShopItemArt = 310;

        /// <summary>The ring around the checker the player is about to sell: over its art.</summary>
        public const int ShopItemRing = 312;

        /// <summary>The dark strip under the table that carries the shop's info line.</summary>
        public const int ShopInfoStrip = 315;

        /// <summary>Prices, item codes and the info line of the shop.</summary>
        public const int ShopText = 320;
    }
}
