namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// Everything that can be bought in the shop (special checker, trap, board skin) implements this interface.
    /// NOTE: "Has it been purchased?" is NOT stored here. That belongs to a player-owned
    /// inventory/repository layer (SRP). This interface only describes the storefront data.
    /// </summary>
    public interface IPurchasable
    {
        /// <summary>Stable, unique identifier. Save data, analytics and shop lookups use this value.</summary>
        string ItemId { get; }

        /// <summary>Name shown in the UI (may also be used as a localization key).</summary>
        string DisplayName { get; }

        /// <summary>Price of the item. The currency type may later be extended with a separate enum.</summary>
        int Price { get; }
    }
}
