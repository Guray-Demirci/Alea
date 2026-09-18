using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Zaman Buken Pul. "Her oynattiginda %50 ihtimalle rakibin turunu pas gecirirsin,
    /// %50 ihtimalle hicbir sey olmaz."
    /// </summary>
    public class TimeBenderChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_timebender";
        private const float SkipChance = 0.5f;

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            if (!context.Random.Chance(SkipChance))
                return;

            int opponentId = (OwnerId + 1) % GameState.PlayerCount;
            context.SkipTurn(opponentId);
            context.Board.ReportAbility("TimeBend", this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Zaman Büken Pul";
        public int Price => 550;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public TimeBenderChecker(int ownerId) : base(ownerId) { }
    }
}
