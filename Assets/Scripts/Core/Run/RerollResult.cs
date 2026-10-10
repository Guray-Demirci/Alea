namespace Tavla.Core.Run
{
    /// <summary>Whether the shelves can be rerolled, and if not, why. <see cref="Shop.Reroll"/> returns it.</summary>
    public enum RerollResult
    {
        Success,
        ShopClosed,
        NoRerollsLeft,
        NotEnoughGold
    }
}
