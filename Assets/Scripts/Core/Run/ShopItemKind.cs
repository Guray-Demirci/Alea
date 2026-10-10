namespace Tavla.Core.Run
{
    /// <summary>What a <see cref="ShopItem"/> is, which decides where it goes when bought.</summary>
    public enum ShopItemKind
    {
        /// <summary>A checker with an ability. Joins the deck; priced by its tier.</summary>
        SpecialChecker,

        /// <summary>A plain checker ("Başlangıç Pulu"). Joins the deck; can be bought any number of times.</summary>
        BasicChecker,

        /// <summary>A single-use card (consumable relic). Joins the run's relics until it is used up.</summary>
        Card,

        /// <summary>A permanent dice charm (permanent relic). Joins the run's relics for good; never sold twice.</summary>
        Charm
    }
}
