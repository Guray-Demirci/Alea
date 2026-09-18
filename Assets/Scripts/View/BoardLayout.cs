using UnityEngine;

namespace Tavla.View
{
    /// <summary>
    /// Tahtanin GORSEL geometrisi. Degerler dogrudan play_game_scheme.aseprite dosyasindan
    /// piksel piksel olculdu. Burada oyun kurali yoktur — sadece "hane 7 ekranda nerede?"
    /// sorusunun cevabi vardir.
    ///
    /// Neden ScriptableObject? Cunku sanatci ucgen araligini degistirdiginde kod derlenmesin,
    /// sadece asset guncellensin. BoardManager'a atanmazsa varsayilan degerlerle calisir.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardLayout", menuName = "Tavla/Board Layout")]
    public class BoardLayout : ScriptableObject
    {
        [Header("Tuval (aseprite şeması)")]
        [SerializeField] private int _canvasWidth = 320;
        [SerializeField] private int _canvasHeight = 180;
        [SerializeField] private float _pixelsPerUnit = 12f;

        [Header("Üçgen yerleşimi (piksel)")]
        [Tooltip("İlk üçgenin sol kenarı")]
        [SerializeField] private int _firstTriangleX = 90;
        [Tooltip("İki üçgen arası mesafe (12 genişlik + 2 boşluk)")]
        [SerializeField] private int _trianglePitch = 14;
        [SerializeField] private int _triangleWidth = 12;
        [Tooltip("6. üçgenden itibaren eklenen bar kayması")]
        [SerializeField] private int _barOffset = 14;
        [SerializeField] private int _triangleHeight = 56;

        [Tooltip("Üst sıra üçgenlerinin üst kenarı")]
        [SerializeField] private int _topRowTopY = 18;
        [Tooltip("Alt sıra üçgenlerinin alt kenarı (161 dahil olduğu için 162)")]
        [SerializeField] private int _bottomRowBottomY = 162;

        [Header("Pul dizilimi")]
        [SerializeField] private int _checkerSize = 12;
        [Tooltip("Üst üste dizilen pullar arası mesafe. 11 px -> 5 pul 55 px, 56 px üçgene sığar.")]
        [SerializeField] private int _stackStep = 11;

        [Tooltip("Pulların z ekseninde hanelerin ÖNÜNDE durması için. Aksi halde fare tıklaması " +
                 "hane collider'ına takılır ve sürükleme başlamaz.")]
        [SerializeField] private float _checkerZ = -1f;

        [Header("Bar (kırık pullar)")]
        [SerializeField] private int _barCenterX = 180;
        [SerializeField] private int _barCenterY = 90;
        [SerializeField] private int _barStackStep = 4;
        [Tooltip("Bar boşluğunun oyuncu başına düşen yüksekliği (32 px'lik boşluğun yarısı).")]
        [SerializeField] private int _barHalfHeight = 16;

        [Header("Toplama alanı (bear-off)")]
        [Tooltip("Tahtanın sağındaki boş şeridin yatay merkezi. Çerçeve 279 px'te bitiyor, " +
                 "sol taraf ise bilgi paneline ait.")]
        [SerializeField] private int _bearOffCenterX = 298;
        [SerializeField] private int _bearOffWidth = 28;

        public float PixelsPerUnit => _pixelsPerUnit;

        /// <summary>Ortografik kamera boyutu — tam piksel orani icin.</summary>
        public float CameraOrthographicSize => (_canvasHeight * 0.5f) / _pixelsPerUnit;

        /// <summary>Pulun dunya birimi cinsinden boyutu (collider icin).</summary>
        public float CheckerWorldSize => _checkerSize / _pixelsPerUnit;

