using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tavla.View
{
    /// <summary>
    /// How large text is drawn: how many canvas pixels one font pixel covers. At the art's own scale Alagard's
    /// 11 px capitals would not fit the panels, so the font is drawn on a finer grid than the art.
    /// </summary>
    public enum PixelTextSize
    {
        /// <summary>Half size: 2 font pixels per canvas pixel, capitals 5.5 px tall. The HUD and the shop's info line.</summary>
        Regular,

        /// <summary>Third size: 3 font pixels per canvas pixel, capitals about 3.7 px tall. Tight spots such as price tags.</summary>
        Small
    }

    /// <summary>
    /// Bitmap font: Alagard (Pix3M), a 16 px pixel font. The glyph sprites are GENERATED IN CODE from the table
    /// below — no font asset is needed at runtime. The source, alagard.ttf, is kept in Assets/Fonts.
    ///
    /// WHY NOT TextMeshPro / uGUI:
    ///   • Packages/manifest.json has no com.unity.ugui; neither UnityEngine.UI.Text nor TMP_Text
    ///     compiles in this project. Adding a package just to print a few numbers is a heavy dependency.
    ///   • A rasterised TTF is antialiased and lands on half pixels at this scale; the text would look soft
    ///     while the rest of the board is crisp pixel art. Alagard is a pixel font, so its exact pixels are
    ///     baked here and drawn as sprites.
    ///   • Glyphs are SpriteRenderers, so they naturally join the panel's SortingGroup;
    ///     no separate Canvas / camera / sorting world is needed.
    ///
    /// Glyph table: generated from alagard.ttf on its design grid (64 font units = 1 pixel, 16 pixels per em).
    /// Each entry is the character, its advance, the ink's offset from the pen position on the baseline
    /// (x right, y UP — descenders are negative), the ink width, and the ink rows TOP TO BOTTOM as hex bit
    /// masks with the leftmost pixel in the highest bit:
    ///     'A', 9, 0, 0, 8, "18 3C ..."   ->   18 = 00011000 = "...##..."
    ///
    /// The font is PROPORTIONAL and every glyph leaves 1 px after its ink. All sizes here are in FONT pixels;
    /// PixelLabel scales them onto the canvas (see <see cref="PixelTextSize"/>).
    /// </summary>
    public static class PixelFont
    {
        /// <summary>Rows of a capital letter or a digit, standing on the baseline.</summary>
        public const int CapHeight = 11;

        /// <summary>Advance of a space; it has no glyph.</summary>
        private const int SpaceAdvance = 5;

        /// <summary>Gap every glyph leaves after its ink. Not part of a text's measured width.</summary>
        private const int TrailingGap = 1;

        private readonly struct GlyphData
        {
            public readonly char Character;
            public readonly int Advance;
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly string Rows;

            public GlyphData(char character, int advance, int x, int y, int width, string rows)
            {
                Character = character;
                Advance = advance;
                X = x;
                Y = y;
                Width = width;
                Rows = rows;
            }
        }

        // Printable ASCII. Generated from alagard.ttf — regenerate rather than edit by hand.
        private static readonly GlyphData[] Glyphs =
        {
            new GlyphData('!', 5, 0, 0, 4, "02 06 0E 06 06 06 07 06 00 07 06"),
            new GlyphData('"', 8, 0, 6, 7, "33 77 33 33 22"),
            new GlyphData('#', 11, 0, 0, 10, "044 0CC 0CC 3FF 0CC 0CC 3FF 0CC 0CC 088"),
            new GlyphData('$', 9, 0, -2, 8, "08 3E 4F CB C8 FC 7E 17 13 D3 F2 7C 10 10"),
            new GlyphData('%', 13, 0, -1, 12, "300 582 CC4 CC8 690 320 04C 096 133 233 41A 00C"),
            new GlyphData('&', 12, 0, 0, 11, "3C0 660 630 630 327 1C6 3C4 668 630 639 3CE"),
            new GlyphData('\'', 4, 0, 6, 3, "03 07 03 03 02"),
            new GlyphData('(', 7, 0, -3, 6, "06 0F 1B 30 30 30 30 30 30 30 30 38 1E 0C"),
            new GlyphData(')', 8, 1, -3, 6, "08 1C 2E 07 03 03 03 03 03 03 03 02 04 08"),
            new GlyphData('*', 7, 0, 5, 6, "08 0C 3F 1C 36 22"),
            new GlyphData('+', 11, 0, 0, 10, "010 030 070 030 030 3FF 030 030 038 030"),
            new GlyphData(',', 4, 0, -3, 3, "03 07 03 02 04"),
            new GlyphData('-', 7, 0, 3, 6, "3F"),
            new GlyphData('.', 4, 1, 0, 2, "03 03"),
            new GlyphData('/', 12, 1, 0, 10, "001 002 004 008 010 020 040 080 100 200"),
            new GlyphData('0', 8, 0, 0, 7, "1C 2E 67 63 67 6B 73 63 73 3A 1C"),
            new GlyphData('1', 5, 0, 0, 4, "02 06 0E 06 06 06 06 06 06 06 0F"),
            new GlyphData('2', 7, 0, 0, 6, "1E 37 23 03 02 04 08 10 30 3F 3E"),
            new GlyphData('3', 8, 0, 0, 7, "1C 3E 47 03 02 0E 07 03 63 72 3C"),
            new GlyphData('4', 10, 0, 0, 9, "00C 01C 02C 04C 08C 18C 1FF 1FE 00C 00C 00C"),
            new GlyphData('5', 8, 0, 0, 7, "7F 60 60 6C 7E 67 43 03 63 72 3C"),
            new GlyphData('6', 9, 0, 0, 8, "1E 27 43 C0 DC EE C7 C3 E3 72 3C"),
            new GlyphData('7', 9, 0, 0, 8, "3F 7F 82 04 08 18 18 18 18 1C 18"),
            new GlyphData('8', 9, 0, 0, 8, "3E 47 C3 E3 72 3C 4E C7 E3 72 3C"),
            new GlyphData('9', 9, 0, 0, 8, "3C 4E C7 C3 E3 73 3F 03 C2 E4 78"),
            new GlyphData(':', 4, 1, 0, 2, "03 03 00 00 00 03 03"),
            new GlyphData(';', 4, 0, -3, 3, "03 03 00 00 00 03 07 03 02 04"),
            new GlyphData('<', 6, 0, 0, 5, "01 02 04 08 10 18 0C 06 03"),
            new GlyphData('=', 9, 1, 2, 7, "7F 00 00 7F"),
            new GlyphData('>', 7, 1, 0, 5, "18 0C 06 03 01 02 04 08 10"),
            new GlyphData('?', 8, 0, 0, 7, "1C 2E 47 03 02 04 08 00 18 18"),
            new GlyphData('@', 13, 0, -3, 12, "1FC 20E 467 CF3 D1B C7B C9B D9A D9C CF8 E01 702 3FC"),
            new GlyphData('A', 9, 0, 0, 8, "18 3C 4E C6 C6 CE D6 E6 C6 E7 C6"),
            new GlyphData('B', 9, 0, 0, 8, "3C 6E E7 62 64 6E 67 63 63 E2 7C"),
            new GlyphData('C', 9, 0, 0, 8, "1D 2F 47 C0 C0 C0 C0 C0 E1 72 3C"),
            new GlyphData('D', 10, 0, 0, 9, "07C 0CE 1C7 0C3 0C3 0C3 0C3 0C2 0C4 0C8 0F0"),
            new GlyphData('E', 10, 0, 0, 9, "05D 0EF 1C6 0C0 0C0 0F8 0C0 0C0 0C0 0E2 07C"),
            new GlyphData('F', 10, 0, 0, 9, "05D 0EF 1C6 0C0 0C0 0F8 0C0 0C0 0C0 0E0 0C0"),
            new GlyphData('G', 9, 0, 0, 8, "1E 27 43 C0 C0 C0 C7 C3 E3 72 3C"),
            new GlyphData('H', 10, 0, 0, 9, "042 0C6 1C6 0C6 0C6 0FE 0C6 0C6 0C6 0C7 0C6"),
            new GlyphData('I', 5, 0, 0, 4, "02 06 0E 06 06 06 06 06 06 07 06"),
            new GlyphData('J', 4, 0, -2, 3, "01 03 07 03 03 03 03 03 03 03 03 02 04"),
            new GlyphData('K', 10, 0, 0, 9, "042 0C6 1C6 0CC 0D8 0FC 0EE 0C6 0C6 0E7 0C6"),
            new GlyphData('L', 8, 0, 0, 7, "10 30 70 30 30 30 30 30 30 31 3E"),
            new GlyphData('M', 13, 0, 0, 12, "266 6EE F76 666 666 666 666 666 666 777 666"),
            new GlyphData('N', 11, 0, 0, 10, "082 186 386 1C6 1A6 196 18E 186 186 1C7 186"),
            new GlyphData('O', 9, 0, 0, 8, "1C 2E 47 C3 C3 C3 C3 C3 E2 74 38"),
            new GlyphData('P', 9, 0, 0, 8, "2E 77 E3 63 63 62 7C 78 60 70 60"),
            new GlyphData('Q', 10, 0, -2, 9, "038 05C 08E 186 186 186 186 186 1C4 0E8 07D 00E 004"),
            new GlyphData('R', 10, 0, 0, 9, "05C 0EE 1C6 0C4 0CC 0DE 0E6 0C6 0C6 0E7 0C6"),
            new GlyphData('S', 9, 0, 0, 8, "1C 2F 46 C0 FC 7E 07 03 C2 E4 78"),
            new GlyphData('T', 10, 0, 0, 9, "07D 097 133 030 030 030 030 030 031 03A 01C"),
            new GlyphData('U', 11, 0, 0, 10, "082 186 38E 186 186 186 186 186 186 1C7 0FE"),
            new GlyphData('V', 10, 0, 0, 9, "041 0C3 1C3 0C3 0C3 0C3 0C3 0E3 072 03C 018"),
            new GlyphData('W', 12, 0, 0, 11, "101 303 703 313 333 333 333 333 3B2 1F4 0B8"),
            new GlyphData('X', 10, 0, 0, 9, "042 0C6 1C6 0E6 074 038 05C 0CE 0C6 1C7 0C6"),
            new GlyphData('Y', 10, 0, 0, 9, "041 0C3 1C3 0C3 0E2 074 038 018 018 01C 018"),
            new GlyphData('Z', 9, 0, 0, 8, "3E 7F 82 04 08 7C 20 40 C1 FE 7C"),
            new GlyphData('[', 6, 1, -2, 4, "0F 0C 0C 0C 0C 0C 0C 0C 0C 0C 0C 0C 0F"),
            new GlyphData('\\', 11, 0, 0, 10, "300 380 1C0 0E0 070 038 01C 00E 007 003"),
            new GlyphData(']', 5, 0, -2, 4, "0F 03 03 03 03 03 03 03 03 03 03 03 0F"),
            new GlyphData('^', 9, 0, 6, 8, "08 1C 2E 47 83"),
            new GlyphData('_', 10, -1, 0, 10, "3FF"),
            new GlyphData('`', 6, 2, 8, 3, "06 07 03"),
            new GlyphData('a', 8, 0, 0, 7, "1C 3E 46 1E 26 66 67 3E"),
            new GlyphData('b', 9, 0, 0, 8, "20 60 E0 66 6F 73 63 63 63 62 7C"),
            new GlyphData('c', 8, 0, 0, 7, "1D 27 63 60 60 70 39 1E"),
            new GlyphData('d', 9, 0, 0, 8, "06 0E 06 76 DE CE C6 C6 E6 7F 36"),
            new GlyphData('e', 8, 0, 0, 7, "1E 27 62 64 68 70 71 3E"),
            new GlyphData('f', 8, 0, 0, 7, "0D 17 33 30 30 7C 30 30 30 38 30"),
            new GlyphData('g', 9, 0, -3, 8, "36 5F CE C6 C6 C6 6E 36 C6 E4 78"),
            new GlyphData('h', 10, 0, 0, 9, "040 0C0 1C0 0CC 0DE 0E6 0C6 0C6 0C6 0C7 0C6"),
            new GlyphData('i', 5, 0, 0, 4, "02 06 04 00 02 06 0E 06 06 07 06"),
            new GlyphData('j', 4, 0, -3, 3, "01 03 02 00 01 03 07 03 03 03 03 03 02 04"),
            new GlyphData('k', 10, 0, 0, 9, "040 0C0 1CC 0CC 0D8 0FC 0EE 0C6 0C6 0E7 0C6"),
            new GlyphData('l', 5, 0, 0, 4, "02 06 0E 06 06 06 06 06 06 07 06"),
            new GlyphData('m', 13, 0, 0, 12, "266 6EE F76 666 666 666 667 666"),
            new GlyphData('n', 10, 0, 0, 9, "04E 0D6 1E6 0C6 0C6 0C6 0E7 0C6"),
            new GlyphData('o', 8, 0, 0, 7, "1C 2E 67 63 63 73 3A 1C"),
            new GlyphData('p', 9, 0, -3, 8, "1C 2E 67 E3 63 73 7A 6C 60 70 60"),
            new GlyphData('q', 9, 0, -3, 8, "36 5E CE C6 C6 E6 7E 36 06 07 06"),
            new GlyphData('r', 8, 0, 0, 7, "12 37 7B 30 30 30 38 30"),
            new GlyphData('s', 7, 0, 0, 6, "1E 33 38 1C 0E 27 33 1E"),
            new GlyphData('t', 6, 0, 0, 5, "04 0C 1C 0C 1F 0C 0C 0C 0D 0E 0C"),
            new GlyphData('u', 10, 0, 0, 9, "042 0C6 1C6 0C6 0C6 0CE 0F7 066"),
            new GlyphData('v', 9, 0, 0, 8, "21 63 E3 63 63 72 3C 18"),
            new GlyphData('w', 12, 0, 0, 11, "101 303 713 333 333 3B3 1FA 0CC"),
            new GlyphData('x', 11, 0, 0, 10, "184 3CE 0F6 070 038 33C 3CF 186"),
            new GlyphData('y', 8, 0, -3, 7, "11 33 73 33 33 37 3B 13 42 64 38"),
            new GlyphData('z', 8, 0, 0, 7, "1F 3E 44 08 10 21 7E 7C"),
            new GlyphData('{', 5, 0, -3, 4, "01 02 06 06 06 06 0C 06 06 06 06 07 03"),
            new GlyphData('|', 5, 2, -3, 2, "01 03 03 03 03 03 03 03 03 03 03 03 03 02"),
            new GlyphData('}', 5, 0, -3, 4, "0C 0E 06 06 06 06 03 06 06 06 06 04 08"),
            new GlyphData('~', 9, 0, 2, 8, "30 79 9E 0C"),
        };

        private const char Fallback = '?';

        private static readonly Dictionary<char, int> GlyphIndex = BuildIndex();

        // The atlas is generated once; all labels share the same texture.
        private static Texture2D _atlas;
        private static float _atlasPixelsPerUnit;
        private static readonly Dictionary<char, Sprite> Sprites = new Dictionary<char, Sprite>();

        /// <summary>
        /// Resets the static cache when Play mode starts. With "Enter Play Mode Options" (domain reload
        /// disabled) statics survive between sessions while the textures they point to do not.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _atlas = null;
            _atlasPixelsPerUnit = 0f;
            Sprites.Clear();
        }

        private static Dictionary<char, int> BuildIndex()
        {
            Dictionary<char, int> index = new Dictionary<char, int>(Glyphs.Length);
            for (int i = 0; i < Glyphs.Length; i++)
                index.Add(Glyphs[i].Character, i);
            return index;
        }

        /// <summary>Canvas pixels covered by one font pixel at the given size.</summary>
        public static float GetPixelSize(PixelTextSize size)
        {
            return size == PixelTextSize.Small ? 1f / 3f : 0.5f;
        }

        /// <summary>
        /// Width of the text in font pixels, from the first pen position to the end of the last glyph's ink
        /// (the gap after the last character is not counted).
        /// </summary>
        public static int Measure(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int width = 0;
            for (int i = 0; i < text.Length; i++)
                width += GetAdvance(text[i]);

            return width - TrailingGap;
        }

        /// <summary>How far the pen moves after a character, in font pixels.</summary>
        public static int GetAdvance(char character)
        {
            char key = Normalize(character);
            if (key == ' ')
                return SpaceAdvance;

            int index;
            if (!GlyphIndex.TryGetValue(key, out index))
                index = GlyphIndex[Fallback];

            return Glyphs[index].Advance;
        }

        /// <summary>
        /// Sprite of a character, pivoted on the pen position on the baseline: placed at the pen, its ink lands
        /// where the font puts it. Returns null for a space (nothing to draw); an unknown character becomes '?' —
        /// so it does not silently disappear from the panel but gets noticed and fixed.
        /// </summary>
        public static Sprite GetGlyph(char character, float pixelsPerUnit)
        {
            EnsureAtlas(pixelsPerUnit);

            char key = Normalize(character);
            if (key == ' ')
                return null;

            Sprite sprite;
            if (Sprites.TryGetValue(key, out sprite))
                return sprite;

            return Sprites.TryGetValue(Fallback, out sprite) ? sprite : null;
        }

        /// <summary>
        /// Folds Turkish letters to their ASCII counterparts, keeping the case. Alagard has none of them,
        /// and drawing "ş" as "s" is better than losing it.
        /// </summary>
        private static char Normalize(char character)
        {
            switch (character)
            {
                case 'ı': return 'i';
                case 'İ': return 'I';
                case 'ş': return 's';
                case 'Ş': return 'S';
                case 'ğ': return 'g';
                case 'Ğ': return 'G';
                case 'ü': return 'u';
                case 'Ü': return 'U';
                case 'ö': return 'o';
                case 'Ö': return 'O';
                case 'ç': return 'c';
                case 'Ç': return 'C';
                default: return character;
            }
        }

        /// <summary>
        /// Generates the atlas (if needed).
        ///
        /// It is regenerated in two cases:
        ///   1) The PPU changed — sprites carry the world scale inside them.
        ///   2) _atlas has been "destroyed". Unity's null comparison catches textures that were
        ///      released when Play mode ended.
        /// The previous atlas and its sprites are destroyed before rebuilding so nothing leaks.
        /// </summary>
        private static void EnsureAtlas(float pixelsPerUnit)
        {
            if (pixelsPerUnit <= 0f)
                pixelsPerUnit = 1f;

            if (_atlas != null && Mathf.Approximately(_atlasPixelsPerUnit, pixelsPerUnit))
                return;

            ReleaseAtlas();
            _atlasPixelsPerUnit = pixelsPerUnit;

            // Glyphs side by side with a 1 px transparent gap: a cheap safeguard against neighbouring
            // pixels bleeding in, even with Point filtering.
            string[][] rows = new string[Glyphs.Length][];
            int width = 0;
            int height = 0;

            for (int i = 0; i < Glyphs.Length; i++)
            {
                rows[i] = Glyphs[i].Rows.Split(' ');
                width += Glyphs[i].Width + 1;
                height = Mathf.Max(height, rows[i].Length);
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "PixelFontAtlas";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[width * height];
            Color32 on = new Color32(255, 255, 255, 255);
            int[] originX = new int[Glyphs.Length];
            int x = 0;

            for (int i = 0; i < Glyphs.Length; i++)
            {
                GlyphData glyph = Glyphs[i];
                originX[i] = x;

                for (int row = 0; row < rows[i].Length; row++)
                {
                    int bits = Convert.ToInt32(rows[i][row], 16);

                    // The table is read top to bottom, the texture is laid out bottom to top.
                    int y = rows[i].Length - 1 - row;

                    for (int col = 0; col < glyph.Width; col++)
                    {
                        if ((bits & (1 << (glyph.Width - 1 - col))) != 0)
                            pixels[y * width + x + col] = on;
                    }
                }

                x += glyph.Width + 1;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _atlas = texture;

            for (int i = 0; i < Glyphs.Length; i++)
            {
                GlyphData glyph = Glyphs[i];
                int glyphHeight = rows[i].Length;
                Rect rect = new Rect(originX[i], 0f, glyph.Width, glyphHeight);

                // The pivot is the pen position on the baseline, outside the ink when the glyph is offset
                // (a descender, a colon): the layout code (PixelLabel) then only has to move the pen.
                Vector2 pivot = new Vector2(-glyph.X / (float)glyph.Width, -glyph.Y / (float)glyphHeight);

                Sprite sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "Glyph_" + glyph.Character;
                Sprites.Add(glyph.Character, sprite);
            }
        }

        /// <summary>Destroys the current atlas texture and its sprites (runtime-created objects are not collected).</summary>
        private static void ReleaseAtlas()
        {
            foreach (Sprite sprite in Sprites.Values)
            {
                if (sprite != null)
                    Object.Destroy(sprite);
            }
            Sprites.Clear();

            if (_atlas != null)
                Object.Destroy(_atlas);

            _atlas = null;
        }
    }
}
