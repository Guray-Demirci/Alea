using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Gocebe Pul: "Bastigi hanenin sagindaki ve solundaki haneler tamamen bossa
    /// fazladan 3 hane daha ileri gitme hakki kazanir."
    /// Mesafe dogrulamasinda zar degerine EKLENIR, ekstra hamle hakki dogurmaz.
    /// </summary>
    public interface IExtraDistanceProvider : ICheckerAbility
    {
        /// <summary>Bu pulun mevcut konumuna gore kazandigi ek hane sayisi (0 olabilir).</summary>
        int GetBonusDistance(GameState state, int fromIndex);
    }
}
