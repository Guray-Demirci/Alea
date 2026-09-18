using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C7 - Mayinli Bolge / Barut Ficisi. "Pul bu haneye bastiginda 3 turluk geri sayim baslar.
    /// 3 tur icinde pulu uzaklastirmazsan fici patlar ve pul KALICI olarak yok olur (bara bile gidemez)."
    /// Geri sayim pul bazindadir; hanedeki her pulun kendi sayaci vardir.
    /// </summary>
    public class PowderKegTile : BaseTileEffect
    {
        public const string Id = "tile_powderkeg";
        private const int FuseTurns = 3;

        private readonly Dictionary<BaseChecker, int> _fuses = new Dictionary<BaseChecker, int>();

        public override string EffectId => Id;

        /// <summary>View'in karo ustunde sayac pikselleri gostermesi icin.</summary>
        public int GetRemainingFuse(BaseChecker checker)
        {
            int turns;
            return _fuses.TryGetValue(checker, out turns) ? turns : -1;
        }

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            _fuses[checker] = FuseTurns;
            context.Board.ReportAbility("FuseLit", checker, node.Index);
        }

        public override void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker)
        {
            // Pul zamaninda kacti: sayac iptal.
            if (_fuses.Remove(checker))
                context.Board.ReportAbility("FuseDefused", checker, node.Index);
        }

        public override void OnTurnPassed(IMatchContext context, Node node)
        {
            if (_fuses.Count == 0)
                return;

            // Sozlugu gezerken degistiremeyiz -> once patlayacaklari topla.
            List<BaseChecker> detonated = null;
            List<BaseChecker> keys = new List<BaseChecker>(_fuses.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                BaseChecker checker = keys[i];
                int remaining = _fuses[checker] - 1;

                if (remaining > 0)
                {
                    _fuses[checker] = remaining;
                    continue;
                }

                if (detonated == null)
                    detonated = new List<BaseChecker>();

                detonated.Add(checker);
            }

            if (detonated == null)
                return;

            for (int i = 0; i < detonated.Count; i++)
            {
                _fuses.Remove(detonated[i]);
                context.Board.ReportAbility("KegExploded", detonated[i], node.Index);

                // Bara bile gitmez: oyundan tamamen silinir.
                context.Board.DestroyChecker(detonated[i], node.Index);
            }
        }
    }
}
