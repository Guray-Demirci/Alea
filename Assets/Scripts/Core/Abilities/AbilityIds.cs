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
        // --- Movement rules (GameState) ---
        public const string ExtraDistance = "ExtraDistance";
        public const string AreaCapture = "AreaCapture";
        public const string Infiltrate = "Infiltrate";
        public const string BlockadeBypass = "BlockadeBypass";
        public const string ImmunityBlocked = "ImmunityBlocked";

        // --- Checkers ---
        public const string AnchorPin = "AnchorPin";
        public const string BouncerSelfHit = "BouncerSelfHit";
        public const string BouncerStrike = "BouncerStrike";
        public const string Catalyst = "Catalyst";
        public const string DoubleDownBonus = "DoubleDownBonus";
        public const string DoubleDownPenalty = "DoubleDownPenalty";
        public const string GeminiLink = "GeminiLink";
        public const string GreedyIncome = "GreedyIncome";
        public const string NavigatorTrail = "NavigatorTrail";
        public const string PhoenixRebirth = "PhoenixRebirth";
        public const string SacrificeDiscount = "SacrificeDiscount";
        public const string SiegeLocked = "SiegeLocked";
        public const string TimeBend = "TimeBend";
        public const string TollCollected = "TollCollected";

        // --- Tiles ---
        public const string DungeonClosed = "DungeonClosed";
        public const string DungeonOpened = "DungeonOpened";
        public const string FogEnter = "FogEnter";
        public const string FogExit = "FogExit";
        public const string FuseLit = "FuseLit";
        public const string FuseDefused = "FuseDefused";
        public const string KegExploded = "KegExploded";
        public const string MaelstromPull = "MaelstromPull";
        public const string Plundered = "Plundered";
        public const string SpikeBroken = "SpikeBroken";
        public const string SpikeHit = "SpikeHit";
        public const string SwampStuck = "SwampStuck";
        public const string SwampReleased = "SwampReleased";
        public const string TollPaid = "TollPaid";
    }
}
