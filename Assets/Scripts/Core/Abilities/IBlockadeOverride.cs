namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Kapi (blokaj) kuralini bozan pullar. Zirhli Pul: "Tek basina dursa bile kapi sayilir."
    /// Klasik kuralda kapi icin 2 pul gerekir; bu yetenek o esigi pul bazinda ezer.
    /// </summary>
    public interface IBlockadeOverride : ICheckerAbility
    {
        bool FormsBlockadeAlone { get; }
    }
}
