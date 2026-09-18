using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.AI;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// CORE ile UNITY arasindaki TEK kopru. Burada oyun kurali YOKTUR.
    ///
    /// Gorevleri:
    ///   1) Core'u ayaga kaldirmak (GameState + MatchContext),
    ///   2) sahneyi kurmak (24 hane + toplama alani + pullar),
    ///   3) CheckerController'dan gelen istekleri TryMove'a iletmek,
    ///   4) Core'dan gelen event'lerde gorseli tazelemek.
    ///
    /// KRITIK TASARIM: Pul konumlari hamle verisinden HESAPLANMAZ. Her event sonrasi
    /// RefreshBoard() cagrilir ve tahta, modelin o anki durumundan yeniden okunur.
    /// Bu sayede Kiyamet Pulu'nun toplu kirmasi, Girdap'in isinlamasi, Anka'nin geri
    /// donmesi gibi 30 farkli ozel durum icin tek satir ekstra View kodu gerekmez.
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        private const int CheckerSortingBase = 10;
        private const int BarSortingBase = 200;

        /// <summary>Bir turda botun yapabilecegi azami hamle. Sonsuz donguye karsi emniyet.</summary>
        private const int MaxBotMovesPerTurn = 12;

        [Header("Geometri")]
        [Tooltip("Boş bırakılırsa varsayılan (aseprite şemasından ölçülmüş) değerlerle çalışır.")]
        [SerializeField] private BoardLayout _layout;

        [Header("Sprite")]
        [Tooltip("checker_scheme sprite'ı. Prefab'a gerek yok — pul objeleri koddan üretilir.")]
        [SerializeField] private Sprite _checkerSprite;
        [Tooltip("play_game_scheme sprite'ı. Atanırsa zemin olarak sahneye basılır.")]
        [SerializeField] private Sprite _boardSprite;

        [Header("Prefab (opsiyonel)")]
        [Tooltip("Doldurulursa pullar bu prefab'tan üretilir; boşsa yukarıdaki sprite ile koddan.")]
        [SerializeField] private CheckerController _checkerPrefab;
        [Tooltip("Boş bırakılırsa haneler koddan üretilir (görünmez collider).")]
        [SerializeField] private NodeView _nodePrefab;

        [Header("Oyuncu renkleri")]
        [SerializeField] private Color _player0Color = Color.white;
        [SerializeField] private Color _player1Color = new Color(0.15f, 0.15f, 0.18f);

        [Header("Toplama alanı")]
        [Tooltip("Toplama alanının sürekli görünen soluk zemini. Vurgu yalnızca sürüklerken " +
                 "yanıyor; bu zemin olmadan oyuncu tahtanın dışında bir bırakma yeri olduğunu fark etmez.")]
        [SerializeField] private Color _bearOffTrayColor = new Color(1f, 1f, 1f, 0.08f);

        [Header("Bilgi paneli")]
        [Tooltip("Sol paneldeki skor/altın/hamle yazılarını basan sunucu. Boş bırakılırsa " +
                 "sahnede aranır; hiç yoksa panel devre dışı kalır, oyun aynen çalışır.")]
        [SerializeField] private HudPresenter _hud;

        [Header("Kamera")]
        [SerializeField] private bool _configureCamera = true;

        [Header("Yapay zeka")]
        [Tooltip("Botun oynadığı oyuncu. -1 = bot kapalı, iki taraf da elle oynanır.")]
        [SerializeField] private int _botPlayerId = 1;
        [Tooltip("Bot hamle yapmadan önceki düşünme süresi (sn).")]
        [SerializeField] private float _botThinkDelay = 0.6f;
        [Tooltip("Botun ardışık hamleleri arasındaki bekleme (sn).")]
        [SerializeField] private float _botMoveDelay = 0.35f;

        [Header("Tur")]
        [Tooltip("İnsan oyuncunun oynayabileceği hamle kalmayınca tur kendiliğinden devredilsin mi?")]
        [SerializeField] private bool _autoEndTurn = true;
        [Tooltip("Hamle kalmadığı anlaşıldıktan sonra sıra devredilmeden önceki bekleme (sn).")]
        [SerializeField] private float _autoEndDelay = 0.6f;
        [Tooltip("Turu elle bitirme tuşu (oto-geçişin yakalayamadığı durumlar için emniyet).")]
        [SerializeField] private KeyCode _endTurnKey = KeyCode.Space;

        [Header("Determinizm")]
        [Tooltip("0 = her oyunda farklı. Sıfırdan farklıysa zar ve şans sonuçları tekrarlanabilir.")]
        [SerializeField] private int _randomSeed = 0;

        private GameState _gameState;
        private MatchContext _context;
        private TavlaBot _bot;
        private Coroutine _botRoutine;
        private Coroutine _autoEndRoutine;

        // Core nesnesi -> sahne temsilcisi. View'in modele tek bagi bu sozluktur.
        private readonly Dictionary<BaseChecker, CheckerController> _checkerViews =
            new Dictionary<BaseChecker, CheckerController>();

        private readonly List<NodeView> _nodeViews = new List<NodeView>();

        // _nodeViews hane indeksiyle (0-23) adresleniyor; toplama hedefi o listeye girmez.
        private NodeView _bearOffView;

        public GameState Board => _gameState;
        public MatchContext Match => _context;

        #region Kurulum

        private void Start()
        {
            if (_layout == null)
            {
                // Asset atanmadiysa varsayilan degerlerle bellekte bir tane uret.
                _layout = ScriptableObject.CreateInstance<BoardLayout>();
                Debug.Log("[View] BoardLayout atanmamış, varsayılan geometri kullanılıyor.");
            }

            if (_checkerPrefab == null && _checkerSprite == null)
            {
                Debug.LogError("[View] Checker Sprite alanına 'checker_scheme' sürüklenmeli " +
                               "(veya bir Checker Prefab atanmalı). BoardManager devre dışı.");
                enabled = false;
                return;
            }

            BuildCore();
            BuildScene();
            Subscribe();
            BindHud();

            // Bot aciksa olustur. -1 verilirse iki taraf da elle oynanir.
            if (_botPlayerId >= 0 && _botPlayerId < GameState.PlayerCount)
                _bot = new TavlaBot(_botPlayerId);

            RefreshBoard(true);

            // Ilk turun zarini at, sonra sira bottaysa onu baslat.
            _context.RollDice(_gameState.CurrentPlayerId);
            TryStartBotTurn(_gameState.CurrentPlayerId);
            ScheduleAutoEndCheck();
        }

        private void BuildCore()
        {
            _gameState = new GameState();

            IRandomProvider random = _randomSeed == 0
                ? new SystemRandomProvider()
                : new SystemRandomProvider(_randomSeed);

            // MatchContext yapicisinda kendini tahtaya baglar (Bind);
            // bu olmadan ozel pullarin yetenekleri sessizce calismaz.
            _context = new MatchContext(_gameState, random);

            _gameState.SetupStandardBoard();
        }

        private void BuildScene()
        {
            if (_configureCamera)
                ConfigureCamera();

            if (_boardSprite != null)
                SpawnBoardBackground();

            Sprite solid = CreateSolidSprite();
            SpawnNodes(solid);
            SpawnBearOffTarget(solid);
            SpawnCheckers();
        }

        /// <summary>
        /// Bilgi panelini Core'a baglar.
        ///
        /// Zar/skor/altin degerlerini View'a tasiyan event'ler zaten var; panel bunlara
        /// KENDI abone olur, BoardManager sadece tanistirir. Bu yuzden yeni bir HUD alani
        /// eklemek bu dosyayi degistirmez.
        ///
        /// Ilk zar atisindan (Start'in sonu) ONCE cagrilmali: aksi halde panel ilk turun
        /// zarlarini kacirir ve sira degisene kadar bos kalir.
        /// </summary>
        private void BindHud()
        {
            if (_hud == null)
                _hud = FindAnyObjectByType<HudPresenter>();

            // Panel sahnede olmayabilir (test sahnesi, sadece tahta). Oyun aynen calisir.
            if (_hud == null)
                return;

            _hud.Bind(_gameState, _context);
        }

        /// <summary>
        /// Kamerayi tahtaya gore ayarlar. Sahnede hic kamera yoksa kendisi olusturur —
        /// "Display 1: No cameras rendering" hatasina karsi guvenlik agi.
        /// </summary>
        private void ConfigureCamera()
        {
            Camera cam = Camera.main;

            if (cam == null)
                cam = FindAnyObjectByType<Camera>();

            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                Debug.Log("[View] Sahnede kamera yoktu, oluşturuldu.");
            }

            cam.orthographic = true;
            cam.orthographicSize = _layout.CameraOrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Uretilen tum gorseller SAHNE KOKUNDE, dunya orijininde durur.
        /// GameManager objesinin konumuna/olcegine baglanmazlar — "Create Empty" objeyi
        /// (0,0,0)'a degil Scene view'in baktigi noktaya koydugu icin bu sart:
        /// aksi halde tahta zemini pullardan ayri kayar.
        /// </summary>
        private static Transform CreateWorldRoot(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private void SpawnBoardBackground()
        {
            Transform board = CreateWorldRoot("BoardBackground");

            SpriteRenderer renderer = board.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _boardSprite;
            renderer.sortingOrder = -100;
        }

        /// <summary>24 haneyi geometriye gore uretir. Elle 24 obje yerlestirmeye gerek yok.</summary>
        private void SpawnNodes(Sprite highlight)
        {
            Transform root = CreateWorldRoot("Nodes");

            Vector2 size = _layout.GetNodeSize();

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                NodeView view;

                if (_nodePrefab != null)
                {
                    view = Instantiate(_nodePrefab, root);
                }
                else
                {
                    GameObject go = new GameObject("Node");
                    go.transform.SetParent(root, false);
                    go.AddComponent<BoxCollider2D>();
                    view = go.AddComponent<NodeView>();
                }

                view.Initialize(i, _layout.GetNodeCenter(i), size, highlight);
                _nodeViews.Add(view);
            }
        }

        /// <summary>
        /// Toplama (bear-off) birakma hedefi. Mekanik olarak siradan bir NodeView — tek farki
        /// indeksinin GameState.BearOffIndex olmasi. CheckerController bu yuzden hicbir sey
        /// bilmeden RequestMove(from, BearOffIndex) gonderir; TryMove bunu zaten toplama
        /// olarak yorumluyor. Hane prefab'i bilerek kullanilmiyor: onun vurgusu ucgen
        /// boyutuna gore hazirlanmis olur, bu seride uymaz.
        /// </summary>
        private void SpawnBearOffTarget(Sprite solid)
        {
            Transform root = CreateWorldRoot("BearOff");
            Vector2 size = _layout.GetBearOffSize();

            GameObject go = new GameObject("BearOffTray");
            go.transform.SetParent(root, false);
            go.AddComponent<BoxCollider2D>();

            _bearOffView = go.AddComponent<NodeView>();
            _bearOffView.Initialize(GameState.BearOffIndex, _layout.GetBearOffCenter(), size, solid);
            go.name = "BearOffTray"; // Initialize "Node_-02" yaziyor.

            GameObject tray = new GameObject("Tray");
            tray.transform.SetParent(go.transform, false);
            tray.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = tray.AddComponent<SpriteRenderer>();
            renderer.sprite = solid;
            renderer.color = _bearOffTrayColor;
            renderer.sortingOrder = -50; // Zeminin (-100) ustunde, vurgunun (0) altinda.
        }

        /// <summary>Vurgu icin 1x1 beyaz sprite — asset gerektirmesin diye koddan uretilir.</summary>
        private static Sprite CreateSolidSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            // pixelsPerUnit = 1 -> sprite tam 1 dunya birimi, olcekle boyutlandirilir.
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        private void SpawnCheckers()
        {
            Transform root = CreateWorldRoot("Checkers");

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Node node = _gameState.Nodes[i];
                for (int k = 0; k < node.StoneCount; k++)
                    SpawnCheckerView(node.Checkers[k], root);
            }
        }

        /// <summary>Deste insasinda yeni bir ozel pul geldiginde de kullanilabilir.</summary>
        public CheckerController SpawnCheckerView(BaseChecker model, Transform parent = null)
        {
            if (_checkerViews.ContainsKey(model))
                return _checkerViews[model];

            CheckerController view = _checkerPrefab != null
                ? Instantiate(_checkerPrefab, parent)
                : CreateRuntimeChecker(parent);

            view.Bind(this, model, model.OwnerId == 0 ? _player0Color : _player1Color);

            _checkerViews.Add(model, view);
            return view;
        }

        /// <summary>
        /// Prefab yoksa pul objesini koddan kurar. Editorde prefab hazirlama adimini
        /// tamamen ortadan kaldirir — tek gereken Inspector'daki sprite alani.
        /// </summary>
        private CheckerController CreateRuntimeChecker(Transform parent)
        {
            GameObject go = new GameObject("Checker");
            go.transform.SetParent(parent, false);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _checkerSprite;

            float size = _layout.CheckerWorldSize;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(size, size);

            return go.AddComponent<CheckerController>();
        }

        #endregion

        #region Abonelik

        private void Subscribe()
        {
            _gameState.OnMoveExecuted += HandleMoveExecuted;
            _gameState.OnMoveRejected += HandleMoveRejected;
            _gameState.OnStoneCaptured += HandleStoneCaptured;
            _gameState.OnCheckerDestroyed += HandleCheckerDestroyed;
            _gameState.OnCheckerTeleported += HandleCheckerTeleported;
            _gameState.OnTileTriggered += HandleTileTriggered;
            _gameState.OnAbilityTriggered += HandleAbilityTriggered;
            _gameState.OnTurnChanged += HandleTurnChanged;
            _gameState.OnTurnCountChanged += HandleTurnCountChanged;
            _gameState.OnCheckerBorneOff += HandleCheckerBorneOff;
            _gameState.OnMatchEnded += HandleMatchEnded;

            _context.OnDiceRolled += HandleDiceRolled;
            _context.OnScoreChanged += HandleScoreChanged;
            _context.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDestroy()
        {
            // Abonelik sokulmezse Core, yok edilmis MonoBehaviour'i referansta tutar
            // ve sahne degisiminde MissingReferenceException + bellek sizintisi olur.
            if (_gameState != null)
            {
                _gameState.OnMoveExecuted -= HandleMoveExecuted;
                _gameState.OnMoveRejected -= HandleMoveRejected;
                _gameState.OnStoneCaptured -= HandleStoneCaptured;
                _gameState.OnCheckerDestroyed -= HandleCheckerDestroyed;
                _gameState.OnCheckerTeleported -= HandleCheckerTeleported;
                _gameState.OnTileTriggered -= HandleTileTriggered;
                _gameState.OnAbilityTriggered -= HandleAbilityTriggered;
                _gameState.OnTurnChanged -= HandleTurnChanged;
                _gameState.OnTurnCountChanged -= HandleTurnCountChanged;
                _gameState.OnCheckerBorneOff -= HandleCheckerBorneOff;
                _gameState.OnMatchEnded -= HandleMatchEnded;
            }

            if (_context != null)
            {
                _context.OnDiceRolled -= HandleDiceRolled;
                _context.OnScoreChanged -= HandleScoreChanged;
                _context.OnGoldChanged -= HandleGoldChanged;
            }
        }

        #endregion

        #region View -> Core

        /// <summary>
        /// CheckerController pulu biraktiginda buraya gelir. Tek yaptigimiz soruyu iletmek;
        /// gecerlilik karari tamamen Core'a ait.
        /// </summary>
        public void RequestMove(int fromIndex, int toIndex)
        {
            _gameState.TryMove(fromIndex, toIndex);
        }

        /// <summary>
        /// Bir pulun SU ANDA hangi hanede oldugunu modele sorar.
        /// Pul kendi konumunu tutmaz — tek dogruluk kaynagi GameState'tir.
        /// Bardaysa GameState.BarIndex (-1) doner ve TryMove bunu bar girisi olarak yorumlar.
        /// </summary>
        public int GetSourceIndex(BaseChecker checker)
        {
            return _gameState.FindNodeIndexOf(checker);
        }

        /// <summary>
        /// Pul kaldirildiginda gidebilecegi haneleri vurgular.
        /// Kural hesabi View'da DEGIL — Core'un CanMove'u ayni dogrulama kodundan geciyor,
        /// bu yuzden "yesil yanan hane reddedilir" tutarsizligi olusamaz.
        /// </summary>
        public void ShowLegalTargets(int fromIndex)
        {
            ClearHighlights();

            List<int> targets = _gameState.GetLegalTargets(fromIndex);
            for (int i = 0; i < targets.Count; i++)
                _nodeViews[targets[i]].SetHighlight(true);

            // GetLegalTargets sadece 0-23 hanelerini tariyor (bot da toplamayi ayrica soruyor).
            // Toplama vurgusu ayni Core dogrulamasindan, CanBearOff'tan geciyor.
            bool canBearOff = _gameState.CanBearOff(fromIndex);
            if (_bearOffView != null)
                _bearOffView.SetHighlight(canBearOff);

            if (targets.Count == 0 && !canBearOff)
                Debug.Log($"[View] Hane {fromIndex} için bu zarlarla geçerli hamle yok.");
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < _nodeViews.Count; i++)
                _nodeViews[i].SetHighlight(false);

            if (_bearOffView != null)
                _bearOffView.SetHighlight(false);
        }

        /// <summary>
        /// Insan oyuncunun turu elle bitirmesi: kisayol tusu, UI butonu (OnClick) veya ContextMenu.
        /// Normalde gerek yok — hamle kalmayinca tur kendiliginden devrediliyor
        /// (bkz. AutoEndTurnWhenStuck). Bu, oto-gecisin yakalayamadigi durumlar icin emniyet.
        /// Botun turunu kesmez; bot kendi turunu RunBotTurn icinde bitirir.
        /// </summary>
        [ContextMenu("Turu Bitir")]
        public void EndTurn()
        {
            // Play disinda ContextMenu'den cagrilirsa Core henuz kurulmamis olur.
            if (_gameState == null || !IsHumanTurn)
                return;

            CancelAutoEnd();
            ClearHighlights();
            _gameState.EndTurn();
        }

        private void Update()
        {
            if (_gameState != null && Input.GetKeyDown(_endTurnKey))
                EndTurn();
        }

        #endregion

        #region Core -> View

        /// <summary>
        /// Tahtayi modelin o anki durumundan bastan dizer.
        /// Hamle verisine bakmaz — bu yuzden isinlanma, toplu kirma, yok olma
        /// gibi tum ozel durumlar ekstra kod olmadan dogru gorunur.
        /// </summary>
        public void RefreshBoard(bool instant = false)
        {
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Node node = _gameState.Nodes[i];

                // Toplam sayiyi da geciyoruz: yigin buyukse BoardLayout araligi daraltip
                // pullari ucgenin icinde tutuyor (artik 15 pul tek haneye yigilabiliyor).
                int count = node.StoneCount;
                for (int k = 0; k < count; k++)
                    PlaceChecker(node.Checkers[k], _layout.GetCheckerPosition(i, k, count),
                                 CheckerSortingBase + k, instant);
            }

            for (int p = 0; p < GameState.PlayerCount; p++)
            {
                IReadOnlyList<BaseChecker> bar = _gameState.GetBar(p);
                for (int k = 0; k < bar.Count; k++)
                    PlaceChecker(bar[k], _layout.GetBarPosition(p, k, bar.Count),
                                 BarSortingBase + k, instant);
            }
        }

        private void PlaceChecker(BaseChecker model, Vector3 position, int sortingOrder, bool instant)
        {
            CheckerController view;
            if (!_checkerViews.TryGetValue(model, out view))
                return;

            view.SetTarget(position, sortingOrder);

            if (instant)
                view.SnapToTarget();
        }

        private void HandleMoveExecuted(MoveResult result)
        {
            string suffix = result.WasInfiltration ? " [sızma]" : string.Empty;
            string to = result.WasBearOff ? "toplama" : result.ToIndex.ToString();
            Debug.Log($"[View] Hamle: {result.FromIndex} -> {to} " +
                      $"(Oyuncu {result.PlayerId}, kırılan: {result.CapturedCheckers.Count}){suffix}");

            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        private void HandleMoveRejected(MoveResult result)
        {
            Debug.LogWarning($"[View] Hamle reddedildi: {result.Error}");

            // Suruklenip elde kalmis pulu yerine oturtur. Ayri bir "geri sicra" koduna gerek yok.
            RefreshBoard();
        }

        private void HandleStoneCaptured(BaseChecker checker, int nodeIndex, CaptureDestination destination)
        {
            switch (destination)
            {
                case CaptureDestination.HomeEntry:
                    Debug.Log($"[View] {checker.TypeId} küllerinden doğdu, başlangıç hanesine döndü.");
                    break;
                case CaptureDestination.Destroyed:
                    Debug.Log($"[View] {checker.TypeId} kendini feda etti.");
                    break;
                default:
                    Debug.Log($"[View] {checker.TypeId} kırıldı, bara gitti (hane {nodeIndex}).");
                    break;
            }

            // TODO: kirilma VFX'i burada oynatilacak. Konum RefreshBoard'un isi.
        }

        private void HandleCheckerDestroyed(BaseChecker checker, int nodeIndex)
        {
            CheckerController view;
            if (_checkerViews.TryGetValue(checker, out view))
            {
                _checkerViews.Remove(checker);
                Destroy(view.gameObject);
            }

            Debug.Log($"[View] {checker.TypeId} oyundan silindi (hane {nodeIndex}).");
        }

        private void HandleCheckerTeleported(BaseChecker checker, int fromIndex, int toIndex)
        {
            Debug.Log($"[View] {checker.TypeId} girdaba kapıldı: {fromIndex} -> {toIndex}");
        }

        private void HandleTileTriggered(ITileEffect tile, int nodeIndex, BaseChecker checker)
        {
            Debug.Log($"[View] Karo tetiklendi: {tile.EffectId} (hane {nodeIndex})");
        }

        /// <summary>
        /// TEK event, 30 farkli VFX. Yeni bir ozel pul eklendiginde BoardManager degismez —
        /// sadece abilityName -> ParticleSystem sozlugune bir satir girer.
        /// </summary>
        private void HandleAbilityTriggered(string abilityName, BaseChecker checker, int nodeIndex)
        {
            Debug.Log($"[View] Yetenek: {abilityName} (hane {nodeIndex})");
        }

        private void HandleTurnChanged(int playerId)
        {
            Debug.Log($"[View] Sıra: Oyuncu {playerId}");
            _context.RollDice(playerId);

            // Zar atildiktan SONRA botu uyandir — eli belli olmadan dusunemez.
            TryStartBotTurn(playerId);
            ScheduleAutoEndCheck();
        }

        private void HandleTurnCountChanged(int remaining)
        {
            Debug.Log($"[View] Kalan tur: {remaining}");
        }

        private void HandleCheckerBorneOff(BaseChecker checker, int nodeIndex)
        {
            Debug.Log($"[View] Pul toplandı: {checker.TypeId} (hane {nodeIndex})");
        }

        private void HandleMatchEnded(int winnerId)
        {
            StopBotTurn();
            CancelAutoEnd();

            string message = winnerId == GameState.NoWinner
                ? "Maç berabere bitti."
                : $"Maç bitti — kazanan: Oyuncu {winnerId}";

            Debug.Log($"[View] {message} " +
                      $"(P0: {_context.GetScore(0)} — P1: {_context.GetScore(1)})");
        }

        #endregion

        #region Yapay zeka

        /// <summary>
        /// Sira bottaysa tur rutinini baslatir. Bot kapaliysa veya mac bittiyse hicbir sey yapmaz.
        /// </summary>
        private void TryStartBotTurn(int playerId)
        {
            if (_bot == null || playerId != _botPlayerId || _gameState.IsMatchOver)
                return;

            StopBotTurn();
            _botRoutine = StartCoroutine(RunBotTurn());
        }

        private void StopBotTurn()
        {
            if (_botRoutine == null)
                return;

            StopCoroutine(_botRoutine);
            _botRoutine = null;
        }

        /// <summary>
        /// Botun turu. Hamle hakki bitene kadar tek tek oynar, sonra turu devreder.
        ///
        /// Gecikmeler sadece OYUNCU ICIN: bot aslinda aninda karar veriyor, ama pullar
        /// bir anda yer degistirirse ne oldugu anlasilmiyor.
        /// </summary>
        private IEnumerator RunBotTurn()
        {
            yield return new WaitForSeconds(_botThinkDelay);

            int moveCount = 0;

            while (!_gameState.IsMatchOver
                   && _gameState.CurrentPlayerId == _botPlayerId
                   && moveCount < MaxBotMovesPerTurn)
            {
                // PlayOneMove false donerse oynanacak hamle kalmamistir.
                if (!_bot.PlayOneMove(_gameState))
                    break;

                moveCount++;
                yield return new WaitForSeconds(_botMoveDelay);
            }

            // Rutin bitmeden temizle: EndTurn yeni bir tur baslatip bu alani yeniden yazacak.
            _botRoutine = null;

            if (!_gameState.IsMatchOver && _gameState.CurrentPlayerId == _botPlayerId)
                _gameState.EndTurn();
        }

        #endregion

        #region Tur gecisi

        /// <summary>Sira insanda mi? Bot kapaliysa iki taraf da insandir.</summary>
        private bool IsHumanTurn =>
            !_gameState.IsMatchOver && (_bot == null || _gameState.CurrentPlayerId != _botPlayerId);

        /// <summary>
        /// Insan oyuncunun hamlesi kalip kalmadigini kontrol ettirir. Her hamleden ve her
        /// zar atisindan sonra cagrilir — boylece hem "zarlar harcandi" hem de
        /// "atilan zarla hic oynanamiyor" durumlari ayni yoldan cozulur.
        /// </summary>
        private void ScheduleAutoEndCheck()
        {
            CancelAutoEnd();

            if (!_autoEndTurn || !IsHumanTurn)
                return;

            _autoEndRoutine = StartCoroutine(AutoEndTurnWhenStuck(_gameState.CurrentPlayerId));
        }

        private void CancelAutoEnd()
        {
            if (_autoEndRoutine == null)
                return;

            StopCoroutine(_autoEndRoutine);
            _autoEndRoutine = null;
        }

        /// <summary>
        /// Bekleme iki ise yariyor: birlesik hamle (3-2 ile 5 hane) her adimda ayri
        /// OnMoveExecuted firlattigi icin karar plan bitmeden VERILMIYOR, ve oyuncu
        /// son hamlesini gorup ne oldugunu anlayabiliyor.
        /// </summary>
        private IEnumerator AutoEndTurnWhenStuck(int playerId)
        {
            yield return new WaitForSeconds(_autoEndDelay);

            // Rutin bitmeden temizle: EndTurn yeni bir tur baslatip bu alani yeniden yazabilir.
            _autoEndRoutine = null;

            if (_gameState.IsMatchOver
                || _gameState.CurrentPlayerId != playerId
                || _gameState.HasAnyLegalMove())
                yield break;

            Debug.Log($"[View] Oyuncu {playerId} için oynanabilir hamle kalmadı, sıra devrediliyor.");
            ClearHighlights();
            _gameState.EndTurn();
        }

        #endregion

        #region MatchContext dinleyicileri

        private void HandleDiceRolled(DiceRoll roll)
        {
            Debug.Log($"[View] Zar: {roll.First}-{roll.Second}" + (roll.IsDouble ? " (çift!)" : string.Empty));
        }

        private void HandleScoreChanged(int playerId, int score) => Debug.Log($"[View] Skor P{playerId}: {score}");

        private void HandleGoldChanged(int playerId, int gold) => Debug.Log($"[View] Altın P{playerId}: {gold}");

        #endregion
    }
}
