using System.Runtime.CompilerServices;

// The View's clock is internal so only Unity drives it (DiceCardsView.Tick from Update).
// The EditMode test assembly still drives it to play the card flips frame by frame.
[assembly: InternalsVisibleTo("Tavla.View.Tests")]
