using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Mutiny Checker. "When it captures an open enemy checker, it does not send it to the bar: it paints it in your
    /// colour and turns it into your checker (as long as you do not exceed 15 checkers)."
    /// The convert is a plain checker of yours; GameState decides when there is room for it.
    /// </summary>
    public class MutinyChecker : SpecialChecker, ICaptureConverter
    {
        public const string Id = "checker_mutiny";

        public BaseChecker Convert(BaseChecker victim) => new DefaultChecker(OwnerId);

        public MutinyChecker(int ownerId) : base(ownerId, Id, "Mutiny Checker") { }
    }
}
