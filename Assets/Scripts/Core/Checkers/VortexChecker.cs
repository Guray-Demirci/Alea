using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Vortex Checker. "Removes the checker limit of its point; you can gather every checker of your
    /// colour on a single point and build a massive SUPER POINT."
    /// Capacity is no longer a fixed rule but a question asked of the checkers standing on the point.
    /// </summary>
    public class VortexChecker : BaseChecker, IStackLimitOverride, IPermanent
    {
        public const string Id = "checker_vortex";

        public override string TypeId => Id;

        /// <summary>The limit is removed entirely. Inside a Fog Tile this ability drops and the limit returns.</summary>
        public int GetStackLimit(int defaultLimit) => int.MaxValue;

        public string ItemId => Id;
        public string DisplayName => "Vortex Checker";
        public int Price => 850;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public VortexChecker(int ownerId) : base(ownerId) { }
    }
}
