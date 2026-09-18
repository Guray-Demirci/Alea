using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tavla.View
{
    /// <summary>
    /// Panelde sayi/metin basilabilen slotlar. Adlar Aseprite katmanlariyla eslesir;
    /// hangi katmana yazildigi Inspector'dan degistirilebilir.
    /// </summary>
    public enum HudField
    {
        /// <summary>Oyuncunun toplam skoru.</summary>
        Score,

        /// <summary>Oyuncunun altini.</summary>
        Gold,

        /// <summary>O eldeki kalan hamle haklari (zarlar).</summary>
        Moves,

        /// <summary>Gorev/durum satiri. Varsayilan olarak kalan tur sayisini gosterir.</summary>
        Task
    }

    /// <summary>
    /// Sol taraftaki bilgi paneline (general_info_scheme.aseprite) calisma anindan erisim.
    ///
    /// Katmanlari BU SCRIPT URETMEZ. Aseprite importer, "Animated Sprite + Individual Layers"
    /// modunda her katman icin bir cocuk GameObject + SpriteRenderer iceren bir prefab uretiyor;
    /// bu bilesen o prefab'in koku uzerinde durur ve cocuklari isimleriyle bulur.
    ///
    /// Pivot Space = Canvas oldugu icin butun katmanlar localPosition (0,0)'dadir ve
    /// Aseprite'taki dizilim korunur. Tuval 320x180 — tahta semasiyla ayni — dolayisiyla
    /// kok (0,0,0)'a konuldugunda panel tahtaya gore de hizalidir.
    ///
    /// SAYILAR: panel sanatinda "Score", "Gold", "Hamle", "Task" kutulari var ama icleri bos.
    /// Bu bilesen her kutunun uzerine bir PixelLabel oturtur ve disariya "su alana su metni
    /// yaz" demekten ibaret bir API acar. Degerlerin NE oldugunu bilmez — onu HudPresenter bilir.
    ///
    /// Slot konumlari ELLE GIRILMEZ: her etiket, ayni isimli Aseprite katmaninin gorsel
    /// kutusunun merkezine oturur. Sanatci kutuyu panelde kaydirirsa yazi kendiliginde
    /// onunla gider; kod degismez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>Bir slotun nereye ve nasil yazacagi. Tamamen gorsel ayar — veri icermez.</summary>
        [System.Serializable]
        private sealed class HudSlot
        {
            public HudField Field;

            [Tooltip("Aseprite katman adı. Değer bu katmanın görsel kutusunun merkezine yazılır.")]
            public string LayerName;

            [Tooltip("Kutu merkezine göre piksel kaydırma. +x sağa, +y AŞAĞI (Aseprite yönü).")]
            public Vector2 PixelOffset;

            public PixelTextAlign Align = PixelTextAlign.Center;

            public Color Color = Color.white;

            [Tooltip("Katman bulunamazsa kullanılacak tuval pikseli (320x180 şemasında).")]
            public Vector2 FallbackPixel;
        }

        [Header("Sıralama")]
        [Tooltip("Kokteki SortingGroup'un katman sirasi. Pullar 10+ katmaninda duruyor; " +
                 "panelin onlerinde kalmasi icin yuksek bir deger. SortingGroup butun " +
                 "cocuklari TEK parca olarak siraladigi icin bu deger sart.")]
        [SerializeField] private int _sortingOrder = 200;

        [Tooltip("Yazıların panel katmanlarının ÜSTÜNDE kalması için sıra değeri. " +
                 "Importer katmanlara 0'dan başlayan küçük sıralar verdiği için 100 rahat bir tavan.")]
        [SerializeField] private int _textSortingOrder = 100;

        [Header("Yazı")]
        [Tooltip("Piksel/birim oranı ve piksel->dünya çevrimi buradan okunur. " +
                 "Boş bırakılırsa varsayılan geometriyle (320x180 @ 12 PPU) çalışır.")]
        [SerializeField] private BoardLayout _layout;

        [Tooltip("Hangi değerin hangi katmana yazılacağı. Katman adları Aseprite'taki adlardır.")]
        [SerializeField]
        private HudSlot[] _slots =
        {
            new HudSlot { Field = HudField.Score, LayerName = "Score", FallbackPixel = new Vector2(44f, 96f) },
            new HudSlot { Field = HudField.Gold,  LayerName = "Gold",  FallbackPixel = new Vector2(44f, 120f) },
            new HudSlot { Field = HudField.Moves, LayerName = "Hamle", FallbackPixel = new Vector2(44f, 144f) },
            new HudSlot { Field = HudField.Task,  LayerName = "Task",  FallbackPixel = new Vector2(44f, 64f) }
        };

        private readonly Dictionary<string, SpriteRenderer> _layers = new();
        private readonly Dictionary<HudField, PixelLabel> _labels = new();

        private bool _built;

        private void Awake()
        {
            EnsureBuilt();
        }

        #region Kurulum

        /// <summary>
        /// Paneli yerine oturtur, katmanlari toplar ve etiketleri kurar.
        ///
        /// Awake yerine "ihtiyac aninda" cagrilabilir olmasi bilincli: panel calisma aninda
        /// Instantiate edilip ayni karede SetField cagrilirsa Awake sirasina guvenmek kirilgan
        /// olur. Konum sifirlamasi da burada — etiket capalari panelin NIHAI konumundan
        /// hesaplandigi icin sirasi kaymamali.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;

            // BoardManager ile ayni gerekce: "Create Empty"/prefab surukleme objeyi Scene
            // view'in baktigi noktaya koydugu icin konumu zorla sifirliyoruz — aksi halde
            // panel tahtadan kayar.
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // SortingGroup importer tarafindan prefab KOKUNE eklenir. Bu bilesen ister o kokte
            // olsun, ister prefab'i cocuk olarak tutan bir objede — ikisi de calissin.
            SortingGroup group = GetComponentInChildren<SortingGroup>(true);
            if (group != null)
                group.sortingOrder = _sortingOrder;

            CollectLayers();
            BuildLabels(group != null ? group.transform : transform, group != null);
        }

        private void CollectLayers()
        {
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                // Etiket glifleri de SpriteRenderer'dir; kendi urettiklerimizi katman sanmayalim.
                if (renderer.GetComponentInParent<PixelLabel>() != null)
                    continue;

                _layers[renderer.gameObject.name] = renderer;
            }

            if (_layers.Count == 0)
                Debug.LogError("[View] HudView altinda hic SpriteRenderer yok — panel bos. " +
                               "Bu bilesen BOS bir GameObject'e degil, Project'ten sahneye " +
                               "suruklenen general_info_scheme prefab'ina eklenmeli.", this);
        }

        /// <summary>
        /// Her slot icin bir PixelLabel uretir.
        /// </summary>
        /// <param name="parent">
        /// Etiketler SortingGroup'un ICINE konur: grup cocuklarini tek parca olarak siraladigi
        /// icin disarida kalan bir yazi, panelin arkasina duser ya da tahtayi delip one cikar.
        /// </param>
        /// <param name="insideGroup">
        /// Grup icindeysek sira GRUBA GOREDIR (0'dan baslar). Grup yoksa panel katmanlari
        /// global sirada durur ve yazinin onlari gecmesi icin panel sirasi eklenmelidir.
        /// </param>
        private void BuildLabels(Transform parent, bool insideGroup)
        {
            if (_layout == null)
            {
                // Asset atanmadiysa varsayilan degerlerle bellekte bir tane uret (BoardManager ile ayni yol).
                _layout = ScriptableObject.CreateInstance<BoardLayout>();
            }

            int sortingOrder = insideGroup ? _textSortingOrder : _sortingOrder + _textSortingOrder;
            int sortingLayerId = GetPanelSortingLayerId();

            for (int i = 0; i < _slots.Length; i++)
            {
                HudSlot slot = _slots[i];

                if (_labels.ContainsKey(slot.Field))
                {
                    Debug.LogWarning($"[View] HUD slotu iki kez tanimlanmis: {slot.Field}. " +
                                     "Ikincisi yok sayildi.", this);
                    continue;
                }

                GameObject go = new GameObject($"Label_{slot.Field}");
                go.transform.SetParent(parent, false);
                go.transform.position = GetSlotAnchor(slot);

                PixelLabel label = go.AddComponent<PixelLabel>();
                label.Configure(_layout.PixelsPerUnit, sortingLayerId, sortingOrder,
                                slot.Color, slot.Align);

                _labels.Add(slot.Field, label);
            }
        }

        /// <summary>
        /// Yazilar panelin kendi siralama katmanina katilir. Panel ozel bir katmandaysa
        /// (orn. "UI") yazinin "Default"ta kalmasi onu goze gorunmez yapardi.
        /// </summary>
        private int GetPanelSortingLayerId()
        {
            foreach (KeyValuePair<string, SpriteRenderer> pair in _layers)
                return pair.Value.sortingLayerID;

            return 0;
        }

        /// <summary>
        /// Slotun dunya konumu: once ayni isimli katmanin gorsel kutusunun merkezi,
        /// o yoksa semadaki yedek piksel.
        ///
        /// Katman kutusu transform'dan degil SPRITE'TAN okunur (sprite.bounds): Pivot Space
        /// Canvas modunda her katmanin transform'u (0,0)'dadir, kutu bilgisi tamamen
        /// sprite'in pivotunda saklidir.
        /// </summary>
        private Vector3 GetSlotAnchor(HudSlot slot)
        {
            float ppu = _layout.PixelsPerUnit;

            // Aseprite'ta y ASAGI artar; BoardLayout.PixelToWorld ile ayni cevrim.
            Vector3 offset = new Vector3(slot.PixelOffset.x / ppu, -slot.PixelOffset.y / ppu, 0f);

            SpriteRenderer layer = FindLayer(slot.LayerName);
            if (layer != null && layer.sprite != null)
                return layer.transform.TransformPoint(layer.sprite.bounds.center) + offset;

            Debug.LogWarning($"[View] HUD katmani bulunamadi: \"{slot.LayerName}\" " +
                             $"({slot.Field}). Yedek konum kullanildi. Paneldeki katmanlar: " +
                             string.Join(", ", _layers.Keys), this);

            return _layout.PixelToWorld(slot.FallbackPixel.x, slot.FallbackPixel.y) + offset;
        }

        /// <summary>
        /// Katmani adiyla bulur. Once birebir, sonra buyuk/kucuk harf ve Turkce karakter
        /// farklarini affeden bir arama — importer katman adlarini ("İnfo/Quit") dosya adina
        /// uygun hale getirirken degistirebiliyor.
        /// </summary>
        private SpriteRenderer FindLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return null;

            SpriteRenderer renderer;
            if (_layers.TryGetValue(layerName, out renderer))
                return renderer;

            foreach (KeyValuePair<string, SpriteRenderer> pair in _layers)
            {
                if (pair.Key.IndexOf(layerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return pair.Value;
            }

            return null;
        }

        #endregion

        #region Değer yazma (HudPresenter -> panel)

        /// <summary>Bir slota metin yazar. Slot tanimli degilse sessizce gecilir.</summary>
        public void SetField(HudField field, string text)
        {
            EnsureBuilt();

            PixelLabel label;
            if (_labels.TryGetValue(field, out label))
                label.SetText(text);
        }

        /// <summary>Sayi yazmanin kisa yolu — cagiranlarin her yerde ToString() yazmamasi icin.</summary>
        public void SetField(HudField field, int value)
        {
            SetField(field, value.ToString());
        }

        public void ClearField(HudField field)
        {
            SetField(field, string.Empty);
        }

        /// <summary>
        /// Slotun etiketi — rengini degistirmek (orn. altin azalinca kirmizi) ya da
        /// gizlemek icin. Yoksa null.
        /// </summary>
        public PixelLabel GetLabel(HudField field)
        {
            EnsureBuilt();

            PixelLabel label;
            return _labels.TryGetValue(field, out label) ? label : null;
        }

        #endregion

        #region Katmanlar

        /// <summary>
        /// Bir katmanin renderer'ini adiyla verir — avatari degistirmek, bir seridi gizlemek
        /// ya da uzerine collider koymak icin.
        /// Ad, Aseprite'taki katman adidir ("Avatari", "Gold", "İnfo/Quit"...).
        /// </summary>
        public SpriteRenderer GetLayer(string layerName)
        {
            EnsureBuilt();
            return FindLayer(layerName);
        }

        /// <summary>Avatari calisma aninda degistirir (karakter secimi icin).</summary>
        public void SetAvatar(Sprite avatar)
        {
            SpriteRenderer renderer = GetLayer("Avatari");
            if (renderer != null)
                renderer.sprite = avatar;
        }

        /// <summary>Bir katmani gosterir/gizler.</summary>
        public void SetLayerVisible(string layerName, bool visible)
        {
            SpriteRenderer renderer = GetLayer(layerName);
            if (renderer != null)
                renderer.enabled = visible;
        }

        #endregion
    }
}
