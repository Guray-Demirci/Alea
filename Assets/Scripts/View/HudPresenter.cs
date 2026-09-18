using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// CORE -> HUD koprusu. Panelde NE yazacagini bilen tek yer burasi.
    ///
    /// Is bolumu:
    ///   Core         : HUD'un varligindan HABERSIZDIR, sadece event firlatir.
    ///   HudPresenter : o event'leri dinler ve metne cevirir (bu dosya).
    ///   HudView      : metni nereye, hangi renkte, hangi sirada basacagini bilir.
    ///
    /// Boylece panel semasi degisince Core'a, oyun kurali degisince panele dokunulmaz.
    /// BoardManager'la ayni sozlesme: burada oyun kurali YOKTUR, tek satir hesap yapilmaz;
    /// gosterilen her sayi Core'dan oldugu gibi okunur.
    ///
    /// Baglanti iki fazlidir (MatchContext <-> GameState ile ayni desen): BoardManager,
    /// Core'u ayaga kaldirdiktan sonra Bind cagirir. Sahnede bu bilesen olmasa da oyun calisir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudPresenter : MonoBehaviour
    {
        [Header("Bağlantı")]
        [Tooltip("Boş bırakılırsa aynı objede, sonra sahnede aranır.")]
        [SerializeField] private HudView _view;

        [Header("Kim gösteriliyor")]
        [Tooltip("Panelin skorunu ve altınını gösterdiği oyuncu. Tek panel var: bot 1 ise burası 0 kalmalı.")]
        [SerializeField] private int _playerId = 0;

        [Header("Biçim")]
        [Tooltip("Kalan zarların arasına konan ayraç.")]
        [SerializeField] private string _diceSeparator = " ";
        [Tooltip("Gösterilecek değer yokken yazılan metin (zar atılmadan önce, hamle bitince).")]
        [SerializeField] private string _emptyValue = "-";
        [Tooltip("Görev satırındaki kalan tur başlığı. Kalan sayı arkasına eklenir.")]
        [SerializeField] private string _turnPrefix = "TUR ";

        private GameState _board;
        private MatchContext _context;

        // Zar listesi her hamlede yeniden kurulur; her seferinde string birlestirmemek icin.
        private readonly StringBuilder _builder = new StringBuilder(16);

        private void Awake()
        {
            if (_view == null)
                _view = GetComponent<HudView>();

            if (_view == null)
                _view = FindAnyObjectByType<HudView>();

            if (_view == null)
                Debug.LogError("[View] HudPresenter bir HudView bulamadi — panel bos kalacak. " +
                               "Bileseni general_info_scheme prefab'ina ekleyin ya da " +
                               "View alanina surukleyin.", this);
        }

        #region Bağlanma

        /// <summary>
        /// Core'a baglanir ve paneli o anki duruma esitler.
        /// BoardManager, GameState + MatchContext kurulduktan HEMEN SONRA cagirir.
        /// </summary>
        public void Bind(GameState board, MatchContext context)
        {
            if (board == null || context == null)
            {
                Debug.LogError("[View] HudPresenter.Bind bos Core aldi; panel guncellenmeyecek.", this);
                return;
            }

            // Iki kez baglanirsa event'ler cift tetiklenmesin.
            Unbind();

            _board = board;
            _context = context;

            _board.OnTurnChanged += HandleTurnChanged;
            _board.OnTurnCountChanged += HandleTurnCountChanged;
            _board.OnMoveExecuted += HandleMoveExecuted;
            _board.OnMatchEnded += HandleMatchEnded;

            _context.OnScoreChanged += HandleScoreChanged;
            _context.OnGoldChanged += HandleGoldChanged;
            _context.OnDiceRolled += HandleDiceRolled;

            RefreshAll();
        }

        /// <summary>
        /// Abonelikleri soker. BoardManager.OnDestroy ile ayni gerekce: Core, yok edilmis
        /// bir MonoBehaviour'i referansta tutarsa sahne degisiminde sizinti olur.
        /// </summary>
        public void Unbind()
        {
            if (_board != null)
            {
                _board.OnTurnChanged -= HandleTurnChanged;
                _board.OnTurnCountChanged -= HandleTurnCountChanged;
                _board.OnMoveExecuted -= HandleMoveExecuted;
                _board.OnMatchEnded -= HandleMatchEnded;
                _board = null;
            }

            if (_context != null)
            {
                _context.OnScoreChanged -= HandleScoreChanged;
                _context.OnGoldChanged -= HandleGoldChanged;
                _context.OnDiceRolled -= HandleDiceRolled;
                _context = null;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        #endregion

        #region Tazeleme

        /// <summary>
        /// Butun slotlari modelden yeniden okur.
        ///
        /// Event'lere ek olarak bu metodun bulunmasi sart: baglanma anindaki degerler icin
        /// hicbir event firlamaz, panel ilk zar atilana kadar bos kalirdi.
        /// </summary>
        public void RefreshAll()
        {
            if (_view == null || _context == null || _board == null)
                return;

            _view.SetField(HudField.Score, _context.GetScore(_playerId));
            _view.SetField(HudField.Gold, _context.GetGold(_playerId));
            _view.SetField(HudField.Moves, FormatMoves());
            _view.SetField(HudField.Task, FormatTurns(_board.RemainingTurns));
        }

        /// <summary>
        /// Gorev satirina disaridan metin yazar — ileride bir gorev/kontrat sistemi
        /// geldiginde baglanacagi yer burasi. Kalan tur yazisinin uzerine gecer.
        /// </summary>
        public void SetTask(string text)
        {
            if (_view != null)
                _view.SetField(HudField.Task, text);
        }

        private void RefreshMoves()
        {
            if (_view != null)
                _view.SetField(HudField.Moves, FormatMoves());
        }

        #endregion

        #region Core dinleyicileri

        private void HandleScoreChanged(int playerId, int score)
        {
            if (playerId == _playerId && _view != null)
                _view.SetField(HudField.Score, score);
        }

        private void HandleGoldChanged(int playerId, int gold)
        {
            if (playerId == _playerId && _view != null)
                _view.SetField(HudField.Gold, gold);
        }

        private void HandleDiceRolled(DiceRoll roll)
        {
            RefreshMoves();
        }

        /// <summary>Hamle yapilinca bir zar harcanir; kalan haklar degisir.</summary>
        private void HandleMoveExecuted(MoveResult result)
        {
            RefreshMoves();
        }

        private void HandleTurnChanged(int playerId)
        {
            // Zar HENUZ atilmadi (BoardManager sirayi aldiktan sonra atiyor) — bu yuzden
            // burada gorunen "-" dogru: yeni turun haklari daha belli degil.
            RefreshMoves();
        }

        private void HandleTurnCountChanged(int remaining)
        {
            if (_view != null)
                _view.SetField(HudField.Task, FormatTurns(remaining));
        }

        private void HandleMatchEnded(int winnerId)
        {
            if (_view == null)
                return;

            string message;
            if (winnerId == GameState.NoWinner)
                message = "BERABERE";
            else if (winnerId == _playerId)
                message = "KAZANDIN";
            else
                message = "KAYBETTIN";

            _view.SetField(HudField.Task, message);
            _view.SetField(HudField.Moves, _emptyValue);
        }

        #endregion

        #region Biçimlendirme

        /// <summary>
        /// Kalan hamle haklari. Iki zar tuketim modu da ayni slotta gosterilir:
        ///   Normal     : kalan zar degerleri ("5 3", cift atista "6 6 6 6"),
        ///   FreeSplit  : bolunebilir tek havuz ("8") — Kartografin Hilesi aktifken.
        /// </summary>
        private string FormatMoves()
        {
            if (_context == null)
                return _emptyValue;

            DiceRoll roll = _context.CurrentRoll;
            if (roll == null || !roll.HasMovesLeft)
                return _emptyValue;

            if (roll.FreeSplit)
                return roll.FreeTotal.ToString();

            IReadOnlyList<int> moves = roll.RemainingMoves;

            _builder.Clear();
            for (int i = 0; i < moves.Count; i++)
            {
                if (i > 0)
                    _builder.Append(_diceSeparator);

                _builder.Append(moves[i]);
            }

            return _builder.ToString();
        }

        private string FormatTurns(int remaining)
        {
            return _turnPrefix + remaining;
        }

        #endregion
    }
}
