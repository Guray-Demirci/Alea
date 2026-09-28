namespace Tavla.View
{
    /// <summary>
    /// Every sorting order used by the View, in one place.
    ///
    /// The layers are relative to each other (background &lt; tray &lt; highlight &lt; checkers &lt; bar &lt; HUD),
    /// so scattering the numbers across files made it easy to break the stacking by editing one of them.
    /// Values that are also exposed in the Inspector use these constants as their defaults.
    /// </summary>
    internal static class SortingOrders
    {
        /// <summary>Full-screen background behind the board.</summary>
        public const int ScreenBackground = -200;

        /// <summary>Board artwork.</summary>
        public const int BoardBackground = -100;

        /// <summary>Always-visible bear-off tray: above the board, below highlights.</summary>
        public const int BearOffTray = -50;

        /// <summary>Legal-target highlight on a point: above the board, below checkers.</summary>
        public const int NodeHighlight = 0;

        /// <summary>Checkers on a point; the stack index is added on top.</summary>
        public const int CheckerBase = 10;

        /// <summary>Checkers on the bar; the stack index is added on top.</summary>
        public const int BarCheckerBase = 200;

        /// <summary>Added to a checker while it is dragged so it floats above everything on the board.</summary>
        public const int DragBoost = 500;

        /// <summary>SortingGroup order of the HUD panel (in front of the checkers).</summary>
        public const int HudPanel = 200;

        /// <summary>HUD text inside the panel's SortingGroup (above the importer's layer orders).</summary>
        public const int HudTextInGroup = 100;
    }
}
