using System.Collections.Generic;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// 3x5'lik bitmap font. Glif sprite'larini KOD URETIR — projede tek bir font asset'i yoktur.
    ///
    /// NEDEN TextMeshPro / uGUI DEGIL:
    ///   • Packages/manifest.json'da com.unity.ugui yok; bu projede ne UnityEngine.UI.Text
    ///     ne de TMP_Text derlenir. Paket eklemek, iki sayi basmak icin agir bir bagimlilik.
    ///   • Tuval 320x180 ve 12 PPU. Vektor bir font bu olcekte yarim piksele oturur ve
    ///     bulaniklasir; tahtanin geri kalani keskin piksel sanatken yazi yumusak gorunur.
    ///   • Glifler SpriteRenderer oldugu icin panelin SortingGroup'una dogal olarak katilir;
    ///     ayri bir Canvas / kamera / siralama dunyasi kurmak gerekmez.
    ///
    /// Gliflerin dizilisi: soldan saga, YUKARIDAN ASAGIYA 15 bit. Once satir, sonra sutun:
    ///     1 1 1     "111"
    ///     1 0 1     "101"
    ///     1 0 1  -> "101"  ->  "111101101101111"   ('0' karakteri)
    ///     1 0 1     "101"
    ///     1 1 1     "111"
    ///
    /// Font SABIT GENISLIKTEDIR (monospace). HUD icin tercih sebebi: skor 99'dan 100'e
    /// ciktiginda sayi kaymaz, panelde titreme olmaz.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphWidth = 3;
        public const int GlyphHeight = 5;

        /// <summary>Bir karakterin kapladigi yatay adim: 3 px glif + 1 px bosluk.</summary>
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

        // Atlas bir kez uretilir; butun etiketler ayni dokuyu paylasir.
        private static Texture2D _atlas;
        private static float _atlasPixelsPerUnit;
        private static readonly Dictionary<char, Sprite> Sprites = new Dictionary<char, Sprite>();

        /// <summary>Metnin piksel cinsinden genisligi. Son karakterin ardindaki bosluk sayilmaz.</summary>
        public static int Measure(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return text.Length * Advance - 1;
        }

        /// <summary>
        /// Bir karakterin sprite'i. Bosluk icin null doner (cizilecek bir sey yok),
        /// taninmayan karakter '?' olur — panelde sessizce kaybolmasin, gorulup duzeltilsin.
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
        /// Turkce harfleri ASCII karsiligina indirger ve buyuk harfe cevirir.
        /// Font 3 piksel genisliginde; noktali/kuyruklu harfleri ayirt edecek yer yok,
        /// "I" ile "İ"yi ayni cizmek onlari kaybetmekten iyidir.
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
        /// Atlasi (gerekirse) uretir.
        ///
        /// Iki kosulda yeniden uretilir:
        ///   1) PPU degisti — sprite'lar dunya olcegini icinde tasir.
        ///   2) _atlas "yok edilmis" durumda. Enter Play Mode Options ile domain reload
        ///      kapaliyken statik alan ayakta kalir ama Texture2D oyun bitince silinir;
        ///      Unity'nin null karsilastirmasi bunu yakalar.
        /// </summary>
        private static void EnsureAtlas(float pixelsPerUnit)
        {
            if (pixelsPerUnit <= 0f)
                pixelsPerUnit = 1f;

            if (_atlas != null && Mathf.Approximately(_atlasPixelsPerUnit, pixelsPerUnit))
                return;

            Sprites.Clear();
            _atlasPixelsPerUnit = pixelsPerUnit;

            char[] order = new char[Glyphs.Count];
            Glyphs.Keys.CopyTo(order, 0);

            // Glifler arasinda 1 px seffaf bosluk: filtreleme Point olsa da komsu
            // pikselin sizmasina karsi ucuz bir emniyet.
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

                        // Glif tablosu yukaridan asagi okunur, doku asagidan yukari dizilir.
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

                // Pivot sol-alt: yerlesim hesabi (PixelLabel) boylece dogrudan piksel
                // koordinatiyla calisir, glif genisligini pivottan geri hesaplamaz.
                Sprite sprite = Sprite.Create(texture, rect, Vector2.zero, pixelsPerUnit, 0,
                                              SpriteMeshType.FullRect);
                sprite.name = "Glyph_" + order[i];
                Sprites.Add(order[i], sprite);
            }
        }
    }
}
