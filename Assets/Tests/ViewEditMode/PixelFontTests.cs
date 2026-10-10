using NUnit.Framework;

namespace Tavla.View.Tests
{
    /// <summary>The text layout rules of the pixel font, which the HUD and the shop fit their boxes by.</summary>
    public class PixelFontTests
    {
        [Test]
        public void Measure_CountsEveryAdvance_ButNotTheGapAfterTheLastGlyph()
        {
            Assert.AreEqual(0, PixelFont.Measure(string.Empty));
            Assert.AreEqual(PixelFont.GetAdvance('A') + PixelFont.GetAdvance('B') - 1, PixelFont.Measure("AB"));
        }

        [Test]
        public void TurkishLetters_TakeTheRoomOfTheirAsciiLetters()
        {
            Assert.AreEqual(PixelFont.GetAdvance('s'), PixelFont.GetAdvance('ş'));
            Assert.AreEqual(PixelFont.GetAdvance('I'), PixelFont.GetAdvance('İ'));
            Assert.AreEqual(PixelFont.GetAdvance('i'), PixelFont.GetAdvance('ı'));
            Assert.AreEqual(PixelFont.GetAdvance('G'), PixelFont.GetAdvance('Ğ'));
        }

        [Test]
        public void AnUnknownCharacter_TakesTheRoomOfAQuestionMark()
        {
            Assert.AreEqual(PixelFont.GetAdvance('?'), PixelFont.GetAdvance('€'));
        }
    }
}
