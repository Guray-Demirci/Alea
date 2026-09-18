using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Bar Pulu (The Bouncer). "%50 ihtimalle onundeki ilk rakip pulunu direkt kirar,
    /// %25 ihtimalle kendi kendini kirar."
    /// Tek bir zar atisi (0-99) ile uc sonucu da ayiriyoruz: 0-49 kirar, 50-74 kendini kirar, 75-99 hicbir sey.
    /// </summary>
    public class BouncerChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_bouncer";

        private const int CaptureThreshold = 50;   // %50
        private const int SelfHitThreshold = 75;   // sonraki %25

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            int roll = context.Random.Range(0, 100);

            if (roll < CaptureThreshold)
            {
                CaptureFirstEnemyAhead(context, result.ToIndex);
            }
            else if (roll < SelfHitThreshold)
            {
                // Kendi kendini kirar. ForceCapture'dan gectigi icin Anka/Fedakar kurallari da isler.
                context.Board.ReportAbility("BouncerSelfHit", this, result.ToIndex);
                context.Board.ForceCapture(this, result.ToIndex, OwnerId);
            }
        }

        /// <summary>Ilerleme yonunde ilk rakip pulunu bulup kirar.</summary>
        private void CaptureFirstEnemyAhead(IMatchContext context, int fromIndex)
        {
            int step = GameState.GetDirection(OwnerId);

            for (int index = fromIndex + step; GameState.IsOnBoard(index); index += step)
            {
                Node node = context.Board.Nodes[index];
                BaseChecker enemy = node.GetTopEnemyOf(OwnerId);

                if (enemy == null)
                    continue;

                // Zirhli Pul gibi bagisikligi olanlar bu yetenekten de etkilenmez.
                if (!enemy.CanBeCaptured)
                    return;

                context.Board.ReportAbility("BouncerStrike", this, index);
                context.Board.ForceCapture(enemy, index, OwnerId);
                return;
            }
        }

        public string ItemId => Id;
        public string DisplayName => "Bar Pulu";
        public int Price => 600;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public BouncerChecker(int ownerId) : base(ownerId) { }
    }
}
