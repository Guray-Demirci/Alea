using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Acgozlu Pul: "Tek basina (acik olarak) kaldigi HER TUR icin ekstra altin uretir."
    /// Tur sonunda tahtadaki tum pullar icin cagrilir; pul kendi durumunu context uzerinden sorgular.
    /// </summary>
    public interface ITurnEndReactor : ICheckerAbility
    {
        void OnTurnEnded(IMatchContext context, int nodeIndex);
    }
}
