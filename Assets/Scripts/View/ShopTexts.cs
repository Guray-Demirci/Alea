using System;

namespace Tavla.View
{
    /// <summary>
    /// Every player-facing string of the shop screen, so they can be changed (or later fed from a localization
    /// table) without touching code. Formats use string.Format placeholders as noted. Keep them within the
    /// PixelFont character set (printable ASCII) and short: the info line fits about 25 capitals.
    /// </summary>
    [Serializable]
    public sealed class ShopTexts
    {
        /// <summary>The short code drawn on a shelf's cushion for a special checker that has no art.</summary>
        [Serializable]
        public sealed class ItemCode
        {
            public string ItemId;
            public string Code;
        }

        public string HudTask = "SHOP";
        public string Hint = "CLICK AN ITEM TO BUY";
        public string Next = "NEXT MATCH";

        /// <summary>{0} = price.</summary>
        public string Reroll = "REROLL {0}G";

        public string NoRerollsLeft = "NO REROLLS LEFT";

        /// <summary>{0} = name, {1} = price.</summary>
        public string Item = "{0} {1}G";

        /// <summary>{0} = name, {1} = price, {2} = tier.</summary>
        public string SpecialChecker = "{0} T{2} {1}G";

        /// <summary>Name of the plain checker shelf.</summary>
        public string BasicChecker = "CHECKER +1";

        /// <summary>{0} = name.</summary>
        public string Sold = "{0} SOLD";

        /// <summary>{0} = name.</summary>
        public string Bought = "BOUGHT {0}";

        public string Empty = "NOTHING LEFT";
        public string NotEnoughGold = "NOT ENOUGH GOLD";
        public string DeckFull = "DECK IS FULL";

        /// <summary>{0} = the limit.</summary>
        public string TooManySpecialCheckers = "MAX {0} SPECIAL CHECKERS";

        public string AlreadyOwned = "ALREADY OWNED";

        /// <summary>Items missing here show the first two letters of their name.</summary>
        public ItemCode[] Codes =
        {
            new ItemCode { ItemId = "checker_armored", Code = "AR" },
            new ItemCode { ItemId = "checker_nomad", Code = "NO" },
            new ItemCode { ItemId = "checker_parasite", Code = "PA" },
            new ItemCode { ItemId = "checker_timebender", Code = "TB" },
            new ItemCode { ItemId = "checker_clover", Code = "CL" },
            new ItemCode { ItemId = "checker_doubledown", Code = "DD" },
            new ItemCode { ItemId = "checker_catalyst", Code = "CA" },
            new ItemCode { ItemId = "checker_navigator", Code = "NA" },
            new ItemCode { ItemId = "checker_siege", Code = "SI" },
            new ItemCode { ItemId = "checker_sacrificial", Code = "SA" },
            new ItemCode { ItemId = "checker_golden", Code = "GO" },
            new ItemCode { ItemId = "checker_bandit", Code = "BA" },
            new ItemCode { ItemId = "checker_anchor", Code = "AN" },
            new ItemCode { ItemId = "checker_phoenix", Code = "PH" },
            new ItemCode { ItemId = "checker_doomsday", Code = "DM" },
            new ItemCode { ItemId = "checker_drunken", Code = "DR" },
            new ItemCode { ItemId = "checker_gunpowder", Code = "GP" },
            new ItemCode { ItemId = "checker_voodoo", Code = "VO" },
            new ItemCode { ItemId = "checker_reckless", Code = "RE" },
            new ItemCode { ItemId = "checker_teleporter", Code = "TP" },
            new ItemCode { ItemId = "checker_secondchance", Code = "SC" },
            new ItemCode { ItemId = "checker_grappling", Code = "GR" },
            new ItemCode { ItemId = "checker_poisonedrum", Code = "PR" },
            new ItemCode { ItemId = "checker_diceholder", Code = "DH" },
            new ItemCode { ItemId = "checker_kraken", Code = "KR" },
            new ItemCode { ItemId = "checker_mutiny", Code = "MU" },
            new ItemCode { ItemId = "checker_hammer", Code = "HA" }
        };
    }
}
