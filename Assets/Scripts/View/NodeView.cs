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
        [FormerlySerializedAs("_hoverColor")]
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 1f, 0.25f);

        private BoxCollider2D _collider;

        /// <summary>Index in Core's GameState.Nodes array (0-23), or GameState.BearOffIndex for the tray.</summary>
        public int NodeIndex => _nodeIndex;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();

            // The collider must be a trigger: checkers must not physically collide with points,
            // only the overlap query "was it dropped here?" is needed.
            _collider.isTrigger = true;

            SetHighlight(false);
        }

        /// <summary>Called by BoardManager while building the scene.</summary>
        public void Initialize(int nodeIndex, Vector3 worldPosition, Vector2 size, Sprite highlightSprite = null)
        {
            _nodeIndex = nodeIndex;
            transform.position = worldPosition;

            if (_collider == null)
                _collider = GetComponent<BoxCollider2D>();

            _collider.isTrigger = true;
            _collider.size = size;
            _collider.offset = Vector2.zero;

            gameObject.name = $"Node_{nodeIndex:00}";

            if (_highlight == null && highlightSprite != null)
                BuildHighlight(highlightSprite, size);

            SetHighlight(false);
        }

        /// <summary>
        /// Builds the highlight visual in code. The sprite is 1 world unit in size,
        /// so its scale is set directly to the point size.
        /// </summary>
        private void BuildHighlight(Sprite sprite, Vector2 size)
        {
            GameObject go = new GameObject("Highlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            _highlight = go.AddComponent<SpriteRenderer>();
            _highlight.sprite = sprite;
            _highlight.color = _highlightColor;
            _highlight.sortingOrder = SortingOrders.NodeHighlight;
        }

        /// <summary>Highlights this point as a legal destination while dragging. Visual only.</summary>
        public void SetHighlight(bool active)
        {
            if (_highlight == null)
                return;

            _highlight.enabled = active;
            _highlight.color = _highlightColor;
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
