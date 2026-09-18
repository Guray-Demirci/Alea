using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// Tahtadaki tek bir hanenin (ucgenin) birakma hedefi.
    /// TEK sorumlulugu: "ben kacinci haneyim?" sorusuna cevap vermek.
    ///
    /// Bilerek aptal tutuldu — bu haneye pul konabilir mi, dolu mu, rakibin kapisi mi,
    /// hicbirini BILMEZ ve sormaz. O sorularin cevabi sadece GameState'te vardir.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class NodeView : MonoBehaviour
    {
        [SerializeField] private int _nodeIndex;

        [Header("Görsel geri bildirim (opsiyonel)")]
        [SerializeField] private SpriteRenderer _highlight;
        [SerializeField] private Color _hoverColor = new Color(1f, 1f, 1f, 0.25f);

        private BoxCollider2D _collider;

        /// <summary>Core tarafindaki GameState.Nodes dizisindeki indeks (0-23).</summary>
        public int NodeIndex => _nodeIndex;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();

            // Collider tetikleyici olmali: pullar hanelere fiziksel olarak carpmasin,
            // sadece OverlapPoint ile "uzerine birakildi mi" tespiti yapilsin.
            _collider.isTrigger = true;

            SetHighlight(false);
        }

        /// <summary>BoardManager sahneyi kurarken cagirir.</summary>
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
        /// Vurgu gorselini koddan kurar. Sprite 1 dunya birimi boyutunda oldugu icin
        /// olcegi dogrudan hane boyutuna esitliyoruz.
        /// </summary>
        private void BuildHighlight(Sprite sprite, Vector2 size)
        {
            GameObject go = new GameObject("Highlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            _highlight = go.AddComponent<SpriteRenderer>();
            _highlight.sprite = sprite;
            _highlight.color = _hoverColor;
            _highlight.sortingOrder = 0; // Zeminin (-100) ustunde, pullarin (10+) altinda.
        }

        /// <summary>Surukleme sirasinda pulun uzerinde durdugu haneyi vurgular. Sadece gorsel.</summary>
        public void SetHighlight(bool active)
        {
            if (_highlight == null)
                return;

            _highlight.enabled = active;
            _highlight.color = _hoverColor;
        }

        // Editorde hane sinirlarini ve indeksini gorebilmek icin.
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
