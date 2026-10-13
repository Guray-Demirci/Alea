using System;
using Tavla.Core.Checkers;
using Tavla.Core.Run;

namespace Tavla.View
{
    /// <summary>
    /// The one place that maps the <see cref="CheckerKind"/> dropdown to checker classes. The shop
    /// (<see cref="RunConfig"/>) and the board art (<see cref="CheckerArtSet"/>) both read it, so a new checker
    /// needs exactly one new case here.
    /// </summary>
    public static class CheckerCatalog
    {
        /// <summary>Makes a new checker of this kind for the given owner.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The kind has no checker class.</exception>
        public static CheckerFactory GetFactory(CheckerKind kind)
        {
            switch (kind)
            {
                case CheckerKind.Armored: return ownerId => new ArmoredChecker(ownerId);
                case CheckerKind.Nomad: return ownerId => new NomadChecker(ownerId);
                case CheckerKind.Parasite: return ownerId => new ParasiteChecker(ownerId);
                case CheckerKind.TimeBender: return ownerId => new TimeBenderChecker(ownerId);
                case CheckerKind.Clover: return ownerId => new CloverChecker(ownerId);
                case CheckerKind.DoubleDown: return ownerId => new DoubleDownChecker(ownerId);
                case CheckerKind.Catalyst: return ownerId => new CatalystChecker(ownerId);
                case CheckerKind.Navigator: return ownerId => new NavigatorChecker(ownerId);
                case CheckerKind.Siege: return ownerId => new SiegeChecker(ownerId);
                case CheckerKind.Sacrificial: return ownerId => new SacrificialChecker(ownerId);
                case CheckerKind.Golden: return ownerId => new GoldenChecker(ownerId);
                case CheckerKind.Bandit: return ownerId => new BanditChecker(ownerId);
                case CheckerKind.Anchor: return ownerId => new AnchorChecker(ownerId);
                case CheckerKind.Phoenix: return ownerId => new PhoenixChecker(ownerId);
                case CheckerKind.Doomsday: return ownerId => new DoomsdayChecker(ownerId);
                case CheckerKind.Drunken: return ownerId => new DrunkenChecker(ownerId);
                case CheckerKind.Gunpowder: return ownerId => new GunpowderChecker(ownerId);
                case CheckerKind.Voodoo: return ownerId => new VoodooChecker(ownerId);
                case CheckerKind.Reckless: return ownerId => new RecklessChecker(ownerId);
                case CheckerKind.Teleporter: return ownerId => new TeleporterChecker(ownerId);
                case CheckerKind.SecondChance: return ownerId => new SecondChanceChecker(ownerId);
                case CheckerKind.Grappling: return ownerId => new GrapplingChecker(ownerId);
                case CheckerKind.PoisonedRum: return ownerId => new PoisonedRumChecker(ownerId);
                case CheckerKind.DiceHolder: return ownerId => new DiceHolderChecker(ownerId);
                case CheckerKind.Kraken: return ownerId => new KrakenChecker(ownerId);
                case CheckerKind.Mutiny: return ownerId => new MutinyChecker(ownerId);
                case CheckerKind.Hammer: return ownerId => new HammerChecker(ownerId);
                case CheckerKind.Bowman: return ownerId => new BowmanChecker(ownerId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown checker kind.");
            }
        }

        /// <summary>The <see cref="BaseChecker.TypeId"/> of this kind's checkers.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The kind has no checker class.</exception>
        public static string GetTypeId(CheckerKind kind)
        {
            return GetFactory(kind)(0).TypeId;
        }
    }
}
