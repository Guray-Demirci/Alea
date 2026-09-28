using System.Collections.Generic;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// 3x5 bitmap font. The glyph sprites are GENERATED IN CODE — the project contains no font asset.
    ///
    /// WHY NOT TextMeshPro / uGUI:
    ///   • Packages/manifest.json has no com.unity.ugui; neither UnityEngine.UI.Text nor TMP_Text
    ///     compiles in this project. Adding a package just to print two numbers is a heavy dependency.
    ///   • The canvas is 320x180 at 12 PPU. A vector font lands on half pixels at this scale and blurs;
    ///     the text would look soft while the rest of the board is crisp pixel art.
    ///   • Glyphs are SpriteRenderers, so they naturally join the panel's SortingGroup;
    ///     no separate Canvas / camera / sorting world is needed.
    ///
    /// Glyph encoding: 15 bits, left to right, TOP TO BOTTOM. Rows first, then columns:
    ///     1 1 1     "111"
    ///     1 0 1     "101"
    ///     1 0 1  -> "101"  ->  "111101101101111"   (the '0' character)
    ///     1 0 1     "101"
    ///     1 1 1     "111"
    ///
    /// The font is FIXED WIDTH (monospace). Chosen for the HUD so a score going from 99 to 100
    /// does not shift the digits and the panel does not jitter.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphWidth = 3;
        public const int GlyphHeight = 5;

        /// <summary>Horizontal advance of one character: 3 px glyph + 1 px spacing.</summary>
        public const int Advance = GlyphWidth + 1;

        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            { '0', "111101101101111" }, { '1', "010110010010111" },
            { '2', "111001111100111" }, { '3', "111001111001111" },
            { '4', "101101111001001" }, { '5', "111100111001111" },
            { '6', "111100111101111" }, { '7', "111001010010010" },
            { '8', "111101111101111" }, { '9', "111101111001111" },

            { 'A', "010101111101101" }, { 'B', "110101110101110" },
            { 'C', "011100100100011" }, { 'D', "110101101101110" },
            { 'E', "111100110100111" }, { 'F', "111100110100100" },
            { 'G', "011100101101011" }, { 'H', "101101111101101" },
            { 'I', "111010010010111" }, { 'J', "001001001101010" },
            { 'K', "101101110101101" }, { 'L', "100100100100111" },
            { 'M', "101111111101101" }, { 'N', "110101101101101" },
            { 'O', "010101101101010" }, { 'P', "110101110100100" },
            { 'Q', "010101101111011" }, { 'R', "110101110101101" },
            { 'S', "011100010001110" }, { 'T', "111010010010010" },
            { 'U', "101101101101111" }, { 'V', "101101101101010" },
            { 'W', "101101111111101" }, { 'X', "101101010101101" },
            { 'Y', "101101010010010" }, { 'Z', "111001010100111" },

            { ':', "000010000010000" }, { '-', "000000111000000" },
            { '.', "000000000000010" }, { ',', "000000000010100" },
            { '/', "001001010100100" }, { '+', "000010111010000" },
            { '%', "101001010100101" }, { '!', "010010010000010" },
            { '?', "110001010000010" }, { '=', "000111000111000" },
            { '*', "101010101000000" }, { '<', "001010100010001" },
            { '>', "100010001010100" }, { '(', "001010010010001" },
            { ')', "100010010010100" }
        };

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

        /// <summary>Width of the text in pixels. The spacing after the last character is not counted.</summary>
        public static int Measure(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return text.Length * Advance - 1;
        }

        /// <summary>
        /// Sprite of a character. Returns null for a space (nothing to draw);
        /// an unknown character becomes '?' — so it does not silently disappear from the panel but gets noticed and fixed.
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

            return Sprites.TryGetValue('?', out sprite) ? sprite : null;
        }

        /// <summary>
        /// Folds Turkish letters to their ASCII counterparts and converts to upper case.
        /// The font is 3 pixels wide; there is no room to distinguish dotted/cedilla letters,
        /// and drawing "I" and "İ" the same is better than losing them.
        /// </summary>
        private static char Normalize(char character)
        {
            switch (character)
            {
                case 'ı': case 'İ': case 'i': return 'I';
                case 'ş': case 'Ş': return 'S';
                case 'ğ': case 'Ğ': return 'G';
                case 'ü': case 'Ü': return 'U';
                case 'ö': case 'Ö': return 'O';
                case 'ç': case 'Ç': return 'C';
                default: return char.ToUpperInvariant(character);
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

            char[] order = new char[Glyphs.Count];
            Glyphs.Keys.CopyTo(order, 0);

            // 1 px transparent gap between glyphs: a cheap safeguard against neighbouring pixels
            // bleeding in, even with Point filtering.
            int width = order.Length * (GlyphWidth + 1);

            Texture2D texture = new Texture2D(width, GlyphHeight, TextureFormat.RGBA32, false);
            texture.name = "PixelFontAtlas";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[width * GlyphHeight];
            Color32 on = new Color32(255, 255, 255, 255);

            for (int i = 0; i < order.Length; i++)
            {
                string bits = Glyphs[order[i]];
                int originX = i * (GlyphWidth + 1);

                for (int row = 0; row < GlyphHeight; row++)
                {
                    for (int col = 0; col < GlyphWidth; col++)
                    {
                        if (bits[row * GlyphWidth + col] != '1')
                            continue;

                        // The glyph table is read top to bottom, the texture is laid out bottom to top.
                        int y = GlyphHeight - 1 - row;
                        pixels[y * width + originX + col] = on;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _atlas = texture;

            for (int i = 0; i < order.Length; i++)
            {
                Rect rect = new Rect(i * (GlyphWidth + 1), 0f, GlyphWidth, GlyphHeight);

                // Bottom-left pivot: the layout code (PixelLabel) can then work directly in pixel
                // coordinates without deriving the glyph width back from the pivot.
                Sprite sprite = Sprite.Create(texture, rect, Vector2.zero, pixelsPerUnit, 0,
                                              SpriteMeshType.FullRect);
                sprite.name = "Glyph_" + order[i];
                Sprites.Add(order[i], sprite);
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
