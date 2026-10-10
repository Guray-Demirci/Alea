namespace Tavla.Core.Run
{
    /// <summary>
    /// The shelves of a shop visit. On the Merchant Table (in_game_shop_ready) the special checkers lie on three
    /// cushions and the plain checker on a fourth; the card and charm shelves get their places later.
    /// </summary>
    public enum ShopSlot
    {
        /// <summary>First of the three special checker shelves.</summary>
        SpecialChecker1,
        SpecialChecker2,
        SpecialChecker3,

        /// <summary>The plain checker shelf. Never sells out and is not touched by rerolls.</summary>
        BasicChecker,

        Card,
        Charm
    }
}
