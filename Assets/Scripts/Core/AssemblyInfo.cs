using System.Runtime.CompilerServices;

// Board and dice mutators are internal so only Core rules can change the game state.
// The EditMode test assembly still needs them to build test positions.
[assembly: InternalsVisibleTo("Tavla.Core.Tests")]
