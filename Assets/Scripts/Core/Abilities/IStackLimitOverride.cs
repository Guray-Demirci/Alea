namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Kara Delik Pulu: "Bu pulun bastigi hanedeki pul sinirini kaldirir."
    /// Hane kapasitesi artik sabit bir kural degil, hanede duran pullarin yetenegine bagli.
    /// </summary>
    public interface IStackLimitOverride : ICheckerAbility
    {
        /// <summary>Varsayilan limiti alir, gecerli limiti doner (sinirsiz icin int.MaxValue).</summary>
        int GetStackLimit(int defaultLimit);
    }
}
