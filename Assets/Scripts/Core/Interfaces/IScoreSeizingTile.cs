namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// A tile that seizes the score made on its point (Plunder Cove): a capture there, or a bear-off from there, earns
    /// nothing. A pure rule, so the whole capability is a marker; GameState applies it as it awards the score.
    /// </summary>
    public interface IScoreSeizingTile
    {
    }
}
