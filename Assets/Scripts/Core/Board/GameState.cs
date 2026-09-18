using System;
using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    /// <summary>
    /// Oyunun TEK dogruluk kaynagi. Tamamen saf C#'tir.
    ///
    /// TASARIM SOZU: Bu sinifin icinde tek bir ozel pul veya karo ismi GECMEZ.
    /// 18 pul ve 8 karo eklendigi halde GameState hicbirini tanimiyor; sadece
    /// "bu pulda su yetenek var mi?" (GetAbility) ve "bu karo ne diyor?" (ITileEffect)
    /// diye soruyor. Yeni bir pul eklemek bu dosyayi DEGISTIRMEZ (Open/Closed).
    /// </summary>
    public class GameState
    {
        public const int NodeCount = 24;
        public const int PlayerCount = 2;
        public const int MaxDieValue = 6;
        public const int BarIndex = -1;

        /// <summary>Oyuncu basina pul sayisi. Hepsini toplayan maci erken bitirir.</summary>
        public const int CheckersPerPlayer = 15;

        /// <summary>
        /// Bir hanede durabilecek pul sayisi: 5.
        ///
        /// Bu bir TAVAN degil, VARSAYILAN. Node.GetStackLimit hanedeki pullara
        /// "bu siniri degistiren var mi?" diye soruyor; Kara Delik Pulu evet deyip
        /// sinirsiza cekiyor. Yani 15 pulu tek haneye yigmak hala mumkun — ama artik
        /// bedava degil, o pulu satin almayi gerektiriyor.
        /// </summary>
        public const int DefaultStackLimit = 5;

        #region Arcade kurallari

        /// <summary>
        /// Toplama (bear-off) hedefi. Tahta uzerinde bir hane DEGIL, tahtanin disi.
        /// BarIndex (-1) ile cakismasin diye -2 secildi; 0-23 araligina asla girmez.
        /// </summary>
        public const int BearOffIndex = -2;

        /// <summary>Ev bolgesinin genisligi (son 6 hane).</summary>
        public const int HomeBoardSize = 6;

        /// <summary>Bir rakip pulu kirmanin HAM puani. Carpanlar MatchContext icinde uygulanir.</summary>
        public const int CaptureScore = 10;

        /// <summary>Bir pulu toplamanin HAM puani.</summary>
        public const int BearOffScore = 20;

        /// <summary>Varsayilan mac uzunlugu — iki oyuncunun toplam tur sayisi.</summary>
        public const int DefaultTurnLimit = 30;

        /// <summary>Kazanan belirlenemedi (beraberlik veya skor sistemi bagli degil).</summary>
        public const int NoWinner = -1;

        #endregion

        private readonly Node[] _nodes = new Node[NodeCount];
        private readonly List<BaseChecker>[] _bar = new List<BaseChecker>[PlayerCount];
        private readonly bool[] _skipNextTurn = new bool[PlayerCount];
        private readonly int[] _borneOff = new int[PlayerCount];

        public IReadOnlyList<Node> Nodes => _nodes;
        public int CurrentPlayerId { get; private set; }

        /// <summary>Kalan tur hakki. EndTurn her cagrildiginda bir azalir.</summary>
        public int RemainingTurns { get; private set; }

        /// <summary>Mac bitti mi? Bittiginde TryMove hicbir hamleyi kabul etmez.</summary>
        public bool IsMatchOver { get; private set; }

        /// <summary>Mac bittiginde kazanan oyuncu; beraberlikte NoWinner.</summary>
        public int WinnerId { get; private set; } = NoWinner;

        /// <summary>
        /// Yeteneklerin oyuna dokunma kapisi. Iki fazli kurulur (Bind) cunku
        /// MatchContext'in tahtaya, tahtanin da context'e ihtiyaci var.
        /// </summary>
        public IMatchContext Context { get; private set; }

        /// <summary>
        /// Klon tahtalar pul ve karo NESNELERINI gercek tahtayla PAYLASIR. Bayrak acikken
        /// yan etkili kancalar hic calismaz — yoksa bot dusunurken Kasif'in gezdigi haneler,
        /// Kusatma'nin tetiklenmesi veya kalkan haklari gibi GERCEK pul durumlari bozulurdu.
        /// Kurallar (yon, mesafe, zar, blokaj, kirma, toplama) aynen islemeye devam eder.
        /// </summary>
        public bool IsSimulation { get; private set; }

        /// <summary>
        /// Yan etkili kancalar (yetenek tepkileri, karo etkileri, kirilma bildirimleri)
        /// calissin mi? Context bagli degilse ya da bu bir simulasyonsa hayir.
        /// </summary>
        private bool SideEffectsEnabled => Context != null && !IsSimulation;

        #region Olaylar (Core -> View)

        /// <summary>Bir pul kirildi. (kirilan pul, hane indeksi, gittigi yer)</summary>
        public event Action<BaseChecker, int, CaptureDestination> OnStoneCaptured;

        public event Action<MoveResult> OnMoveExecuted;
        public event Action<MoveResult> OnMoveRejected;

        /// <summary>Bir pul oyundan tamamen silindi (Fedakar Pul, Barut Ficisi).</summary>
        public event Action<BaseChecker, int> OnCheckerDestroyed;

        /// <summary>Pul zorla baska haneye tasindi (Girdap Hanesi). (pul, eski hane, yeni hane)</summary>
        public event Action<BaseChecker, int, int> OnCheckerTeleported;

        /// <summary>Bir ozel karo tetiklendi. (karo, hane indeksi, etkilenen pul)</summary>
        public event Action<ITileEffect, int, BaseChecker> OnTileTriggered;

        /// <summary>
        /// Genel yetenek bildirimi — View'in VFX basmasi icin.
        /// (yetenek adi, pul, hane indeksi). Her yeni pul icin ayri event acmaya gerek kalmaz.
        /// </summary>
        public event Action<string, BaseChecker, int> OnAbilityTriggered;

        public event Action<int> OnTurnChanged;

        /// <summary>
        /// Bir pul toplandi (bear-off). (pul, ciktigi hane)
        /// GORSEL temizligi OnCheckerDestroyed yapar; bu event OYUN anlamini tasir —
        /// skor patlamasi, sayac, ses gibi seyler buna baglanir.
        /// </summary>
        public event Action<BaseChecker, int> OnCheckerBorneOff;

        /// <summary>Kalan tur sayisi degisti. (kalan tur)</summary>
        public event Action<int> OnTurnCountChanged;

        /// <summary>Mac bitti. (kazanan oyuncu; beraberlikte NoWinner)</summary>
        public event Action<int> OnMatchEnded;

        #endregion

        /// <param name="turnLimit">
        /// Macin toplam tur hakki. Tasarimci bunu degistirebilsin diye sabit degil parametre.
        /// </param>
        public GameState(int turnLimit = DefaultTurnLimit)
        {
            for (int i = 0; i < NodeCount; i++)
                _nodes[i] = new Node(i);

            for (int p = 0; p < PlayerCount; p++)
                _bar[p] = new List<BaseChecker>();

            CurrentPlayerId = 0;
            RemainingTurns = turnLimit < 1 ? 1 : turnLimit;
        }

        /// <summary>MatchContext kendini tahtaya tanitir. Sadece kurulumda bir kez cagrilir.</summary>
        public void Bind(IMatchContext context)
        {
            Context = context;
        }

        /// <summary>Yetenekler ve karolar View'a haber vermek icin bunu kullanir.</summary>
        public void ReportAbility(string abilityName, BaseChecker checker, int nodeIndex)
        {
            OnAbilityTriggered?.Invoke(abilityName, checker, nodeIndex);
        }

        #region Kurulum

        public void SetupStandardBoard(Func<int, BaseChecker> checkerFactory = null)
        {
            if (checkerFactory == null)
                checkerFactory = ownerId => new DefaultChecker(ownerId);

            PlaceCheckers(0, 0, 2, checkerFactory);
            PlaceCheckers(0, 11, 5, checkerFactory);
            PlaceCheckers(0, 16, 3, checkerFactory);
            PlaceCheckers(0, 18, 5, checkerFactory);

            PlaceCheckers(1, 23, 2, checkerFactory);
            PlaceCheckers(1, 12, 5, checkerFactory);
            PlaceCheckers(1, 7, 3, checkerFactory);
            PlaceCheckers(1, 5, 5, checkerFactory);
        }

        private void PlaceCheckers(int ownerId, int nodeIndex, int count, Func<int, BaseChecker> factory)
        {
            for (int i = 0; i < count; i++)
                _nodes[nodeIndex].Push(factory(ownerId));
        }

        /// <summary>Deste insasi: standart bir pulu ozel pulla degistirir (dukkan satin almasi sonrasi).</summary>
        public bool ReplaceChecker(int nodeIndex, BaseChecker oldChecker, BaseChecker newChecker)
        {
            // Kurulum yolu: pul "haneyi terk etmiyor", yerine bir baskasi geciyor.
            // Karo kancasi bilerek tetiklenmiyor (mac baslamadan Sis/Bataklik kaydi olamaz).
            if (!IsOnBoard(nodeIndex) || !_nodes[nodeIndex].Remove(oldChecker))
                return false;

            _nodes[nodeIndex].Push(newChecker);
            return true;
        }

        public void PlaceTile(int nodeIndex, ITileEffect effect)
        {
            if (IsOnBoard(nodeIndex))
                _nodes[nodeIndex].SetTile(effect);
        }

        #endregion

        #region Klon tahta (yapay zeka icin)

        /// <summary>
        /// Yapay zekanin "su hamleyi yaparsam ne olur?" denemesi icin tahtanin
        /// paralel bir evrenini yaratir.
        ///
        /// SIG KLON: pul ve karo nesneleri paylasilir. Bilincli bir tercih — bot sadece
        /// TAHTA DUZENINI degerlendiriyor (kim nerede, kac acik, kac kapi), pullarin ic
        /// durumunu degil. 30 pulu derin kopyalamak hem pahali hem gereksiz olurdu.
        /// Paylasimin tehlikeli tarafini IsSimulation bayragi kapatiyor.
        ///
        /// Zarlar da kopyalanir: aksi halde CanAffordDistance "Context yok" sanip
        /// 1-6 arasi her mesafeyi gecerli sayardi ve bot elinde olmayan zarlarla plan yapardi.
        /// </summary>
        public GameState Clone()
        {
            GameState copy = new GameState(RemainingTurns);

            copy.IsSimulation = true;
            copy.CurrentPlayerId = CurrentPlayerId;
            copy.IsMatchOver = IsMatchOver;
            copy.WinnerId = WinnerId;

            for (int p = 0; p < PlayerCount; p++)
            {
                copy._borneOff[p] = _borneOff[p];
                copy._skipNextTurn[p] = _skipNextTurn[p];
                copy._bar[p].AddRange(_bar[p]);
            }

            for (int i = 0; i < NodeCount; i++)
            {
                Node original = _nodes[i];
                Node cloned = copy._nodes[i];

                if (original.ActiveTile != null)
                    cloned.SetTile(original.ActiveTile);

                // En alttan en uste dogru diz ki yigin sirasi bozulmasin.
                for (int c = 0; c < original.StoneCount; c++)
                    cloned.Push(original.Checkers[c]);
            }

            copy.Bind(new SimulationContext(copy, Context));

            return copy;
        }

        #endregion

        #region Sorgular

        public int GetBarCount(int playerId) => _bar[playerId].Count;

        /// <summary>
        /// Bardaki pullarin salt-okunur listesi. View'in kirik pullari ekrana dizebilmesi icin;
        /// Core kural mantigi bunu kullanmaz.
        /// </summary>
        public IReadOnlyList<BaseChecker> GetBar(int playerId) => _bar[playerId];

        public static bool IsOnBoard(int index) => index >= 0 && index < NodeCount;

        public static int GetDirection(int playerId) => playerId == 0 ? 1 : -1;

        /// <summary>Anka Pulu'nun geri donecegi baslangic hanesi.</summary>
        public static int GetHomeEntryIndex(int playerId) => playerId == 0 ? 0 : NodeCount - 1;

        private static int GetBarOrigin(int playerId) => playerId == 0 ? -1 : NodeCount;

        /// <summary>Bir pulun tahtadaki hane indeksi; bulunamazsa -1.</summary>
        public int FindNodeIndexOf(BaseChecker checker)
        {
            for (int i = 0; i < NodeCount; i++)
            {
                Node node = _nodes[i];
                for (int c = 0; c < node.StoneCount; c++)
                {
                    if (ReferenceEquals(node.Checkers[c], checker))
                        return i;
                }
            }
            return BarIndex;
        }

        #endregion

        #region Hamle

        /// <summary>
        /// View'in cagirdigi TEK giris noktasi.
        ///
        /// Iki asamali: once tek zarla dogrudan hamle denenir. Zar yetmiyorsa
        /// (3-2 elindeyken 5 hane gitmek gibi) zarlari birlestiren bir yol aranir.
        /// </summary>
        public bool TryMove(int fromIndex, int toIndex)
        {
            MoveResult result = ResolveMove(fromIndex, toIndex, CurrentPlayerId, true);

            if (result.Success)
            {
                OnMoveExecuted?.Invoke(result);
                return true;
            }

            // Sadece "zar yetmedi" hatalarinda birlesik hamleyi deniyoruz.
            // Bu iki kontrol ResolveMove'da hedef hane kontrollerinden ONCE geldigi icin
            // buraya duserken hicbir yan etki olusmamistir (kalkan harcanmamistir).
            if (result.Error == MoveError.NoDiceValue || result.Error == MoveError.InvalidDistance)
            {
                if (TryCompoundMove(fromIndex, toIndex, CurrentPlayerId))
                    return true;
            }

            OnMoveRejected?.Invoke(result);
            return false;
        }

        /// <summary>
        /// Hamle gecerli mi? Tahtayi DEGISTIRMEZ, event firlatmaz, kalkan harcamaz.
        /// View bunu "nereye gidebilirim?" vurgusu icin kullanir — kuralin kendisi
        /// yine Core'da kalir, View sadece cevabi boyar.
        /// </summary>
        public bool CanMove(int fromIndex, int toIndex)
        {
            return ResolveMove(fromIndex, toIndex, CurrentPlayerId, false).Success;
        }

        /// <summary>
        /// Bu hedefe TEK zarla ya da zarlari birlestirerek ulasilabilir mi?
        /// View'in vurgu listesi bunu kullanir — oyuncu 3-2 elindeyken 5 hane otedeki
        /// haneyi de yaniyor gorur.
        /// </summary>
        public bool CanReach(int fromIndex, int toIndex)
        {
            if (CanMove(fromIndex, toIndex))
                return true;

            return CanPlanCompound(fromIndex, toIndex, CurrentPlayerId);
        }

        /// <summary>Verilen haneden gidilebilecek tum hedefler. Sirasi gelen oyuncu icin hesaplanir.</summary>
        public List<int> GetLegalTargets(int fromIndex)
        {
            List<int> targets = new List<int>();

            for (int to = 0; to < NodeCount; to++)
            {
                if (to != fromIndex && CanReach(fromIndex, to))
                    targets.Add(to);
            }

            return targets;
        }

        /// <summary>
        /// Sirasi gelen oyuncunun oynayabilecegi en az bir hamle var mi? Tahtaya DOKUNMAZ.
        /// View bunu "zarlar bitti / hamle kalmadi, turu devret" karari icin kullanir.
        /// Kaynak secimi TavlaBot.CollectSources ile ayni: bardaki pul varsa tek kaynak bardir.
        /// </summary>
        public bool HasAnyLegalMove()
        {
            if (IsMatchOver)
                return false;

            // Ucuz kisa devre: zar hakki tukendiyse 24x24 taramaya gerek yok.
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll != null && !roll.HasMovesLeft)
                return false;

            int player = CurrentPlayerId;

            if (GetBarCount(player) > 0)
                return HasAnyTarget(BarIndex);

            for (int i = 0; i < NodeCount; i++)
            {
                if (_nodes[i].CountFor(player) == 0)
                    continue;

                if (CanBearOff(i) || HasAnyTarget(i))
                    return true;
            }

            return false;
        }

        /// <summary>GetLegalTargets'in liste uretmeyen, ilk bulusta duran hali.</summary>
        private bool HasAnyTarget(int fromIndex)
        {
            for (int to = 0; to < NodeCount; to++)
            {
                if (to != fromIndex && CanReach(fromIndex, to))
                    return true;
            }

            return false;
        }

        #endregion

        #region Birlesik hamle (zarlari tek pulda toplama)

        /// <summary>
        /// Zarlari birlestirerek bu hedefe ulasan bir yol var mi? Tahtaya DOKUNMAZ.
        /// </summary>
        private bool CanPlanCompound(int fromIndex, int toIndex, int playerId)
        {
            List<int> plan = new List<int>();
            return TryBuildPlan(fromIndex, toIndex, playerId, plan);
        }

        /// <summary>
        /// 3-2 elindeyken 5 hane gitmek gibi hamleleri uygular.
        ///
        /// KRITIK: Bu tek bir sicrama DEGIL. Plan, arka arkaya GERCEK hamleler olarak
        /// calistirilir — yani ara durak da gercekten konaklanir. Boylece ara hanedeki
        /// tek rakip pulu kirilir, oradaki kazik/sis/girdap karosu tetiklenir ve
        /// yetenekler calisir. Hicbiri icin ayrica kod yazmak gerekmiyor.
        /// </summary>
        private bool TryCompoundMove(int fromIndex, int toIndex, int playerId)
        {
            List<int> plan = new List<int>();
            if (!TryBuildPlan(fromIndex, toIndex, playerId, plan))
                return false;

            int direction = GetDirection(playerId);
            int current = fromIndex;
            bool anyStepDone = false;

            for (int i = 0; i < plan.Count; i++)
            {
                int next = current + plan[i] * direction;

                MoveResult step = ResolveMove(current, next, playerId, true);

                if (!step.Success)
                {
                    // Ara durakta bir karo veya yetenek plani bozdu. Pul oldugu yerde kalir;
                    // hic adim atilmadiysa reddi cagiran katman bildirir.
                    return anyStepDone;
                }

                anyStepDone = true;
                OnMoveExecuted?.Invoke(step);

                // Pul isinlanmis (Girdap), kirilmis (Kazik) veya toplanmis olabilir.
                // Bir sonraki adimi planlanan yerden degil, GERCEK konumundan surduruyoruz.
                BaseChecker mover = step.MovedChecker;
                current = FindNodeIndexOf(mover);

                if (current != next)
                    break; // Plan bozuldu; kalan adimlar iptal.
            }

            return anyStepDone;
        }

        /// <summary>
        /// Eldeki zarlardan hedefe ulasan bir dizi arar (derinlik en fazla 4 — cift atisi).
        /// Zar cokluk kumesi elle yonetilir: ayni degeri elde oldugundan fazla kullanamaz.
        /// </summary>
        private bool TryBuildPlan(int fromIndex, int toIndex, int playerId, List<int> plan)
        {
            if (!IsOnBoard(fromIndex) || !IsOnBoard(toIndex) || fromIndex == toIndex)
                return false;

            if (IsMatchOver || GetBarCount(playerId) > 0)
                return false;

            DiceRoll roll = Context != null ? Context.CurrentRoll : null;

            // Zar sistemi yoksa ya da Kartografin Hilesi zaten serbest bolme veriyorsa
            // birlestirmeye gerek yok — o yollar tek hamlede hallediyor.
            if (roll == null || roll.FreeSplit)
                return false;

            BaseChecker mover = PeekSource(fromIndex, playerId);
            if (mover == null || mover.OwnerId != playerId)
                return false;

            // Kaynak hanedeki kilit (Bataklik / Zindan) birlesik hamleyi de engeller.
            Node source = _nodes[fromIndex];
            if (source.ActiveTile != null && !source.ActiveTile.CanLeaveFrom(mover))
                return false;

            List<int> available = new List<int>(roll.RemainingMoves);

            // Tek zarla zaten gidilebiliyorsa burasi cagrilmamis olmali; yine de
            // en az iki zar gerektiren yollari ariyoruz.
            return SearchPlan(mover, fromIndex, toIndex, playerId, available, plan);
        }

        private bool SearchPlan(BaseChecker mover, int currentIndex, int targetIndex, int playerId,
                                List<int> available, List<int> plan)
        {
            int direction = GetDirection(playerId);

            for (int i = 0; i < available.Count; i++)
            {
                int die = available[i];

                // Ayni degeri bu seviyede iki kez denemeyelim (cift atisinda gereksiz dallanma).
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if (available[j] == die)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                    continue;

                int next = currentIndex + die * direction;

                if (!IsOnBoard(next))
                    continue;

                // Hedefi astiysak bu dal olu.
                if ((next - targetIndex) * direction > 0)
                    continue;

                if (!CanOccupy(mover, next, playerId))
                    continue;

                available.RemoveAt(i);
                plan.Add(die);

                if (next == targetIndex)
                    return true;

                if (SearchPlan(mover, next, targetIndex, playerId, available, plan))
                    return true;

                plan.RemoveAt(plan.Count - 1);
                available.Insert(i, die);
            }

            return false;
        }

        /// <summary>
        /// "Bu pul bu haneye konabilir mi?" — ARA DURAK tahmini.
        /// ResolveMove kullanilamiyor cunku pul henuz orada degil (PeekSource bos donerdi).
        /// Nihai karar yine ResolveMove'da verilir; burasi sadece planlamayi yonlendirir.
        /// Aralarinda bir uyusmazlik olursa birlesik hamle erken durur, tahta bozulmaz.
        /// </summary>
        private bool CanOccupy(BaseChecker mover, int toIndex, int playerId)
        {
            Node target = _nodes[toIndex];

            if (target.ActiveTile != null && !target.ActiveTile.CanLandOn(mover))
                return false;

            if (target.CountEnemiesOf(playerId) > 0)
            {
                IAreaCapture area = mover.GetAbility<IAreaCapture>();
                IInfiltrator infiltrator = mover.GetAbility<IInfiltrator>();
                IBlockadeBypass bypass = mover.GetAbility<IBlockadeBypass>();

                bool allowed =
                    (area != null && area.CanCaptureEntirePoint(target)) ||
                    (infiltrator != null && infiltrator.CanInfiltrate(target)) ||
                    (bypass != null && bypass.CanBypassBlockade(target)) ||
                    !target.IsBlockedFor(playerId);

                if (!allowed)
                    return false;
            }

            return target.CountFor(playerId) + 1 <= target.GetStackLimit(DefaultStackLimit);
        }

        #endregion

        #region Toplama (Bear-Off)

        /// <summary>Bu hane verilen oyuncunun ev bolgesinde (son 6 hane) mi?</summary>
        public static bool IsInHomeBoard(int nodeIndex, int playerId)
        {
            if (!IsOnBoard(nodeIndex))
                return false;

            // Oyuncu 0 ileri gidiyor -> evi 18..23, Oyuncu 1 geri gidiyor -> evi 0..5.
            return playerId == 0
                ? nodeIndex >= NodeCount - HomeBoardSize
                : nodeIndex < HomeBoardSize;
        }

        /// <summary>Bu puldan tahtayi terk etmek icin gereken mesafe (1..6).</summary>
        public static int GetBearOffDistance(int nodeIndex, int playerId)
        {
            return playerId == 0 ? NodeCount - nodeIndex : nodeIndex + 1;
        }

        /// <summary>Oyuncunun simdiye kadar topladigi pul sayisi.</summary>
        public int GetBorneOffCount(int playerId) => _borneOff[playerId];

        /// <summary>
        /// View'in "bu pul toplanabilir mi?" diye sormasi icin. Tahtaya dokunmaz.
        /// </summary>
        public bool CanBearOff(int fromIndex)
        {
            return ResolveBearOff(fromIndex, CurrentPlayerId, false).Success;
        }

        /// <summary>
        /// Toplama denemesi. TryMove(fromIndex, BearOffIndex) ile ayni seyi yapar;
        /// cagri tarafinda niyet net okunsun diye ayri isim verildi.
        /// </summary>
        public bool TryBearOff(int fromIndex)
        {
            return TryMove(fromIndex, BearOffIndex);
        }

        /// <summary>
        /// OZGUR TOPLAMA — klasik tavlanin "tum pullar evde olmali" kurali YOKTUR.
        /// Tek sart: pul kendi ev bolgesinde olsun ve elde yeterince buyuk bir zar bulunsun.
        /// Diger 14 pul nerede olursa olsun umursanmaz.
        /// </summary>
        private MoveResult ResolveBearOff(int fromIndex, int playerId, bool commit)
        {
            if (!IsOnBoard(fromIndex))
                return MoveResult.Fail(MoveError.OutOfBounds, fromIndex, BearOffIndex, playerId);

            // Kirik pul kurali korunuyor: bardaki pulu oyuna sokmadan toplama yapilamaz.
            if (GetBarCount(playerId) > 0)
                return MoveResult.Fail(MoveError.MustEnterFromBar, fromIndex, BearOffIndex, playerId);

            BaseChecker mover = PeekSource(fromIndex, playerId);
            if (mover == null)
                return MoveResult.Fail(MoveError.SourceEmpty, fromIndex, BearOffIndex, playerId);

            if (mover.OwnerId != playerId)
                return MoveResult.Fail(MoveError.NotYourChecker, fromIndex, BearOffIndex, playerId);

            // Bataklik kilidi ve Zindan parmakliklari toplamayi da engeller.
            Node source = _nodes[fromIndex];
            if (source.ActiveTile != null && !source.ActiveTile.CanLeaveFrom(mover))
                return MoveResult.Fail(MoveError.CheckerLocked, fromIndex, BearOffIndex, playerId);

            if (!IsInHomeBoard(fromIndex, playerId))
                return MoveResult.Fail(MoveError.NotInHomeBoard, fromIndex, BearOffIndex, playerId);

            // "Zar >= kalan mesafe" — klasik tavladaki tam-eslesme sarti yok.
            int remaining = GetBearOffDistance(fromIndex, playerId);
            int usedDie;
            if (!TryConsumeForBearOff(remaining, commit, out usedDie))
                return MoveResult.Fail(MoveError.NoDieForBearOff, fromIndex, BearOffIndex, playerId);

            // Kuru sorgu burada durur — zar da harcanmadi (commit=false gecildi).
            if (!commit)
                return MoveResult.BearOff(fromIndex, BearOffIndex, playerId, mover, 0);

            RemoveFromNode(source, mover);

            _borneOff[playerId]++;
            int scored = AwardScore(playerId, BearOffScore);

            // Iki ayri event, iki ayri sorumluluk:
            //   OnCheckerBorneOff -> oyun anlami (skor patlamasi, sayac)
            //   OnCheckerDestroyed -> gorsel yasam dongusu (View prefab'i yok eder)
            // Ikincisi sayesinde View katmanina hic dokunmadan pul sahneden siliniyor.
            OnCheckerBorneOff?.Invoke(mover, fromIndex);
            OnCheckerDestroyed?.Invoke(mover, fromIndex);

            MoveResult result = MoveResult.BearOff(fromIndex, BearOffIndex, playerId, mover, scored);

            // 15 pulun hepsini toplayan maci erken bitirir.
            if (_borneOff[playerId] >= CheckersPerPlayer)
                FinishMatch();

            return result;
        }

        /// <summary>
        /// Toplama icin zar harcar. Yeterli olan EN KUCUK zari secer ki buyuk zarlar israf olmasin.
        /// commit=false ise sadece bakar, harcamaz.
        /// </summary>
        private bool TryConsumeForBearOff(int remaining, bool commit, out int usedDie)
        {
            usedDie = 0;

            DiceRoll roll = Context != null ? Context.CurrentRoll : null;

            // Zar sistemi bagli degilse klasik 1-6 araligi yeterli sayilir (test senaryolari).
            if (roll == null)
            {
                usedDie = remaining;
                return remaining >= 1 && remaining <= MaxDieValue;
            }

            // Kartografin Hilesi aktifse havuzdan tam mesafe kadar dusulur.
            if (roll.FreeSplit)
            {
                if (!roll.CanAfford(remaining))
                    return false;

                usedDie = remaining;
                if (commit)
                    roll.Consume(remaining);
                return true;
            }

            int best = int.MaxValue;
            IReadOnlyList<int> moves = roll.RemainingMoves;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i] >= remaining && moves[i] < best)
                    best = moves[i];
            }

            if (best == int.MaxValue)
                return false;

            usedDie = best;
            if (commit)
                roll.Consume(best);

            return true;
        }

        #endregion

        #region Hamle cozumleme

        private MoveResult ResolveMove(int fromIndex, int toIndex, int playerId, bool commit)
        {
            // ---------- 0) Saatli bomba: mac kapandiysa hicbir hamle kabul edilmez ----------
            if (IsMatchOver)
                return MoveResult.Fail(MoveError.MatchOver, fromIndex, toIndex, playerId);

            // Toplama ayri bir yol: hedef bir hane degil, tahtanin disi.
            // Ayni ResolveMove kapisindan geciyor ki TryMove/CanMove ikisi de calissin.
            if (toIndex == BearOffIndex)
                return ResolveBearOff(fromIndex, playerId, commit);

            // ---------- 1) Temel dogrulama ----------
            if (!IsOnBoard(toIndex) || (!IsOnBoard(fromIndex) && fromIndex != BarIndex))
                return MoveResult.Fail(MoveError.OutOfBounds, fromIndex, toIndex, playerId);

            if (GetBarCount(playerId) > 0 && fromIndex != BarIndex)
                return MoveResult.Fail(MoveError.MustEnterFromBar, fromIndex, toIndex, playerId);

            BaseChecker mover = PeekSource(fromIndex, playerId);
            if (mover == null)
                return MoveResult.Fail(MoveError.SourceEmpty, fromIndex, toIndex, playerId);

            if (mover.OwnerId != playerId)
                return MoveResult.Fail(MoveError.NotYourChecker, fromIndex, toIndex, playerId);

            // ---------- 2) Cikis izni (Bataklik kilidi / Zindan parmakliklari) ----------
            if (IsOnBoard(fromIndex))
            {
                ITileEffect sourceTile = _nodes[fromIndex].ActiveTile;
                if (sourceTile != null && !sourceTile.CanLeaveFrom(mover))
                    return MoveResult.Fail(MoveError.CheckerLocked, fromIndex, toIndex, playerId);
            }

            // ---------- 3) Yon ve mesafe ----------
            int origin = fromIndex == BarIndex ? GetBarOrigin(playerId) : fromIndex;
            int distance = (toIndex - origin) * GetDirection(playerId);

            if (distance <= 0)
                return MoveResult.Fail(MoveError.WrongDirection, fromIndex, toIndex, playerId);

            // Gocebe Pul: komsulari bossa fazladan hane hakki. Zar degerinden DUSULUR,
            // yani oyuncu 3 zariyla 6 hane gidebilir ama ekstra hamle hakki kazanmaz.
            int requiredDie = distance;
            IExtraDistanceProvider extra = mover.GetAbility<IExtraDistanceProvider>();
            if (extra != null && !CanAffordDistance(distance))
            {
                int bonus = extra.GetBonusDistance(this, fromIndex);
                if (bonus > 0 && CanAffordDistance(distance - bonus))
                {
                    requiredDie = distance - bonus;
                    if (commit)
                        ReportAbility("ExtraDistance", mover, toIndex);
                }
            }

            if (requiredDie <= 0 || requiredDie > MaxDieValue)
                return MoveResult.Fail(MoveError.InvalidDistance, fromIndex, toIndex, playerId);

            if (!CanAffordDistance(requiredDie))
                return MoveResult.Fail(MoveError.NoDiceValue, fromIndex, toIndex, playerId);

            // ---------- 4) Hedef hane: karo izni ----------
            Node target = _nodes[toIndex];

            if (target.ActiveTile != null && !target.ActiveTile.CanLandOn(mover))
                return MoveResult.Fail(MoveError.BlockedByTile, fromIndex, toIndex, playerId);

            // ---------- 5) Hedef hane: rakip pullar ----------
            List<BaseChecker> captures = null;
            bool infiltration = false;

            if (target.CountEnemiesOf(playerId) > 0)
            {
                IAreaCapture area = mover.GetAbility<IAreaCapture>();
                IInfiltrator infiltrator = mover.GetAbility<IInfiltrator>();
                IBlockadeBypass bypass = mover.GetAbility<IBlockadeBypass>();

                bool bypassAllowed = bypass != null && bypass.CanBypassBlockade(target);

                if (area != null && area.CanCaptureEntirePoint(target))
                {
                    // Kiyamet Pulu: kapiyi komple supurur.
                    captures = target.GetEnemiesOf(playerId);
                    if (commit)
                        ReportAbility("AreaCapture", mover, toIndex);
                }
                else if (infiltrator != null && infiltrator.CanInfiltrate(target))
                {
                    // Parazit Pul: kirmadan rakiple ayni haneye yerlesir.
                    infiltration = true;
                    if (commit)
                        ReportAbility("Infiltrate", mover, toIndex);
                }
                else
                {
                    BaseChecker victim = target.GetTopEnemyOf(playerId);
                    bool singleEnemy = target.CountEnemiesOf(playerId) == 1;

                    if (singleEnemy && victim.CanBeCaptured && !target.IsBlockedFor(playerId))
                    {
                        captures = new List<BaseChecker> { victim };
                    }
                    else if (bypassAllowed)
                    {
                        // Hayalet Pul: blokaji yok sayip yanlarina konar, kirma yapmaz.
                        if (commit)
                            ReportAbility("BlockadeBypass", mover, toIndex);
                    }
                    else
                    {
                        // Bagisiklik saldiriyi savusturduysa yetenege haber ver (tek kullanimlik kalkanlar icin).
                        ICaptureImmunity immunity = victim.GetAbility<ICaptureImmunity>();
                        // Kalkan harcama SADECE gercek denemede olur; kuru sorguda dokunmayiz.
                        if (commit && SideEffectsEnabled && immunity != null && immunity.IsImmune)
                        {
                            immunity.OnImmunityTriggered();
                            ReportAbility("ImmunityBlocked", victim, toIndex);
                        }

                        return MoveResult.Fail(MoveError.DestinationBlocked, fromIndex, toIndex, playerId);
                    }
                }
            }

            // ---------- 6) Kapasite (Kara Delik Pulu bu siniri kaldirir) ----------
            int stackLimit = target.GetStackLimit(DefaultStackLimit);
            if (target.CountFor(playerId) + 1 > stackLimit)
                return MoveResult.Fail(MoveError.StackFull, fromIndex, toIndex, playerId);

            // ===================================================================
            // Buradan sonrasi: tum kurallar gecildi, artik oyun durumu degisiyor.
            // ===================================================================

            // Kuru sorgu (CanMove / GetLegalTargets) tam burada durur: tum dogrulamayi
            // AYNI kodla gecirir ama tahtaya dokunmaz. Kural iki yerde yazilmadigi icin
            // "vurgulanan hane gecerli ama hamle reddediliyor" tutarsizligi imkansiz.
            if (!commit)
                return MoveResult.Ok(fromIndex, toIndex, playerId, mover, captures, infiltration, 0);

            ConsumeDistance(requiredDie);

            if (IsOnBoard(fromIndex))
            {
                RemoveFromNode(_nodes[fromIndex], mover);
            }
            else
            {
                _bar[playerId].Remove(mover);
            }

            // Gecis yolundaki pullara/karolara haber ver (Vergi Pulu, Haydut Kampi).
            NotifyPassOver(origin, toIndex, playerId, mover);

            // Agresif puanlama: her kirilan rakip pulu icin puan yazilir.
            // ApplyCapture bunu donduruyor, biz sadece topluyoruz.
            int scoreGained = 0;
            if (captures != null)
            {
                for (int i = 0; i < captures.Count; i++)
                    scoreGained += ApplyCapture(captures[i], target, playerId);
            }

            target.Push(mover);

            MoveResult result = MoveResult.Ok(fromIndex, toIndex, playerId, mover, captures,
                                              infiltration, scoreGained);

            // Karo etkisi pul yerlestikten SONRA uygulanir (Kazik, Girdap, Sis, Bataklik...).
            if (SideEffectsEnabled && target.ActiveTile != null)
            {
                ITileEffect tile = target.ActiveTile;
                tile.OnCheckerLanded(Context, target, mover);
                OnTileTriggered?.Invoke(tile, toIndex, mover);

                if (tile.IsExpired)
                    target.ClearTile();
            }

            NotifyMoveReactors(target, mover, result);

            return result;
        }

        #endregion

        #region Kanca dagiticilari

        /// <summary>Hamleyi yapan pul + hedef hanede zaten duran pullar tepki verir.</summary>
        private void NotifyMoveReactors(Node target, BaseChecker mover, MoveResult result)
        {
            // Simulasyonda ve context bagli degilken yetenek dagitimi atlanir.
            if (!SideEffectsEnabled)
                return;

            IMoveReactor moverReactor = mover.GetAbility<IMoveReactor>();
            if (moverReactor != null)
                moverReactor.OnMoveCompleted(Context, result, true);

            // Kopya uzerinde geziyoruz: yetenekler hane icerigini degistirebilir.
            BaseChecker[] witnesses = new BaseChecker[target.StoneCount];
            for (int i = 0; i < target.StoneCount; i++)
                witnesses[i] = target.Checkers[i];

            for (int i = 0; i < witnesses.Length; i++)
            {
                if (ReferenceEquals(witnesses[i], mover))
                    continue;

                IMoveReactor reactor = witnesses[i].GetAbility<IMoveReactor>();
                if (reactor != null)
                    reactor.OnMoveCompleted(Context, result, false);
            }
        }

        /// <summary>Kaynak ile hedef ARASINDAKI haneleri tarar (hedef ve kaynak haric).</summary>
        private void NotifyPassOver(int origin, int toIndex, int playerId, BaseChecker mover)
        {
            if (!SideEffectsEnabled)
                return;

            int step = GetDirection(playerId);

            for (int index = origin + step; index != toIndex; index += step)
            {
                if (!IsOnBoard(index))
                    continue;

                Node node = _nodes[index];

                if (SideEffectsEnabled && node.ActiveTile != null)
                {
                    node.ActiveTile.OnCheckerPassedOver(Context, node, mover);
                    OnTileTriggered?.Invoke(node.ActiveTile, index, mover);
                }

                for (int i = node.StoneCount - 1; i >= 0; i--)
                {
                    BaseChecker resident = node.Checkers[i];
                    if (resident.OwnerId == playerId)
                        continue;

                    IPassOverReactor reactor = resident.GetAbility<IPassOverReactor>();
                    if (reactor != null)
                        reactor.OnEnemyPassedOver(Context, mover, index);
                }
            }
        }

        private void NotifyTileLeft(Node source, BaseChecker checker)
        {
            if (!SideEffectsEnabled || source.ActiveTile == null)
                return;

            source.ActiveTile.OnCheckerLeft(Context, source, checker);

            if (source.ActiveTile.IsExpired)
                source.ClearTile();
        }

        /// <summary>
        /// Bir pulu haneden cikarmanin TEK yolu. Node.Remove'u dogrudan cagirmak YASAK:
        /// karolar pul bazinda durum tutuyor (Sis yetenek kilidi, Bataklik saplanmasi,
        /// Barut Ficisi fitili) ve o durumu sadece OnCheckerLeft temizliyor. Kancayi atlayan
        /// her cikis yolu pulu omur boyu sisli/saplanmis birakir ve karonun sozlugunde
        /// olu referans biriktirir.
        /// </summary>
        /// <returns>Pul gercekten bu hanedeydi ve cikarildiysa true.</returns>
        private bool RemoveFromNode(Node node, BaseChecker checker)
        {
            if (!node.Remove(checker))
                return false;

            NotifyTileLeft(node, checker);
            return true;
        }

        #endregion

        #region Kirma

        /// <summary>
        /// Kirma isleminin TEK uygulama noktasi. Karolar (Kazik, Girdap) ve yetenekler
        /// (Bar Pulu) de buradan gecer, boylece Anka/Fedakar kurallari her yolda calisir.
        /// </summary>
        public void ForceCapture(BaseChecker victim, int nodeIndex, int byPlayerId)
        {
            if (!IsOnBoard(nodeIndex))
                return;

            ApplyCapture(victim, _nodes[nodeIndex], byPlayerId);
        }

        /// <returns>Bu kirmanin kazandirdigi puan (carpanlar uygulanmis hali).</returns>
        private int ApplyCapture(BaseChecker victim, Node node, int byPlayerId)
        {
            // RemoveFromNode KULLANILMIYOR: o yardimci kancayi hemen tetikler, burada ise
            // kanca ile kaldirma arasina "hedef neresi?" sorusu giriyor (asagidaki nota bak).
            if (!node.Remove(victim))
                return 0;

            // Hedefi yetenek belirler: Anka -> baslangic hanesi, Fedakar -> yok olma.
            // Sis icindeyken GetAbility null donecegi icin pul otomatik olarak bara gider.
            //
            // SIRA ONEMLI: Hem hedef hem de tepki, pul HALA karonun etkisi altindayken
            // sorulur. Sis kurali "o hanede durdugu surece duz pul" diyor; kilidi once
            // acarsak sisin icinde kirilan Anka bara degil baslangic hanesine giderdi.
            ICustomCaptureDestination custom = victim.GetAbility<ICustomCaptureDestination>();
            CaptureDestination destination = custom != null ? custom.Destination : CaptureDestination.Bar;
            ICaptureReactor reactor = victim.GetAbility<ICaptureReactor>();

            // Karar alindi; pul artik haneyi gercekten terk ediyor. Kirma yolu bu kancayi
            // atlarsa Sis'in yetenek kilidi hic acilmaz ve pul bardan duz pul olarak doner.
            NotifyTileLeft(node, victim);

            switch (destination)
            {
                case CaptureDestination.HomeEntry:
                    _nodes[GetHomeEntryIndex(victim.OwnerId)].Push(victim);
                    break;

                case CaptureDestination.Destroyed:
                    OnCheckerDestroyed?.Invoke(victim, node.Index);
                    break;

                default:
                    _bar[victim.OwnerId].Add(victim);
                    break;
            }

            // Pul nesneleri klonla PAYLASILDIGI icin simulasyonda bu event tetiklenmez.
            if (SideEffectsEnabled)
                victim.OnCaptured(byPlayerId);

            // Tepki yukarida, pul daha karonun altindayken cozuldu.
            if (reactor != null && SideEffectsEnabled)
                reactor.OnCaptured(Context, byPlayerId, node.Index);

            // AGRESIF PUANLAMA — sadece RAKIP pulu kirmak puan kazandirir.
            // Bu kosul olmadan Bar Pulu'nun %25 kendini kirmasi kendine puan yazardi.
            // Kazik/Girdap gibi tarafsiz karolar rakip kimligiyle cagirdigi icin
            // onlarin oldurdugu pul da rakibe puan yazar — bilincli arcade karari.
            int scored = 0;
            if (byPlayerId != victim.OwnerId)
                scored = AwardScore(byPlayerId, CaptureScore);

            OnStoneCaptured?.Invoke(victim, node.Index, destination);
            return scored;
        }

        /// <summary>
        /// Puani MatchContext'e yazar ve gercekten yazilan (carpanli) degeri dondurur.
        /// Skor TEK yerde tutuluyor — GameState kendi kopyasini tutmuyor ki iki sayac
        /// birbirinden ayrismasin.
        /// </summary>
        private int AwardScore(int playerId, int rawAmount)
        {
            return Context != null ? Context.AddScore(playerId, rawAmount) : 0;
        }

        /// <summary>Girdap Hanesi gibi zorla tasima yapan etkiler icin.</summary>
        public void TeleportChecker(BaseChecker checker, int fromIndex, int toIndex)
        {
            if (!IsOnBoard(fromIndex) || !IsOnBoard(toIndex))
                return;

            // Karo "pul ayrildi" kancasini gormeli: sisten suruklenen pul yeteneklerini
            // geri kazanmali, bataklik saplanma kaydini silmeli.
            if (!RemoveFromNode(_nodes[fromIndex], checker))
                return;

            _nodes[toIndex].Push(checker);
            OnCheckerTeleported?.Invoke(checker, fromIndex, toIndex);

            if (SideEffectsEnabled && _nodes[toIndex].ActiveTile != null)
                _nodes[toIndex].ActiveTile.OnCheckerLanded(Context, _nodes[toIndex], checker);
        }

        /// <summary>Barut Ficisi gibi pulu tamamen yok eden etkiler icin.</summary>
        public void DestroyChecker(BaseChecker checker, int nodeIndex)
        {
            // Pul yok olsa bile kanca calismali: karolarin pul bazli kayitlari
            // (Bataklik kilidi, Barut Ficisi fitili) aksi halde olu referans tutar.
            if (!IsOnBoard(nodeIndex) || !RemoveFromNode(_nodes[nodeIndex], checker))
                return;

            OnCheckerDestroyed?.Invoke(checker, nodeIndex);
        }

        #endregion

        #region Zar koprusu

        // Zar sistemi henuz baglanmadiysa (Context null) klasik 1-6 dogrulamasina duseriz.
        // Boylece mimari, zar sistemi olmadan da test edilebilir kalir.

        private bool CanAffordDistance(int distance)
        {
            if (distance <= 0)
                return false;

            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll == null)
                return distance <= MaxDieValue;

            return roll.CanAfford(distance);
        }

        private void ConsumeDistance(int distance)
        {
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll != null)
                roll.Consume(distance);
        }

        #endregion

        #region Tur yonetimi

        /// <summary>Zaman Buken Pul bunu tetikler.</summary>
        public void RequestSkipTurn(int playerId)
        {
            if (playerId >= 0 && playerId < PlayerCount)
                _skipNextTurn[playerId] = true;
        }

        /// <summary>
        /// Turu bitirir: karo sayaclarini isletir, tur sonu yeteneklerini calistirir ve sirayi devreder.
        /// </summary>
        public void EndTurn()
        {
            // Mac kapandiysa tur da islemez.
            if (IsMatchOver)
                return;

            for (int i = 0; i < NodeCount; i++)
            {
                Node node = _nodes[i];

                if (SideEffectsEnabled && node.ActiveTile != null)
                {
                    node.ActiveTile.OnTurnPassed(Context, node);

                    if (node.ActiveTile.IsExpired)
                        node.ClearTile();
                }

                if (!SideEffectsEnabled)
                    continue;

                // Sondan basa gez: Acgozlu Pul gibi yetenekler hane icerigini degistirebilir.
                for (int c = node.StoneCount - 1; c >= 0; c--)
                {
                    if (c >= node.StoneCount)
                        continue;

                    ITurnEndReactor reactor = node.Checkers[c].GetAbility<ITurnEndReactor>();
                    if (reactor != null)
                        reactor.OnTurnEnded(Context, i);
                }
            }

            // SAATLI BOMBA: her tur sonunda sayac duser.
            RemainingTurns--;
            OnTurnCountChanged?.Invoke(RemainingTurns);

            if (RemainingTurns <= 0)
            {
                FinishMatch();
                return; // Sira devredilmez; mac bitti.
            }

            AdvanceTurn();
        }

        /// <summary>
        /// Maci kapatir ve kazanani ilan eder. Iki yoldan cagrilir:
        /// tur sayacinin bitmesi, ya da bir oyuncunun 15 pulunu da toplamasi.
        /// </summary>
        private void FinishMatch()
        {
            if (IsMatchOver)
                return;

            IsMatchOver = true;
            WinnerId = DetermineWinner();
            OnMatchEnded?.Invoke(WinnerId);
        }

        /// <summary>
        /// Kazanan = puani yuksek olan. Skorun tek kaynagi MatchContext oldugu icin
        /// karar burada degil, oradaki degerler okunarak veriliyor.
        /// </summary>
        private int DetermineWinner()
        {
            if (Context == null)
                return NoWinner;

            int bestPlayer = NoWinner;
            int bestScore = int.MinValue;
            bool tied = false;

            for (int p = 0; p < PlayerCount; p++)
            {
                int score = Context.GetScore(p);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPlayer = p;
                    tied = false;
                }
                else if (score == bestScore)
                {
                    tied = true;
                }
            }

            return tied ? NoWinner : bestPlayer;
        }

        private void AdvanceTurn()
        {
            int next = (CurrentPlayerId + 1) % PlayerCount;

            if (_skipNextTurn[next])
            {
                _skipNextTurn[next] = false;
                next = (next + 1) % PlayerCount; // Sira atlandi, tekrar ayni oyuncuya doner.
            }

            CurrentPlayerId = next;
            OnTurnChanged?.Invoke(CurrentPlayerId);
        }

        #endregion

        private BaseChecker PeekSource(int fromIndex, int playerId)
        {
            if (fromIndex == BarIndex)
                return _bar[playerId].Count > 0 ? _bar[playerId][_bar[playerId].Count - 1] : null;

            // Hanede rakip pulu da olabilir (Parazit); bu yuzden KENDI en ust pulumuzu ariyoruz.
            Node node = _nodes[fromIndex];
            for (int i = node.StoneCount - 1; i >= 0; i--)
            {
                if (node.Checkers[i].OwnerId == playerId)
                    return node.Checkers[i];
            }
            return null;
        }
    }
}
