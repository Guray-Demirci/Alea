using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Hayalet Pul: "Rakibin kapilarinin uzerinden atlayabilir, blokajlari yok sayar."
    /// Hamle DOGRULAMA asamasinda sorulur, bu yuzden yan etkisiz olmalidir.
    /// </summary>
    public interface IBlockadeBypass : ICheckerAbility
    {
        /// <summary>Bu hedef hanedeki blokaj yok sayilabilir mi?</summary>
        bool CanBypassBlockade(Node target);
    }
}
