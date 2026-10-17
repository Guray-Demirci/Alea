using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tavla.View
{
    /// <summary>
    /// Slots on the panel that can display a number/text. Which box of the panel art a slot writes to
    /// can be changed in the Inspector.
    /// </summary>
    public enum HudField
    {
        /// <summary>The player's total score.</summary>
        Score,

        /// <summary>The player's gold.</summary>
        Gold,

        /// <summary>The match's task: the score the player must reach. The dice are shown by the dice cards.</summary>
        Target,

        /// <summary>Status line. Shows the remaining turns by default.</summary>
        Status
    }

    /// <summary>
    /// Runtime view of the info panel on the left (general_info.png, exported from general_info_scheme.aseprite).
    ///
    /// The panel sprite is a full 320x180 canvas like the board, so with this object at the origin the panel
    /// lines up with the board. Everything is built in code — panel and text — so the scene object
    /// needs nothing but this component and the sprite.
    ///
    /// NUMBERS: the panel art has four empty parchment strips. This component draws a caption ("SCORE") on the
    /// LEFT of a strip and the value on the RIGHT, and exposes an API that is nothing more than "write this
    /// text in that slot". It does not know WHAT the values are — HudPresenter does. Captions live here, not in
    /// the art, so the caption uses the same font and colour as the value and the box can drop the caption to
    /// make room for the value.
    ///
    /// The boxes were measured pixel by pixel from the art (x right, y DOWN, as in Aseprite) — the same approach
    /// as <see cref="ShopView"/>. The art is a single flattened layer, so boxes cannot be found by layer name:
    /// if the artist moves a strip, update its Box in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>Dark brown of the text, readable on the parchment strips.</summary>
        private static readonly Color TextBrown = new Color32(102, 57, 49, 255);

        /// <summary>Order of the panel art inside the panel's SortingGroup.</summary>
        private const int PanelOrderInGroup = 0;

        /// <summary>Where and how a slot writes. Purely visual settings — holds no data.</summary>
        [System.Serializable]
        private sealed class HudSlot
        {
            public HudField Field;

            [Tooltip("Text area of the slot on the panel art, in canvas pixels (x right, y DOWN, as in Aseprite): " +
                     "the clean parchment of a strip. Caption and value stay inside it.")]
            public RectInt Box;

            [Tooltip("Fixed caption written on the left of the box (\"SCORE\"). If set, the value is right-aligned; " +
                     "if caption and value do not fit side by side the caption is hidden. If empty only the value is drawn.")]
            public string Caption;

            [Tooltip("Pixel offset of the text inside the box. +x right, +y DOWN (Aseprite direction).")]
            public Vector2 PixelOffset;

            [Tooltip("Alignment of the value relative to the box centre when there is NO caption. With a caption the value is always right-aligned.")]
            public PixelTextAlign Align = PixelTextAlign.Center;

            [Tooltip("Colour of the caption and the value. The strips are light parchment; light colours are invisible.")]
            public Color Color = TextBrown;
        }

        /// <summary>Runtime labels of a slot.</summary>
        private sealed class SlotLabels
        {
            public PixelLabel Value;

            /// <summary>Null if no caption is defined.</summary>
            public PixelLabel Caption;

            /// <summary>Pixel width of the box minus the padding on both sides.</summary>
            public int InnerWidth;
        }

        [Header("Art")]
        [Tooltip("The general_info sprite (full 320x180 canvas).")]
        [SerializeField] private Sprite _panelSprite;

        [Header("Sorting")]
        [Tooltip("Sorting order of the panel's SortingGroup. Checkers use orders 10+; a high value keeps the " +
                 "panel in front of them. The group sorts the panel and the text as ONE unit.")]
        [SerializeField] private int _sortingOrder = SortingOrders.HudPanel;

        [Tooltip("Order of the text inside the panel's SortingGroup, above the panel art.")]
        [SerializeField] private int _textSortingOrder = SortingOrders.HudTextInGroup;

        [Header("Text")]
        [Tooltip("Pixels-per-unit and the pixel->world conversion are read from here. " +
                 "If empty, the default geometry (320x180 @ 12 PPU) is used.")]
        [SerializeField] private BoardLayout _layout;

        [Tooltip("Which value is written to which box of the panel art.")]
        [SerializeField]
        private HudSlot[] _slots =
        {
            // The clean parchment of the four strips, top to bottom: status, score, target, gold.
            new HudSlot { Field = HudField.Score,  Box = new RectInt(20, 91, 40, 9),  Caption = "SCORE" },
            new HudSlot { Field = HudField.Gold,   Box = new RectInt(20, 130, 40, 9), Caption = "GOLD" },
            new HudSlot { Field = HudField.Target, Box = new RectInt(20, 111, 40, 9), Caption = "TASK" },
            new HudSlot { Field = HudField.Status, Box = new RectInt(20, 71, 40, 9),  Caption = "TURN" }
        };

        [Tooltip("Pixel distance between the caption/value and the box edges.")]
        [SerializeField] private int _boxPadding = 2;

        [Tooltip("Minimum pixels between caption and value. If they do not fit, the caption is hidden.")]
        [SerializeField] private int _captionGap = 3;

        private readonly Dictionary<HudField, SlotLabels> _labels = new();

        private bool _built;
        private bool _ownsLayout;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            // A layout created in memory is not collected automatically.
            if (_ownsLayout && _layout != null)
            {
                Destroy(_layout);
                _layout = null;
            }
        }

        #region Setup

        /// <summary>
        /// Places the panel and builds the art and the labels.
        ///
        /// Being callable "on demand" instead of only from Awake is deliberate: if the panel is instantiated
        /// at runtime and SetField is called in the same frame, relying on Awake order would be fragile.
        /// The position reset also happens here — everything is placed in canvas space relative to this
        /// object, so it must be at the origin first.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;

            // Same reason as BoardManager: "Create Empty" places the object where the Scene view is looking,
            // so we force the transform back — otherwise the panel drifts off the board.
            if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity ||
                transform.localScale != Vector3.one)
            {
                Debug.LogWarning("[View] HudView was not at the origin; it was reset because the panel is " +
                                 "authored in canvas space. Place the panel object at (0,0,0) to silence this.", this);

                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            if (_layout == null)
            {
                // No asset assigned: create one in memory with the default values (same path as BoardManager).
                _layout = BoardLayout.CreateDefault();
                _ownsLayout = true;
            }

            // The group sorts the panel and the text as one unit: orders inside it are relative to
            // the group, and the whole panel stays in front of the checkers.
            SortingGroup group = GetComponent<SortingGroup>();
            if (group == null)
                group = gameObject.AddComponent<SortingGroup>();

            group.sortingOrder = _sortingOrder;

            BuildArt();
            BuildLabels();
        }

        private void BuildArt()
        {
            if (_panelSprite == null)
            {
                Debug.LogError("[View] HudView has no Panel Sprite — assign general_info. " +
                               "The text is drawn without its panel.", this);
                return;
            }

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(transform, false);

            SpriteRenderer spriteRenderer = panel.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _panelSprite;
            spriteRenderer.sortingOrder = PanelOrderInGroup;
        }

        /// <summary>Creates the value label of each slot and, if defined, its caption label.</summary>
        private void BuildLabels()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                HudSlot slot = _slots[i];

                if (_labels.ContainsKey(slot.Field))
                {
                    Debug.LogWarning($"[View] HUD slot defined twice: {slot.Field}. " +
                                     "The second one was ignored.", this);
                    continue;
                }

                RectInt box = slot.Box;
                SlotLabels labels = new SlotLabels { InnerWidth = box.width - 2 * _boxPadding };

                // Labels centre their capitals on their position, so the box centre centres the text.
                float centerY = box.y + box.height * 0.5f;

                if (string.IsNullOrEmpty(slot.Caption))
                {
                    labels.Value = CreateLabel($"Label_{slot.Field}", box.x + box.width / 2, centerY,
                                               slot, slot.Align);
                }
                else
                {
                    // Caption hugs the left edge, value the right edge; both stay inside the box.
                    labels.Caption = CreateLabel($"Caption_{slot.Field}", box.x + _boxPadding, centerY,
                                                 slot, PixelTextAlign.Left);
                    labels.Caption.SetText(slot.Caption);

                    labels.Value = CreateLabel($"Label_{slot.Field}", box.xMax - _boxPadding, centerY,
                                               slot, PixelTextAlign.Right);
                }

                _labels.Add(slot.Field, labels);
            }
        }

        /// <summary>
        /// A label at a canvas position (pixels, y DOWN). Labels go INSIDE the SortingGroup:
        /// text left outside would either fall behind the panel or poke through the board.
        /// </summary>
        private PixelLabel CreateLabel(string labelName, int pixelX, float pixelY, HudSlot slot, PixelTextAlign align)
        {
            GameObject go = new GameObject(labelName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = _layout.PixelToWorld(pixelX + slot.PixelOffset.x, pixelY + slot.PixelOffset.y);

            PixelLabel label = go.AddComponent<PixelLabel>();
            label.Configure(_layout.PixelsPerUnit, 0, _textSortingOrder, slot.Color, align, PixelTextSize.Regular);

            return label;
        }

        #endregion

        #region Writing values (HudPresenter -> panel)

        /// <summary>Writes text into a slot. Silently ignored if the slot is not defined.</summary>
        public void SetField(HudField field, string text)
        {
            EnsureBuilt();

            SlotLabels labels;
            if (!_labels.TryGetValue(field, out labels))
                return;

            labels.Value.SetText(text);
            FitCaption(labels);
        }

        /// <summary>Shortcut for numbers — so callers do not have to write ToString() everywhere.</summary>
        public void SetField(HudField field, int value)
        {
            SetField(field, value.ToString());
        }

        /// <summary>
        /// Hides the caption if caption and value do not fit side by side. The value is the real information;
        /// the caption only reminds what the bar is. For long texts such as "TURN 5" or the match result,
        /// the box is left to the value; the caption returns once the value gets shorter.
        /// </summary>
        private void FitCaption(SlotLabels labels)
        {
            if (labels.Caption == null)
                return;

            float valueWidth = labels.Value.MeasuredWidth;
            bool fits = valueWidth <= 0f ||
                        labels.Caption.MeasuredWidth + _captionGap + valueWidth <= labels.InnerWidth;

            labels.Caption.SetVisible(fits);
        }

        #endregion
    }
}
