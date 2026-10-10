using System.Collections.Generic;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>Alignment of the text relative to its anchor point.</summary>
    public enum PixelTextAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Label that draws a single line of text with <see cref="PixelFont"/>.
    ///
    /// DUMB PUPPET — like NodeView: it does not KNOW what it writes, it only writes. What the score is
    /// and where the gold comes from is HudPresenter's job.
    ///
    /// Every character is a SpriteRenderer and these objects are POOLED: when the text changes no new ones
    /// are created, the sprites of existing ones are swapped and the extras are disabled. This matters because
    /// the HUD updates on every move — otherwise every dice roll would produce garbage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelLabel : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private string _text = string.Empty;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private PixelTextAlign _align = PixelTextAlign.Center;

        [Header("Scale and sorting")]
        [Tooltip("Pixels per unit of the canvas. Must match the board (BoardLayout).")]
        [SerializeField] private float _pixelsPerUnit = 12f;
        [Tooltip("How large the font is drawn on the canvas. Small is for tight spots such as price tags.")]
        [SerializeField] private PixelTextSize _size = PixelTextSize.Regular;
        [Tooltip("Order within the group when inside a SortingGroup, otherwise the global order.")]
        [SerializeField] private int _sortingOrder = SortingOrders.HudTextInGroup;

        private readonly List<SpriteRenderer> _glyphs = new List<SpriteRenderer>();

        private int _sortingLayerId;

        /// <summary>The text currently displayed.</summary>
        public string Text => _text;

        /// <summary>Width of the text in CANVAS pixels — used to check whether it fits in a slot.</summary>
        public float MeasuredWidth => PixelFont.Measure(_text) * PixelFont.GetPixelSize(_size);

        private void Awake()
        {
            Rebuild();
        }

        /// <summary>
        /// Applies all visual settings at once. HudView calls this right after creating the label.
        /// The label's position is the vertical centre of the capitals and, horizontally, the alignment anchor.
        /// </summary>
        public void Configure(float pixelsPerUnit, int sortingLayerId, int sortingOrder,
                              Color color, PixelTextAlign align, PixelTextSize size = PixelTextSize.Regular)
        {
            _pixelsPerUnit = pixelsPerUnit > 0f ? pixelsPerUnit : 1f;
            _sortingLayerId = sortingLayerId;
            _sortingOrder = sortingOrder;
            _color = color;
            _align = align;
            _size = size;

            Rebuild();
        }

        /// <summary>
        /// New text. If the text is unchanged not a SINGLE renderer is touched — the call stays free
        /// even if the HUD refreshes several times per second.
        /// </summary>
        public void SetText(string text)
        {
            if (text == null)
                text = string.Empty;

            if (_text == text)
                return;

            _text = text;
            Rebuild();
        }

        public void SetColor(Color color)
        {
            _color = color;

            for (int i = 0; i < _glyphs.Count; i++)
                _glyphs[i].color = color;
        }

        /// <summary>Shows/hides the label. Objects are kept, only drawing stops.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// Lays the text out as glyphs.
        ///
        /// The layout is done in FONT pixels and the label's scale maps them onto the canvas (half or third
        /// size). Positions are whole font pixels, so every glyph shares the same pixel grid and none of them
        /// looks softer than its neighbours.
        /// </summary>
        private void Rebuild()
        {
            transform.localScale = Vector3.one * PixelFont.GetPixelSize(_size);

            float unit = 1f / (_pixelsPerUnit > 0f ? _pixelsPerUnit : 1f);

            // Glyph sprites are pivoted on the baseline, so centring the capitals means moving the baseline
            // down by half their height. They are 11 px — Floor keeps the baseline on the grid (5 above, 6 below).
            float baseY = Mathf.Floor(-PixelFont.CapHeight * 0.5f) * unit;
            int pen = GetStartPixel();

            int used = 0;

            for (int i = 0; i < _text.Length; i++)
            {
                Sprite glyph = PixelFont.GetGlyph(_text[i], _pixelsPerUnit);

                if (glyph != null) // A space takes room but is not drawn.
                {
                    SpriteRenderer glyphRenderer = GetRenderer(used);
                    used++;

                    glyphRenderer.sprite = glyph;
                    glyphRenderer.color = _color;
                    glyphRenderer.sortingOrder = _sortingOrder;

                    if (_sortingLayerId != 0)
                        glyphRenderer.sortingLayerID = _sortingLayerId;

                    glyphRenderer.enabled = true;
                    glyphRenderer.transform.localPosition = new Vector3(pen * unit, baseY, 0f);
                }

                // The font is proportional: every character moves the pen by its own advance.
                pen += PixelFont.GetAdvance(_text[i]);
            }

            // Leftovers of a shorter text are disabled, not destroyed. The next longer text reuses them.
            for (int i = used; i < _glyphs.Count; i++)
                _glyphs[i].enabled = false;
        }

        /// <summary>Font-pixel offset of the first pen position according to the alignment.</summary>
        private int GetStartPixel()
        {
            int width = PixelFont.Measure(_text);

            // Floor keeps an odd width on whole pixels.
            if (_align == PixelTextAlign.Center)
                return Mathf.FloorToInt(-width * 0.5f);

            if (_align == PixelTextAlign.Right)
                return -width;

            return 0;
        }

        private SpriteRenderer GetRenderer(int index)
        {
            while (_glyphs.Count <= index)
            {
                GameObject go = new GameObject("Glyph_" + _glyphs.Count);
                go.transform.SetParent(transform, false);

                SpriteRenderer created = go.AddComponent<SpriteRenderer>();
                created.enabled = false;
                _glyphs.Add(created);
            }

            return _glyphs[index];
        }
    }
}
