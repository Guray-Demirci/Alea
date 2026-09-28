using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tavla.View
{
    /// <summary>
    /// Slots on the panel that can display a number/text. Names match the Aseprite layers;
    /// which layer a slot writes to can be changed in the Inspector.
    /// </summary>
    public enum HudField
    {
        /// <summary>The player's total score.</summary>
        Score,

        /// <summary>The player's gold.</summary>
        Gold,

        /// <summary>Remaining moves (dice) of the current turn.</summary>
        Moves,

        /// <summary>Task/status line. Shows the remaining turns by default.</summary>
        Task
    }

    /// <summary>
    /// Runtime access to the info panel on the left (general_info_scheme.aseprite).
    ///
    /// THIS SCRIPT DOES NOT CREATE the layers. In "Animated Sprite + Individual Layers" mode the Aseprite
    /// importer generates a prefab with one child GameObject + SpriteRenderer per layer; this component
    /// sits on that prefab's root and finds the children by name.
    ///
    /// Because Pivot Space = Canvas, every layer is at localPosition (0,0) and the Aseprite arrangement
    /// is preserved. The canvas is 320x180 — the same as the board scheme — so with the root at (0,0,0)
    /// the panel is also aligned with the board.
    ///
    /// NUMBERS: the panel art has "Score", "Gold", "Hamle" (moves) and "Task" boxes, but they are empty
    /// (plain white). This component draws a caption ("SCORE") on the LEFT of each box and the value on the
    /// RIGHT, and exposes an API that is nothing more than "write this text in that slot". It does not know
    /// WHAT the values are — HudPresenter does. Captions live here, not in the art, so the caption uses the
    /// same font and colour as the value and the box can shrink the caption to make room for the value.
    ///
    /// Slot positions are NOT ENTERED BY HAND: every label snaps to the visual box of the Aseprite layer
    /// with the same name (caption on the left edge, value on the right edge). If the artist moves the box
    /// on the panel, the text follows automatically; the code does not change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>Brown of the panel background (general_info_scheme). Readable on the white boxes.</summary>
        private static readonly Color PanelBrown = new Color32(102, 57, 49, 255);

        /// <summary>Default box width if the layer is not found — the bars in the scheme are 40 px.</summary>
        private const int FallbackBoxWidth = 40;

        /// <summary>Where and how a slot writes. Purely visual settings — holds no data.</summary>
        [System.Serializable]
        private sealed class HudSlot
        {
            public HudField Field;

            [Tooltip("Aseprite layer name. The value is written into this layer's visual box.")]
            public string LayerName;

            [Tooltip("Fixed caption written on the left of the box (\"SCORE\"). If set, the value is right-aligned; " +
                     "if caption and value do not fit side by side the caption is hidden. If empty only the value is drawn.")]
            public string Caption;

            [Tooltip("Pixel offset relative to the box centre. +x right, +y DOWN (Aseprite direction).")]
            public Vector2 PixelOffset;

            [Tooltip("Alignment of the value relative to the box centre when there is NO caption. With a caption the value is always right-aligned.")]
            public PixelTextAlign Align = PixelTextAlign.Center;

            [Tooltip("Colour of the caption and the value. The boxes are white; light colours are invisible.")]
            public Color Color = PanelBrown;

            [Tooltip("Canvas pixel used if the layer is not found (in the 320x180 scheme).")]
            public Vector2 FallbackPixel;
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

        [Header("Sorting")]
        [Tooltip("Sorting order of the root SortingGroup. Checkers use orders 10+; a high value keeps the " +
                 "panel in front of them. Required because a SortingGroup sorts all its children as ONE unit.")]
        [SerializeField] private int _sortingOrder = SortingOrders.HudPanel;

        [Tooltip("Sorting order that keeps the text ABOVE the panel layers. The importer gives layers small " +
                 "orders starting at 0, so 100 is a comfortable ceiling.")]
        [SerializeField] private int _textSortingOrder = SortingOrders.HudTextInGroup;

        [Header("Text")]
        [Tooltip("Pixels-per-unit and the pixel->world conversion are read from here. " +
                 "If empty, the default geometry (320x180 @ 12 PPU) is used.")]
        [SerializeField] private BoardLayout _layout;

        [Tooltip("Which value is written to which layer. Layer names are the names used in Aseprite.")]
        [SerializeField]
        private HudSlot[] _slots =
        {
            // "Hamle" is the name of the moves layer in the Aseprite file; it must match the art, not the code.
            new HudSlot { Field = HudField.Score, LayerName = "Score", Caption = "SCORE", FallbackPixel = new Vector2(44f, 96f) },
            new HudSlot { Field = HudField.Gold,  LayerName = "Gold",  Caption = "GOLD",  FallbackPixel = new Vector2(44f, 120f) },
            new HudSlot { Field = HudField.Moves, LayerName = "Hamle", Caption = "MOVE",  FallbackPixel = new Vector2(44f, 144f) },
            new HudSlot { Field = HudField.Task,  LayerName = "Task",  Caption = "TASK",  FallbackPixel = new Vector2(44f, 64f) }
        };

        [Tooltip("Pixel distance between the caption/value and the box edges.")]
        [SerializeField] private int _boxPadding = 2;

        [Tooltip("Minimum pixels between caption and value. If they do not fit, the caption is hidden.")]
        [SerializeField] private int _captionGap = 3;

        private readonly Dictionary<string, SpriteRenderer> _layers = new();
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
        /// Places the panel, collects the layers and builds the labels.
        ///
        /// Being callable "on demand" instead of only from Awake is deliberate: if the panel is instantiated
        /// at runtime and SetField is called in the same frame, relying on Awake order would be fragile.
        /// The position reset also happens here — label anchors are computed from the panel's FINAL
        /// position, so the order must not change.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;

            // Same reason as BoardManager: "Create Empty" / dragging a prefab places the object where the
            // Scene view is looking, so we force the transform back — otherwise the panel drifts off the board.
            if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity ||
                transform.localScale != Vector3.one)
            {
                Debug.LogWarning("[View] HudView was not at the origin; it was reset because the panel is " +
                                 "authored in canvas space. Place the panel object at (0,0,0) to silence this.", this);

                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            // The importer adds the SortingGroup to the prefab ROOT. This component may sit on that root
            // or on an object that holds the prefab as a child — both must work.
            SortingGroup group = GetComponentInChildren<SortingGroup>(true);
            if (group != null)
                group.sortingOrder = _sortingOrder;

            CollectLayers();
            BuildLabels(group != null ? group.transform : transform, group != null);
        }

        private void CollectLayers()
        {
            foreach (SpriteRenderer layerRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                // Label glyphs are SpriteRenderers too; do not mistake our own ones for layers.
                if (layerRenderer.GetComponentInParent<PixelLabel>() != null)
                    continue;

                _layers[layerRenderer.gameObject.name] = layerRenderer;
            }

            if (_layers.Count == 0)
                Debug.LogError("[View] HudView has no SpriteRenderer children — the panel is empty. " +
                               "Add this component to the general_info_scheme prefab dragged in from the " +
                               "Project window, not to an EMPTY GameObject.", this);
        }

        /// <summary>
        /// Creates the value label of each slot and, if defined, its caption label.
        /// </summary>
        /// <param name="parent">
        /// Labels go INSIDE the SortingGroup: the group sorts its children as one unit, so text left outside
        /// would either fall behind the panel or poke through the board.
        /// </param>
        /// <param name="insideGroup">
        /// Inside a group the order is RELATIVE TO THE GROUP (starts at 0). Without a group the panel layers
        /// use global orders and the panel order must be added for the text to stay on top.
        /// </param>
        private void BuildLabels(Transform parent, bool insideGroup)
        {
            if (_layout == null)
            {
                // No asset assigned: create one in memory with the default values (same path as BoardManager).
                _layout = BoardLayout.CreateDefault();
                _ownsLayout = true;
            }

            int sortingOrder = insideGroup ? _textSortingOrder : _sortingOrder + _textSortingOrder;
            int sortingLayerId = GetPanelSortingLayerId();

            for (int i = 0; i < _slots.Length; i++)
            {
                HudSlot slot = _slots[i];

                if (_labels.ContainsKey(slot.Field))
                {
                    Debug.LogWarning($"[View] HUD slot defined twice: {slot.Field}. " +
                                     "The second one was ignored.", this);
                    continue;
                }

                int boxWidth;
                Vector3 center = GetSlotBox(slot, out boxWidth);

                SlotLabels labels = new SlotLabels { InnerWidth = boxWidth - 2 * _boxPadding };

                if (string.IsNullOrEmpty(slot.Caption))
                {
                    labels.Value = CreateLabel($"Label_{slot.Field}", parent, center, slot,
                                               slot.Align, sortingLayerId, sortingOrder);
                }
                else
                {
                    // Caption hugs the left edge, value the right edge; both stay inside the box.
                    Vector3 edge = Vector3.right * ((boxWidth * 0.5f - _boxPadding) / _layout.PixelsPerUnit);

                    labels.Caption = CreateLabel($"Caption_{slot.Field}", parent, center - edge, slot,
                                                 PixelTextAlign.Left, sortingLayerId, sortingOrder);
                    labels.Caption.SetText(slot.Caption);

                    labels.Value = CreateLabel($"Label_{slot.Field}", parent, center + edge, slot,
                                               PixelTextAlign.Right, sortingLayerId, sortingOrder);
                }

                _labels.Add(slot.Field, labels);
            }
        }

        private PixelLabel CreateLabel(string labelName, Transform parent, Vector3 position, HudSlot slot,
                                       PixelTextAlign align, int sortingLayerId, int sortingOrder)
        {
            GameObject go = new GameObject(labelName);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            PixelLabel label = go.AddComponent<PixelLabel>();
            label.Configure(_layout.PixelsPerUnit, sortingLayerId, sortingOrder, slot.Color, align);

            return label;
        }

        /// <summary>
        /// Text joins the panel's own sorting layer. If the panel uses a custom layer
        /// (e.g. "UI"), text left on "Default" would be invisible.
        /// </summary>
        private int GetPanelSortingLayerId()
        {
            foreach (KeyValuePair<string, SpriteRenderer> pair in _layers)
                return pair.Value.sortingLayerID;

            return 0;
        }

        /// <summary>
        /// The slot's box: centre in world space and width in pixels. The visual box of the layer with the
        /// same name is used first; if it does not exist, the fallback pixel from the scheme.
        ///
        /// The layer box is read from the SPRITE (sprite.bounds), not the transform: in Pivot Space = Canvas
        /// mode every layer's transform sits at (0,0) and the box information lives entirely in the sprite's pivot.
        /// </summary>
        private Vector3 GetSlotBox(HudSlot slot, out int widthPixels)
        {
            float ppu = _layout.PixelsPerUnit;

            // In Aseprite y grows DOWN; same conversion as BoardLayout.PixelToWorld.
            Vector3 offset = new Vector3(slot.PixelOffset.x / ppu, -slot.PixelOffset.y / ppu, 0f);

            SpriteRenderer layer = FindLayer(slot.LayerName);
            if (layer != null && layer.sprite != null)
            {
                Bounds bounds = layer.sprite.bounds;
                Vector3 min = layer.transform.TransformPoint(bounds.min);
                Vector3 max = layer.transform.TransformPoint(bounds.max);

                widthPixels = Mathf.RoundToInt(Mathf.Abs(max.x - min.x) * ppu);
                return layer.transform.TransformPoint(bounds.center) + offset;
            }

            Debug.LogWarning($"[View] HUD layer not found: \"{slot.LayerName}\" " +
                             $"({slot.Field}). Using the fallback position. Layers on the panel: " +
                             string.Join(", ", _layers.Keys), this);

            widthPixels = FallbackBoxWidth;
            return _layout.PixelToWorld(slot.FallbackPixel.x, slot.FallbackPixel.y) + offset;
        }

        /// <summary>
        /// Finds a layer by name. First an exact match, then a search that tolerates case and special-character
        /// differences — the importer may alter layer names (e.g. "İnfo/Quit") to make them file-name safe.
        /// </summary>
        private SpriteRenderer FindLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return null;

            SpriteRenderer layerRenderer;
            if (_layers.TryGetValue(layerName, out layerRenderer))
                return layerRenderer;

            foreach (KeyValuePair<string, SpriteRenderer> pair in _layers)
            {
                if (pair.Key.IndexOf(layerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return pair.Value;
            }

            return null;
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

        public void ClearField(HudField field)
        {
            SetField(field, string.Empty);
        }

        /// <summary>
        /// The slot's value label — to change its colour (e.g. red when gold runs low) or hide it.
        /// Null if the slot does not exist.
        /// </summary>
        public PixelLabel GetLabel(HudField field)
        {
            EnsureBuilt();

            SlotLabels labels;
            return _labels.TryGetValue(field, out labels) ? labels.Value : null;
        }

        /// <summary>
        /// Hides the caption if caption and value do not fit side by side. The value is the real information;
        /// the caption only reminds what the bar is. For long texts such as "6 6 6 6" on a double or the
        /// match result, the box is left to the value; the caption returns once the value gets shorter.
        /// </summary>
        private void FitCaption(SlotLabels labels)
        {
            if (labels.Caption == null)
                return;

            int valueWidth = labels.Value.MeasuredWidth;
            bool fits = valueWidth == 0 ||
                        labels.Caption.MeasuredWidth + _captionGap + valueWidth <= labels.InnerWidth;

            labels.Caption.SetVisible(fits);
        }

        #endregion

        #region Layers

        /// <summary>
        /// Returns a layer's renderer by name — to swap the avatar, hide a strip or put a collider on it.
        /// The name is the Aseprite layer name ("Avatari", "Gold", "İnfo/Quit"...).
        /// </summary>
        public SpriteRenderer GetLayer(string layerName)
        {
            EnsureBuilt();
            return FindLayer(layerName);
        }

        /// <summary>Name of the avatar layer in the Aseprite file.</summary>
        private const string AvatarLayerName = "Avatari";

        /// <summary>Swaps the avatar at runtime (for character selection).</summary>
        public void SetAvatar(Sprite avatar)
        {
            SpriteRenderer avatarRenderer = GetLayer(AvatarLayerName);
            if (avatarRenderer != null)
                avatarRenderer.sprite = avatar;
        }

        /// <summary>Shows/hides a layer.</summary>
        public void SetLayerVisible(string layerName, bool visible)
        {
            SpriteRenderer layerRenderer = GetLayer(layerName);
            if (layerRenderer != null)
                layerRenderer.enabled = visible;
        }

        #endregion
    }
}
