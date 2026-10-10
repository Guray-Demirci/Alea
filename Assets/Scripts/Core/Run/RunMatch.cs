using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Run
{
    /// <summary>
    /// A match of a run, ready to play: the board and context the View binds to, and where the match sits in
    /// the run. Created by <see cref="RunState.StartMatch"/>.
    /// </summary>
    public sealed class RunMatch
    {
        public GameState Board { get; }
        public MatchContext Context { get; }
        public MatchDefinition Definition { get; }

        public int StageIndex { get; }

        /// <summary>Index of the match within its stage.</summary>
        public int MatchIndex { get; }

        internal RunMatch(GameState board, MatchContext context, MatchDefinition definition, int stageIndex, int matchIndex)
        {
            Board = board;
            Context = context;
            Definition = definition;
            StageIndex = stageIndex;
            MatchIndex = matchIndex;
        }
    }
}