        /// <summary>
        /// Aseprite piksel koordinatini Unity dunya koordinatina cevirir.
        /// Aseprite'ta y ASAGI artar, Unity'de YUKARI — cevrim burada tek yerde yapilir.
        /// Tahtanin merkezi dunya orijinindedir (0,0).
        /// </summary>
        public Vector3 PixelToWorld(float px, float py)
        {
            float x = (px - _canvasWidth * 0.5f) / _pixelsPerUnit;
            float y = (_canvasHeight * 0.5f - py) / _pixelsPerUnit;
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// HANE INDEKSI -> EKRAN ESLEMESI (mimarinin sozlesmesi):
        ///   0..11  -> ust sira, SOLDAN SAGA
        ///   12..23 -> alt sira, SAGDAN SOLA
        /// Boylece Oyuncu 0 (0 -> 23) sol ustten baslayip U cizerek sol alta iner.
        /// </summary>
        public bool IsTopRow(int nodeIndex) => nodeIndex < 12;

        /// <summary>Hane indeksinden ekrandaki sutun numarasini (0-11, soldan saga) verir.</summary>
        public int GetColumn(int nodeIndex) => nodeIndex < 12 ? nodeIndex : 23 - nodeIndex;

        /// <summary>Sutunun piksel cinsinden yatay merkezi. Bar boslugu formule gomulu.</summary>
        private float GetColumnCenterX(int column)
        {
            int left = _firstTriangleX + column * _trianglePitch + (column >= 6 ? _barOffset : 0);
            return left + _triangleWidth * 0.5f;
        }

        /// <summary>Ucgenin merkezi — NodeView'in konumu ve collider'i icin.</summary>
        public Vector3 GetNodeCenter(int nodeIndex)
        {
            float px = GetColumnCenterX(GetColumn(nodeIndex));

            float py = IsTopRow(nodeIndex)
                ? _topRowTopY + _triangleHeight * 0.5f
                : _bottomRowBottomY - _triangleHeight * 0.5f;

            return PixelToWorld(px, py);
        }

        /// <summary>NodeView collider'inin dunya birimi cinsinden boyutu.</summary>
        public Vector2 GetNodeSize()
        {
            return new Vector2(_triangleWidth / _pixelsPerUnit, _triangleHeight / _pixelsPerUnit);
        }

        /// <summary>
        /// Toplama birakma alaninin merkezi. Tahtanin DISINDA durur: ucgenlerin ustune
        /// konsaydi collider'i pul tiklamalarini calardi. Dikeyde iki ucgen sirasini kapsar —
        /// tek alan iki oyuncuya da yeter, kimin topladigini Core sirasi gelen oyuncudan biliyor.
        /// </summary>
        public Vector3 GetBearOffCenter()
        {
            return PixelToWorld(_bearOffCenterX, (_topRowTopY + _bottomRowBottomY) * 0.5f);
        }

        /// <summary>Toplama alani collider'inin dunya birimi cinsinden boyutu.</summary>
        public Vector2 GetBearOffSize()
        {
            return new Vector2(_bearOffWidth / _pixelsPerUnit,
                               (_bottomRowBottomY - _topRowTopY) / _pixelsPerUnit);
        }

        /// <summary>
        /// Yigin araligini mevcut alana sigdirir.
        ///
        /// Core artik bir haneye 15 pulun tamaminin yigilmasina izin veriyor. Sabit 11 px
        /// aralikla 15 pul 154 px eder — 56 px'lik ucgeni fena halde tasar. Bu hesap,
        /// yigin buyudukce pullari ust uste bindirerek her zaman alanin icinde tutar.
        /// Az sayida pulda tercih edilen aralik aynen korunur.
        /// </summary>
        private static float FitStep(float preferredStep, float usableHeight, int stackCount)
        {
            if (stackCount <= 1 || usableHeight <= 0f)
                return preferredStep;

            // stackCount-1 aralik var (ilk pulun kendisi yer kaplamiyor sayilir).
            float fitted = usableHeight / (stackCount - 1);
            return fitted < preferredStep ? fitted : preferredStep;
        }

        /// <summary>
        /// Bir hanedeki k'inci pulun konumu.
        /// Ust sirada pullar asagi dogru, alt sirada yukari dogru dizilir — ucgenin tabanindan iceri.
        /// </summary>
        /// <param name="stackCount">Hanedeki toplam pul sayisi; aralik buna gore daralir.</param>
        public Vector3 GetCheckerPosition(int nodeIndex, int stackIndex, int stackCount)
        {
            float px = GetColumnCenterX(GetColumn(nodeIndex));

            // Son pulun MERKEZI ucgenin icinde kalsin diye pul boyu kadari dusuluyor.
            float step = FitStep(_stackStep, _triangleHeight - _checkerSize, stackCount);

            float py = IsTopRow(nodeIndex)
                ? _topRowTopY + stackIndex * step + _checkerSize * 0.5f
                : _bottomRowBottomY - stackIndex * step - _checkerSize * 0.5f;

            Vector3 world = PixelToWorld(px, py);
            world.z = _checkerZ;
            return world;
        }

        /// <summary>Bardaki k'inci pulun konumu. Oyuncu 0 merkezden yukari, oyuncu 1 asagi dizilir.</summary>
        /// <param name="stackCount">Bardaki toplam pul sayisi; aralik buna gore daralir.</param>
        public Vector3 GetBarPosition(int playerId, int stackIndex, int stackCount)
        {
            float direction = playerId == 0 ? -1f : 1f;
            float step = FitStep(_barStackStep, _barHalfHeight - _checkerSize, stackCount);

            float py = _barCenterY + direction * (_checkerSize * 0.5f + stackIndex * step);

            Vector3 world = PixelToWorld(_barCenterX, py);
            world.z = _checkerZ;
            return world;
        }
    }
}
