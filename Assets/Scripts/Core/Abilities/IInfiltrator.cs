using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Parazit Pul: "Rakibin kapi aldigi bir haneye sinsice sizabilir."
    /// Bypass'tan FARKI: Hayalet haneyi es gecip devam eder, Parazit haneye YERLESIR
    /// ve rakiple ayni hanede birlikte durur. Bu yuzden Node artik iki sahipli olabilir.
    /// </summary>
    public interface IInfiltrator : ICheckerAbility
    {
        /// <summary>Rakip kapisina sizabilir mi? Sizma sirasinda kirma YAPILMAZ.</summary>
        bool CanInfiltrate(Node target);
    }
}
