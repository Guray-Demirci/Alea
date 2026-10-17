namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// A tile whose point the opponent's captured checkers restart on instead of the bar (Graveyard). A pure rule, so the
    /// whole capability is a marker; GameState applies it as it sends a captured checker to the bar.
    /// "The opponent" is the side that is not <see cref="Board.MatchRules.ScoringPlayerId"/>: the player's own captured
    /// checkers still go to the bar.
    /// </summary>
    public interface IRestartPointTile
    {
    }
}
