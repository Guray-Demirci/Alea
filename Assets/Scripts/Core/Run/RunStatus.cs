namespace Tavla.Core.Run
{
    /// <summary>Where a run stands.</summary>
    public enum RunStatus
    {
        /// <summary>Matches are still to be played.</summary>
        InProgress,

        /// <summary>Every match was won.</summary>
        Won,

        /// <summary>A match was lost; the run is over.</summary>
        Lost
    }
}
