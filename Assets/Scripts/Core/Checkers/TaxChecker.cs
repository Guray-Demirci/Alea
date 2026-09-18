using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Vergi Pulu (Tax Checker). "Uzerinden gecen her rakip pulu icin (rakip ustunden
    /// atlayip gitse bile) hanene aninda +1 Altin yazilir. Rakibin kacis yollarina kurulan bir gise."
    /// Klasik tavlada ARADAN gecilen haneler hic sorgulanmaz; bu pul icin GameState'e
    /// "gecis yolu taramasi" (NotifyPassOver) adimini eklemek zorunda kaldik.
    /// </summary>
    public class TaxChecker : BaseChecker, IPassOverReactor, IPermanent
    {
        public const string Id = "checker_tax";
        private const int TollAmount = 1;

        public override string TypeId => Id;

        public void OnEnemyPassedOver(IMatchContext context, BaseChecker passingChecker, int nodeIndex)
        {
            context.AddGold(OwnerId, TollAmount);
            context.Board.ReportAbility("TollCollected", this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Vergi Pulu";
        public int Price => 340;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public TaxChecker(int ownerId) : base(ownerId) { }
    }
}
