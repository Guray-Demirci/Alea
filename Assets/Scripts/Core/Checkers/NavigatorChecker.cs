using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Kasif Pulu (The Navigator). "Tahtada ilerledikce arkasinda ayak izi birakir.
    /// Bastigi HER YENI hane icin 50 taban puan verir."
    /// "Yeni" olmasi onemli: ayni haneye tekrar gelirse puan yazilmaz, bu yuzden pul
    /// kendi gezdigi haneleri hatirlamak zorunda (durum pulun kendisine ait, SRP).
    /// </summary>
    public class NavigatorChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_navigator";
        private const int ScorePerNewNode = 50;

        private readonly HashSet<int> _visited = new HashSet<int>();

        public override string TypeId => Id;

        /// <summary>View'in haritaya ayak izi cizmesi icin gezilen haneler.</summary>
        public IEnumerable<int> VisitedNodes => _visited;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            // Add false donerse hane daha once gezilmistir -> puan yok.
            if (!_visited.Add(result.ToIndex))
                return;

            context.AddScore(OwnerId, ScorePerNewNode);
            context.Board.ReportAbility("NavigatorTrail", this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Kaşif Pulu";
        public int Price => 320;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public NavigatorChecker(int ownerId) : base(ownerId) { }
    }
}
