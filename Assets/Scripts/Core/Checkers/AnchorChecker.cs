using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Sabitleyici Pul (Anchor). "Hareket ettirildiginde, bir sonraki el atacagin iki zardan
    /// birini kesin olarak o el ilerledigi hane sayisina sabitler. Sansi tamamen ortadan kaldirir."
    /// Zar sistemine pul uzerinden dokunan tek ornek; kopru IMatchContext.PinNextDie.
    /// </summary>
    public class AnchorChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_anchor";

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            int distance = result.ToIndex - result.FromIndex;
            if (distance < 0)
                distance = -distance;

            // Bardan giris gibi uc durumlarda gecersiz deger sabitlemeyelim.
            if (distance < 1 || distance > GameState.MaxDieValue)
                return;

            context.PinNextDie(OwnerId, distance);
            context.Board.ReportAbility("AnchorPin", this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Sabitleyici Pul";
        public int Price => 620;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public AnchorChecker(int ownerId) : base(ownerId) { }
    }
}
