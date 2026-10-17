using Tavla.Core.Run;

namespace Tavla.View
{
    /// <summary>
    /// Everything on the shop screen that reacts to the pointer, apart from the sell list's slots: the six shelves,
    /// then the buttons. The shelves come first and in the same order as <see cref="ShopSlot"/>, so a shelf
    /// converts with a cast. The card and charm shelves have no place on the Merchant Table yet (see <see cref="ShopView"/>).
    /// </summary>
    public enum ShopTarget
    {
        SpecialChecker1 = (int)ShopSlot.SpecialChecker1,
        SpecialChecker2 = (int)ShopSlot.SpecialChecker2,
        SpecialChecker3 = (int)ShopSlot.SpecialChecker3,
        BasicChecker = (int)ShopSlot.BasicChecker,
        Card = (int)ShopSlot.Card,
        Charm = (int)ShopSlot.Charm,

        /// <summary>"NEXT": leave the shop and start the next match.</summary>
        Next,

        /// <summary>"ROLL": reroll the shelves.</summary>
        Reroll,

        /// <summary>The merchant's stall beside the board: opens and closes the list of checkers to sell.</summary>
        Sell
    }
}
