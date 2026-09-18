using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Vergi Pulu: "Bu pulun uzerinden gecen her rakip pulu icin (ustunden atlayip gitse bile)
    /// +1 Altin yazilir." Klasik tavlada aradan gecilen haneler hic sorgulanmaz;
    /// bu yetenek GameState'e yeni bir tarama adimi (gecis yolu) ekletiyor.
    /// </summary>
    public interface IPassOverReactor : ICheckerAbility
    {
        void OnEnemyPassedOver(IMatchContext context, BaseChecker passingChecker, int nodeIndex);
    }
}
