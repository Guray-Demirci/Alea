using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Katalizor Pul. "Bir rakip pulunu kirdigi an, o el kazandigin tum puanlari ikiye katlar."
    /// Ayri bir "kirma yaptim" arayuzu gerekmedi: MoveResult zaten kirilanlari tasiyor.
    /// </summary>
    public class CatalystChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_catalyst";
        private const float CaptureMultiplier = 2f;

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover || !result.HasCapture)
                return;

            // permanent: false -> sadece bu el gecerli.
            context.AddScoreMultiplier(OwnerId, CaptureMultiplier, false);
            context.Board.ReportAbility("Catalyst", this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Katalizör Pul";
        public int Price => 480;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public CatalystChecker(int ownerId) : base(ownerId) { }
    }
}
