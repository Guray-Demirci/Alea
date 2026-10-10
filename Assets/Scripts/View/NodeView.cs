using UnityEngine;
using UnityEngine.Serialization;

namespace Tavla.View
{
    /// <summary>
    /// Drop target of a single point (triangle) on the board.
    /// ONE responsibility: answering "which point am I?".
    ///
    /// Deliberately kept dumb — whether a checker may be placed here, whether it is full or held by the
    /// opponent, it KNOWS none of it and asks nothing. Those answers exist only in GameState.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class NodeView : MonoBehaviour
    {
        [SerializeField] private int _nodeIndex;

        [Header("Visual feedback (optional)")]
        [SerializeField] private SpriteRenderer _highlight;
        [Tooltip("Tint of a fill highlight (and of a highlight assigned here). Artwork highlights keep their own colours.")]
        [FormerlySerializedAs("_hoverColor")]
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 1f, 0.25f);

        private BoxCollider2D _collider;

        // Shown while the point carries the human player's score mark (Navigator footprints).
        private SpriteRenderer _mark;

        /// <summary>Index in Core's GameState.Nodes array (0-23), or GameState.BearOffIndex for the tray.</summary>
        public int NodeIndex => _nodeIndex;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();

            // The collider must be a trigger: checkers must not physically collide with points,
            // only the overlap query "was it dropped here?" is needed.
            _collider.isTrigger = true;

            // A highlight assigned in a prefab takes the tint; one built later chooses its own colour.
            if (_highlight != null)
                _highlight.color = _highlightColor;

            SetHighlight(false);
        }

        /// <summary>Called by BoardManager while building the scene.</summary>
        public void Initialize(int nodeIndex, Vector3 worldPosition, Vector2 size)
        {
            _nodeIndex = nodeIndex;
            transform.position = worldPosition;

            if (_collider == null)
                _collider = GetComponent<BoxCollider2D>();

            _collider.isTrigger = true;
            _collider.size = size;
            _collider.offset = Vector2.zero;

            gameObject.name = $"Node_{nodeIndex:00}";

            SetHighlight(false);
        }

        /// <summary>
        /// Highlight drawn from pixel artwork at its own size, centred on the point, in its own colours.
        /// <paramref name="flipY"/> mirrors it, so the bottom row reuses the top row's artwork.
        /// Does nothing if a highlight is already assigned (prefab).
        /// </summary>
        public void BuildArtworkHighlight(Sprite artwork, bool flipY)
        {
            if (_highlight != null)
                return;

            _highlight = CreateHighlightRenderer(artwork, Vector2.one, Color.white);
            _highlight.flipY = flipY;
            SetHighlight(false);
        }

        /// <summary>
        /// Highlight drawn from a solid sprite of 1 world unit, stretched over the whole drop area and tinted
        /// with the highlight colour — for targets without artwork. Does nothing if a highlight is already assigned.
        /// </summary>
        public void BuildFillHighlight(Sprite solid, Vector2 size)
        {
            if (_highlight != null)
                return;

            _highlight = CreateHighlightRenderer(solid, size, _highlightColor);
            SetHighlight(false);
        }

        private SpriteRenderer CreateHighlightRenderer(Sprite sprite, Vector2 scale, Color color)
        {
            GameObject go = new GameObject("Highlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

            SpriteRenderer highlight = go.AddComponent<SpriteRenderer>();
            highlight.sprite = sprite;
            highlight.color = color;
            highlight.sortingOrder = SortingOrders.NodeHighlight;
            return highlight;
        }

        /// <summary>Highlights this point as a legal destination while dragging. Visual only.</summary>
        public void SetHighlight(bool active)
        {
            if (_highlight == null)
                return;

            _highlight.enabled = active;
        }

        /// <summary>
        /// The point's score mark (Navigator footprints): <paramref name="sprite"/> in the given tint at
        /// <paramref name="worldPosition"/>, hidden until <see cref="SetMarked"/> shows it. Does nothing if already built.
        /// </summary>
        public void BuildMark(Sprite sprite, Color color, Vector3 worldPosition)
        {
            if (_mark != null || sprite == null)
                return;

            GameObject go = new GameObject("Mark");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPosition;

            _mark = go.AddComponent<SpriteRenderer>();
            _mark.sprite = sprite;
            _mark.color = color;
            _mark.sortingOrder = SortingOrders.PointMark;
            _mark.enabled = false;
        }

        /// <summary>Shows or hides the point's score mark. Visual only — whether it is marked is Core's answer.</summary>
        public void SetMarked(bool marked)
        {
            if (_mark == null)
                return;

            _mark.enabled = marked;
        }

        // Shows the point bounds in the editor.
        private void OnDrawGizmosSelected()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
        }
    }
}
