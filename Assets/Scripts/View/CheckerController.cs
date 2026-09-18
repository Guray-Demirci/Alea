using UnityEngine;
using Tavla.Core.Checkers;

namespace Tavla.View
{
    /// <summary>
    /// Sahnedeki tek bir pulun gorsel temsilcisi + surukle-birak girdisi.
    ///
    /// APTAL KUKLA SOZLESMESI — bu script:
    ///   • Mesafe hesaplamaz, hane dolulugu sorgulamaz, sira kimde bakmaz.
    ///   • Birakildigi anda kendi konumunu ASLA kendisi belirlemez.
    ///   • Tek yaptigi: "su haneden su haneye gitmek istiyorum" deyip Core'a sormak.
    /// Hamle gecerliyse de gecersizse de yeni konum Core'dan gelen event ile atanir.
    /// Bu yuzden gecersiz hamlede "geri sicrama" kodu yazmaya gerek yoktur; kendiliginden olur.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class CheckerController : MonoBehaviour
    {
        [Header("Hareket")]
        [Tooltip("Hedefe kayma hızı (birim/saniye).")]
        [SerializeField] private float _moveSpeed = 14f;

        [Header("Sürükleme")]
        [Tooltip("Sürüklerken diğer pulların üstünde görünmesi için eklenen sıralama değeri.")]
        [SerializeField] private int _dragSortingBoost = 500;

        private BoardManager _board;
        private BaseChecker _model;
        private SpriteRenderer _renderer;
        private BoxCollider2D _collider;
        private Camera _camera;

        private Vector3 _targetPosition;
        private Vector3 _grabOffset;
        private int _restingSortingOrder;
        private bool _isDragging;


        /// <summary>Bu gorselin temsil ettigi Core nesnesi. View bunu SADECE kimlik olarak kullanir.</summary>
        public BaseChecker Model => _model;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<BoxCollider2D>();
            _camera = Camera.main;
        }

        /// <summary>BoardManager pulu sahneye basarken cagirir.</summary>
        public void Bind(BoardManager board, BaseChecker model, Color color)
        {
            _board = board;
            _model = model;

            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            // NOT: Kaynak sprite BEYAZ olmali. checker_scheme.aseprite siyah siluet oldugu icin
            // tint carpma ile calismaz (siyah x herhangi bir renk = siyah).
            _renderer.color = color;

            gameObject.name = $"Checker_{model.TypeId}_P{model.OwnerId}";
        }

        /// <summary>
        /// Yeni dinlenme konumu. SADECE BoardManager, Core'dan event geldiginde cagirir.
        /// Surukleme sirasinda gelen cagrilar hedefi gunceller ama gorsel takibi bozmaz.
        /// </summary>
        public void SetTarget(Vector3 position, int sortingOrder)
        {
            _targetPosition = position;
            _restingSortingOrder = sortingOrder;

            if (!_isDragging)
                _renderer.sortingOrder = sortingOrder;
        }

        /// <summary>Animasyonsuz aninda yerlestirme (ilk kurulum icin).</summary>
        public void SnapToTarget()
        {
            transform.position = _targetPosition;
        }

        private void Update()
        {
            // Suruklenirken fare komutu gecerlidir; diger her an hedefe dogru kayariz.
            if (_isDragging)
                return;

            transform.position = Vector3.MoveTowards(
                transform.position, _targetPosition, _moveSpeed * Time.deltaTime);
        }

        #region Surukle-Birak

        private void OnMouseDown()
        {
            if (_board == null)
                return;

            _isDragging = true;

            // Fareyle pulun merkezi arasindaki farki koru ki pul kenarindan tutunca ziplamasin.
            _grabOffset = transform.position - GetPointerWorldPosition();

            _renderer.sortingOrder = _restingSortingOrder + _dragSortingBoost;

            // Nereye gidebilecegini CORE'a sorup vurgulat. View kural hesaplamiyor,
            // sadece Core'un verdigi listeyi boyuyor.
            _board.ShowLegalTargets(_board.GetSourceIndex(_model));
        }

        private void OnMouseDrag()
        {
            if (!_isDragging)
                return;

            transform.position = GetPointerWorldPosition() + _grabOffset;
        }

        private void OnMouseUp()
        {
            if (!_isDragging)
                return;

            _isDragging = false;
            _renderer.sortingOrder = _restingSortingOrder;

            _board.ClearHighlights();

            NodeView target = FindNodeUnderPointer();

            if (target == null)
            {
                // Tahta disina birakildi: Core'a hic sormuyoruz, hamle denemesi bile degil.
                // Konumu yine de Core'un durumundan tazeliyoruz ki elle tasinmis gorsel kalmasin.
                _board.RefreshBoard();
                return;
            }

            // Kaynak indeksi de MODELDEN soruyoruz — pul kendi nerede oldugunu bilmez.
            int fromIndex = _board.GetSourceIndex(_model);

            _board.RequestMove(fromIndex, target.NodeIndex);

            // Buradan sonra hicbir sey yapmiyoruz. Kabul de edilse ret de edilse
            // yeni konum OnMoveExecuted / OnMoveRejected event'i uzerinden gelecek.
        }

        private Vector3 GetPointerWorldPosition()
        {
            if (_camera == null)
                _camera = Camera.main;

            Vector3 screen = Input.mousePosition;
            screen.z = -_camera.transform.position.z; // Ortografik kamerada duzleme izdusum

            Vector3 world = _camera.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }

        /// <summary>Fare imlecinin altindaki hane. Bulamazsa null.</summary>
        private NodeView FindNodeUnderPointer()
        {
            Vector3 world = GetPointerWorldPosition();
            Collider2D[] hits = Physics2D.OverlapPointAll(world);

            for (int i = 0; i < hits.Length; i++)
            {
                NodeView node = hits[i].GetComponent<NodeView>();
                if (node != null)
                    return node;
            }

            return null;
        }

        #endregion
    }
}
