using System.Collections.Generic;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// A button's hover glow, made in code from the button's own art so it fits whatever the artist draws: a light
    /// outline hugging the art's outer edge, a fainter halo outside it, and a pale wash over the art itself. Its owner
    /// shows it while the pointer is on the button; it breathes gently meanwhile, so the button reads as "click me".
    ///
    /// Only the OUTER edge glows: a gap the art closes all round (the stall's open front between its posts) stays
    /// clear, so the outline traces the button's shape, not every detail inside it.
    ///
    /// The art's texture must be readable (Read/Write in its import settings); without that there is no glow and a
    /// warning says so. The glow lies exactly over the art: same pixels per unit, same pivot pixel, with
    /// <see cref="Padding"/> pixels of room around it for the outline and the halo.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class HoverGlow : MonoBehaviour
    {
        /// <summary>Room around the art for the outline and the halo.</summary>
        internal const int Padding = 2;

        // Warm candle light. The outline is opaque; the halo and the wash only tint what they cover.
        internal static readonly Color32 OutlineColor = new Color32(255, 244, 170, 255);
        internal static readonly Color32 HaloColor = new Color32(255, 214, 74, 110);
        internal static readonly Color32 WashColor = new Color32(255, 248, 220, 56);

        // The glow breathes from full down to this share of it and back, once per period, starting at full.
        internal const float PulseLow = 0.6f;
        internal const float PulsePeriod = 1.2f;

        private SpriteRenderer _renderer;
        private float _elapsed;

        /// <summary>Is the glow on?</summary>
        public bool Shown => gameObject.activeSelf;

        /// <summary>The glow's current strength, from <see cref="PulseLow"/> to 1.</summary>
        internal float Strength => _renderer.color.a;

        /// <summary>
        /// A hidden glow for <paramref name="art"/>, drawn where the art is when both sit at the origin of
        /// <paramref name="parent"/>. Null if there is no art, or — with a warning — if its texture is not readable.
        /// </summary>
        public static HoverGlow Create(string glowName, Transform parent, Sprite art, int sortingOrder)
        {
            if (art == null)
                return null;

            Texture2D source = art.texture;
            if (!source.isReadable)
            {
                Debug.LogWarning($"[View] {source.name} is not readable, so it has no hover glow. " +
                                 "Tick Read/Write in its import settings.", source);
                return null;
            }

            Rect rect = art.rect;
            int artWidth = (int)rect.width;
            int artHeight = (int)rect.height;

            Color[] pixels = source.GetPixels((int)rect.x, (int)rect.y, artWidth, artHeight);
            bool[] ink = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                ink[i] = pixels[i].a > 0f;

            int width = artWidth + 2 * Padding;
            int height = artHeight + 2 * Padding;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = glowName;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(Paint(ink, artWidth, artHeight));
            texture.Apply();

            Vector2 pivot = (art.pivot + new Vector2(Padding, Padding)) / new Vector2(width, height);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, art.pixelsPerUnit, 0,
                                          SpriteMeshType.FullRect);
            sprite.name = glowName;

            GameObject go = new GameObject(glowName);
            go.transform.SetParent(parent, false);

            SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;

            HoverGlow glow = go.AddComponent<HoverGlow>();
            glow._renderer = spriteRenderer;
            go.SetActive(false);
            return glow;
        }

        /// <summary>Turns the glow on, at full strength, or off.</summary>
        public void SetShown(bool shown)
        {
            if (Shown == shown)
                return;

            gameObject.SetActive(shown);
            if (shown)
            {
                _elapsed = 0f;
                Tick(0f);
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>Moves the breathing on by <paramref name="deltaTime"/> seconds. Unity calls it every frame.</summary>
        internal void Tick(float deltaTime)
        {
            _elapsed = (_elapsed + deltaTime) % PulsePeriod;

            // A cosine: full at the start of each period, weakest halfway through.
            float wave = 0.5f + 0.5f * Mathf.Cos(_elapsed / PulsePeriod * 2f * Mathf.PI);
            _renderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(PulseLow, 1f, wave));
        }

        private void OnDestroy()
        {
            // Made in code, so nothing else frees them.
            if (_renderer != null && _renderer.sprite != null)
            {
                Destroy(_renderer.sprite.texture);
                Destroy(_renderer.sprite);
            }
        }

        /// <summary>
        /// The glow's pixels for art whose opaque pixels are <paramref name="ink"/> (row by row, width
        /// <paramref name="width"/>), on a canvas <see cref="Padding"/> pixels larger on every side: the wash on the
        /// ink, the outline on the clear pixels outside that touch it (diagonals too), the halo on the clear pixels
        /// outside that touch the outline (sides only, which rounds its corners). Everything else is clear.
        /// </summary>
        internal static Color32[] Paint(bool[] ink, int width, int height)
        {
            int glowWidth = width + 2 * Padding;
            int glowHeight = height + 2 * Padding;

            bool[] solid = new bool[glowWidth * glowHeight];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    solid[(y + Padding) * glowWidth + x + Padding] = ink[y * width + x];
            }

            bool[] outside = FloodOutside(solid, glowWidth, glowHeight);
            bool[] outline = new bool[solid.Length];
            Color32[] glow = new Color32[solid.Length];

            for (int y = 0; y < glowHeight; y++)
            {
                for (int x = 0; x < glowWidth; x++)
                {
                    int i = y * glowWidth + x;
                    if (solid[i])
                        glow[i] = WashColor;
                    else if (outside[i] && Touches(solid, glowWidth, glowHeight, x, y, true))
                    {
                        outline[i] = true;
                        glow[i] = OutlineColor;
                    }
                }
            }

            for (int y = 0; y < glowHeight; y++)
            {
                for (int x = 0; x < glowWidth; x++)
                {
                    int i = y * glowWidth + x;
                    if (outside[i] && !outline[i] && Touches(outline, glowWidth, glowHeight, x, y, false))
                        glow[i] = HaloColor;
                }
            }

            return glow;
        }

        /// <summary>
        /// The clear pixels reachable from the canvas edge without crossing <paramref name="solid"/> ones. The padding
        /// makes the edge one clear ring, so flooding from a corner reaches all of it.
        /// </summary>
        private static bool[] FloodOutside(bool[] solid, int width, int height)
        {
            bool[] outside = new bool[solid.Length];
            Stack<int> open = new Stack<int>();
            outside[0] = true;
            open.Push(0);

            while (open.Count > 0)
            {
                int i = open.Pop();
                int x = i % width;
                int y = i / width;

                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);
            }

            return outside;

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                    return;

                int i = y * width + x;
                if (outside[i] || solid[i])
                    return;

                outside[i] = true;
                open.Push(i);
            }
        }

        /// <summary>Is one of the pixel's neighbours set? Its four sides, and with <paramref name="diagonals"/> its corners too.</summary>
        private static bool Touches(bool[] pixels, int width, int height, int x, int y, bool diagonals)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || (!diagonals && dx != 0 && dy != 0))
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && pixels[ny * width + nx])
                        return true;
                }
            }

            return false;
        }
    }
}
