using NUnit.Framework;
using UnityEngine;

namespace Tavla.View.Tests
{
    /// <summary>The hover glow drawn from a button's art: where its outline, halo and wash go, and how it breathes.</summary>
    public class HoverGlowTests
    {
        // A 3x3 ring with a hole in the middle, like the stall's open front closed in by its posts.
        private const int ArtSize = 3;
        private const int GlowSize = ArtSize + 2 * HoverGlow.Padding;

        private static readonly bool[] Ring =
        {
            true, true,  true,
            true, false, true,
            true, true,  true
        };

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        [Test]
        public void TheArt_IsWashed_AndAHoleItClosesIn_StaysClear()
        {
            Color32[] glow = HoverGlow.Paint(Ring, ArtSize, ArtSize);

            Assert.AreEqual(GlowSize * GlowSize, glow.Length);
            Assert.AreEqual(HoverGlow.WashColor, At(glow, 2, 2));
            Assert.AreEqual(HoverGlow.WashColor, At(glow, 4, 3));
            Assert.AreEqual(Clear, At(glow, 3, 3), "Only the outer edge glows.");
        }

        [Test]
        public void TheOutline_HugsTheArt_CornersIncluded()
        {
            Color32[] glow = HoverGlow.Paint(Ring, ArtSize, ArtSize);

            Assert.AreEqual(HoverGlow.OutlineColor, At(glow, 1, 3), "Beside the art.");
            Assert.AreEqual(HoverGlow.OutlineColor, At(glow, 1, 1), "Diagonal to the art's corner.");
            Assert.AreEqual(16, Count(glow, HoverGlow.OutlineColor), "The 5x5 square around the 3x3 art.");
        }

        [Test]
        public void TheHalo_LiesOutsideTheOutline_WithItsCornersRounded()
        {
            Color32[] glow = HoverGlow.Paint(Ring, ArtSize, ArtSize);

            Assert.AreEqual(HoverGlow.HaloColor, At(glow, 0, 3));
            Assert.AreEqual(HoverGlow.HaloColor, At(glow, 1, 0));
            Assert.AreEqual(Clear, At(glow, 0, 0), "The halo's corner pixel touches the outline only diagonally.");
            Assert.AreEqual(20, Count(glow, HoverGlow.HaloColor), "The canvas edge but its four corners.");
        }

        [Test]
        public void TheGlow_StartsAtFullStrength_AndBreathesDownToPulseLow()
        {
            Sprite art = CreateArt();
            HoverGlow glow = HoverGlow.Create("Glow", null, art, 0);

            try
            {
                Assert.IsFalse(glow.Shown, "It starts hidden.");

                glow.SetShown(true);
                Assert.AreEqual(1f, glow.Strength, 1e-4f);

                glow.Tick(HoverGlow.PulsePeriod / 2f);
                Assert.AreEqual(HoverGlow.PulseLow, glow.Strength, 1e-4f, "Weakest halfway through.");

                glow.Tick(HoverGlow.PulsePeriod / 2f);
                Assert.AreEqual(1f, glow.Strength, 1e-4f, "Full again after a period.");

                glow.SetShown(false);
                Assert.IsFalse(glow.Shown);
            }
            finally
            {
                Sprite glowSprite = glow.GetComponent<SpriteRenderer>().sprite;
                Object.DestroyImmediate(glowSprite.texture);
                Object.DestroyImmediate(glowSprite);
                Object.DestroyImmediate(glow.gameObject);
                Object.DestroyImmediate(art.texture);
                Object.DestroyImmediate(art);
            }
        }

        [Test]
        public void TheGlow_LiesOverTheArt_OnTheSamePivotPixel()
        {
            Sprite art = CreateArt();
            HoverGlow glow = HoverGlow.Create("Glow", null, art, 0);
            Sprite glowSprite = glow.GetComponent<SpriteRenderer>().sprite;

            try
            {
                Assert.AreEqual(art.pixelsPerUnit, glowSprite.pixelsPerUnit);
                Assert.AreEqual(art.pivot + Vector2.one * HoverGlow.Padding, glowSprite.pivot);
                Assert.AreEqual(GlowSize, glowSprite.texture.width);
            }
            finally
            {
                Object.DestroyImmediate(glowSprite.texture);
                Object.DestroyImmediate(glowSprite);
                Object.DestroyImmediate(glow.gameObject);
                Object.DestroyImmediate(art.texture);
                Object.DestroyImmediate(art);
            }
        }

        /// <summary>The ring as a readable sprite, pivoted on its middle, at the game's 12 pixels per unit.</summary>
        private static Sprite CreateArt()
        {
            Texture2D texture = new Texture2D(ArtSize, ArtSize, TextureFormat.RGBA32, false);
            for (int i = 0; i < Ring.Length; i++)
                texture.SetPixel(i % ArtSize, i / ArtSize, Ring[i] ? Color.white : Color.clear);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, ArtSize, ArtSize), new Vector2(0.5f, 0.5f), 12f);
        }

        private static Color32 At(Color32[] glow, int x, int y)
        {
            return glow[y * GlowSize + x];
        }

        private static int Count(Color32[] glow, Color32 color)
        {
            int count = 0;
            foreach (Color32 pixel in glow)
            {
                if (pixel.Equals(color))
                    count++;
            }

            return count;
        }
    }
}
