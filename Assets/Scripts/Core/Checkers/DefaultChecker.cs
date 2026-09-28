namespace Tavla.Core.Checkers
{
    /// <summary>
    /// The classic backgammon checker. Implements no ability interface — it is the "reference" behaviour of the rules.
    /// Special checkers are always tested against it.
    /// Also, every special checker that enters a Fog Tile behaves exactly like this class
    /// for as long as it stays there.
    /// </summary>
    public class DefaultChecker : BaseChecker
    {
        public const string Id = "checker_default";

        public override string TypeId => Id;

        public DefaultChecker(int ownerId) : base(ownerId)
        {
        }
    }
}
