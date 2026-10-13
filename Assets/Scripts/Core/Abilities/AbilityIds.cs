namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Identifiers passed to <see cref="Board.GameState.AbilityTriggered"/>.
    ///
    /// The View maps these to VFX/SFX, so they are part of the public contract between
    /// Core and View. Keeping them as constants (instead of inline string literals)
    /// turns a typo into a compile error and makes every trigger point searchable.
    /// The string values are stable — renaming a constant must not change its value.
    /// </summary>
    public static class AbilityIds
    {
        // --- Rules engine (GameState) ---
        public const string ExtraDistance = "ExtraDistance";
        public const string AreaCapture = "AreaCapture";
        public const string Infiltrate = "Infiltrate";
        public const string ImmunityBlocked = "ImmunityBlocked";
        public const string ScoreModified = "ScoreModified";
        public const string Deviation = "Deviation";
        public const string FreeMove = "FreeMove";
        public const string Swap = "Swap";
        public const string CaptureRetaliation = "CaptureRetaliation";
        public const string CaptureConverted = "CaptureConverted";
        public const string TileIgnored = "TileIgnored";
        public const string PointMarked = "PointMarked";
        public const string TeleportCrash = "TeleportCrash";

        // --- Checkers ---
        public const string AnchorPin = "AnchorPin";
        public const string BowmanArrow = "BowmanArrow";
        public const string BowmanVolley = "BowmanVolley";
        public const string CloverSelfHit = "CloverSelfHit";
        public const string CloverStrike = "CloverStrike";
        public const string DiceHolderReroll = "DiceHolderReroll";
        public const string DoubleDownBonus = "DoubleDownBonus";
        public const string GoldenIncome = "GoldenIncome";
        public const string GrapplingHook = "GrapplingHook";
        public const string KrakenAwakens = "KrakenAwakens";
        public const string PhoenixRebirth = "PhoenixRebirth";
        public const string RumPoisoned = "RumPoisoned";
        public const string SacrificeDiscount = "SacrificeDiscount";
        public const string SecondChanceBonus = "SecondChanceBonus";
        public const string SiegeBonus = "SiegeBonus";
        public const string TimeBend = "TimeBend";
        public const string TollCollected = "TollCollected";

        // --- Tiles ---
        public const string DungeonClosed = "DungeonClosed";
        public const string DungeonOpened = "DungeonOpened";
        public const string FogEnter = "FogEnter";
        public const string FogExit = "FogExit";
        public const string KegExploded = "KegExploded";
        public const string MaelstromPull = "MaelstromPull";
        public const string Plundered = "Plundered";
        public const string SpikeHit = "SpikeHit";
        public const string SpikeResisted = "SpikeResisted";
        public const string SwampStuck = "SwampStuck";
        public const string SwampReleased = "SwampReleased";
        public const string TollPaid = "TollPaid";
    }
}
