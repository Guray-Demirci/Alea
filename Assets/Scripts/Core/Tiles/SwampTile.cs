using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C1 - Bataklik Hanesi. "Bu haneye basan her pul %50 ihtimalle saplanir (kilitlenir).
    /// Kilidi acmak icin sonraki turda zarlardan birini feda etmek gerekir."
    /// Kilit PUL BAZINDA tutulur: ayni hanede biri saplanmisken digeri serbest olabilir.
    /// </summary>
    public class SwampTile : BaseTileEffect
    {
        public const string Id = "tile_swamp";
        private const float StickChance = 0.5f;

        private readonly HashSet<BaseChecker> _stuck = new HashSet<BaseChecker>();

        public override string EffectId => Id;

        public override bool CanLeaveFrom(BaseChecker checker) => !_stuck.Contains(checker);

        public bool IsStuck(BaseChecker checker) => _stuck.Contains(checker);

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            if (!context.Random.Chance(StickChance))
                return;

            _stuck.Add(checker);
            context.Board.ReportAbility("SwampStuck", checker, node.Index);
        }

        public override void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker)
        {
            // Pul haneden ciktiysa kilit kaydini tasimaya gerek yok (bellek sizintisini da onler).
            _stuck.Remove(checker);
        }

        /// <summary>
        /// Kilidi acmak icin zar feda etme aksiyonu. Hamle DEGILDIR, o yuzden TryMove'dan gecmez;
        /// oyuncu arayuzden "kilidi coz" dediginde cagrilir ve sectigi zari harcar.
        /// </summary>
        public bool TryUnlock(IMatchContext context, BaseChecker checker, int sacrificedDieValue)
        {
            if (!_stuck.Contains(checker))
                return false;

            if (context.CurrentRoll != null && !context.CurrentRoll.Consume(sacrificedDieValue))
                return false;

            _stuck.Remove(checker);
            context.Board.ReportAbility("SwampReleased", checker, context.Board.FindNodeIndexOf(checker));
            return true;
        }
    }
}
