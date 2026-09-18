using System.Collections.Generic;
using UnityEngine;

namespace Tavla.View
{
    /// <summary>Metnin capa noktasina gore hizalanmasi.</summary>
    public enum PixelTextAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// PixelFont ile tek satirlik metin basan etiket.
    ///
    /// APTAL KUKLA — NodeView gibi: ne yazdigini BILMEZ, sadece yazar. Skorun ne oldugu,
    /// altinin nereden geldigi HudPresenter'in isidir.
    ///
    /// Her karakter bir SpriteRenderer'dir ve bu nesneler HAVUZLANIR: metin degistiginde
    /// yenileri uretilmez, var olanlarin sprite'i degisir, artanlar kapatilir. HUD her
    /// hamlede guncellendigi icin bu onemli — aksi halde her zar atisi cop uretirdi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelLabel : MonoBehaviour
    {
        [Header("İçerik")]
        [SerializeField] private string _text = string.Empty;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private PixelTextAlign _align = PixelTextAlign.Center;

        [Header("Ölçek ve sıralama")]
        [Tooltip("Tuvalin piksel/birim oranı. Tahtayla aynı olmalı (BoardLayout).")]
        [SerializeField] private float _pixelsPerUnit = 12f;
        [Tooltip("SortingGroup içindeyse grup içi sıra, değilse global sıra.")]
        [SerializeField] private int _sortingOrder = 100;

        private readonly List<SpriteRenderer> _glyphs = new List<SpriteRenderer>();

        private int _sortingLayerId;

        /// <summary>O an yazili olan metin.</summary>
        public string Text => _text;

        /// <summary>Metnin piksel cinsinden genisligi — slot'a sigip sigmadigini olcmek icin.</summary>
        public int MeasuredWidth => PixelFont.Measure(_text);

        private void Awake()
        {
            Rebuild();
        }

        /// <summary>
        /// Gorsel ayarlari tek seferde kurar. HudView etiketi urettikten hemen sonra cagirir.
        /// </summary>
        public void Configure(float pixelsPerUnit, int sortingLayerId, int sortingOrder,
                              Color color, PixelTextAlign align)
        {
            _pixelsPerUnit = pixelsPerUnit > 0f ? pixelsPerUnit : 1f;
            _sortingLayerId = sortingLayerId;
            _sortingOrder = sortingOrder;
            _color = color;
            _align = align;

            Rebuild();
        }

        /// <summary>
        /// Yeni metin. Metin aynıysa TEK BIR renderer'a bile dokunulmaz — HUD saniyede
        /// birkac kez tazelense de bu cagri bedava kalir.
        /// </summary>
        public void SetText(string text)
        {
            if (text == null)
                text = string.Empty;

            if (_text == text)
                return;

            _text = text;
            Rebuild();
        }

        public void SetColor(Color color)
        {
            _color = color;

            for (int i = 0; i < _glyphs.Count; i++)
                _glyphs[i].color = color;
        }

        /// <summary>Etiketi gorunur/gizli yapar. Nesneler korunur, sadece cizim durur.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// Metni gliflere dizer.
        ///
        /// Konumlar dogrudan piksel izgarasinin katlaridir (n / PPU): yarim piksele oturan
        /// bir yazi tahtanin geri kalaniyla ayni netlikte gorunmez.
        /// </summary>
        private void Rebuild()
        {
            float unit = 1f / (_pixelsPerUnit > 0f ? _pixelsPerUnit : 1f);

            // Glif pivotu sol-ALT oldugu icin dikeyde ortalamak yariyi asagi kaydirmaktir.
            float baseY = -(PixelFont.GlyphHeight * 0.5f) * unit;
            float startX = GetStartPixel() * unit;

            int used = 0;

            for (int i = 0; i < _text.Length; i++)
            {
                Sprite glyph = PixelFont.GetGlyph(_text[i], _pixelsPerUnit);
                if (glyph == null)
                    continue; // Bosluk: yer kaplar ama cizilmez.

                SpriteRenderer renderer = GetRenderer(used);
                used++;

                renderer.sprite = glyph;
                renderer.color = _color;
                renderer.sortingOrder = _sortingOrder;

                if (_sortingLayerId != 0)
                    renderer.sortingLayerID = _sortingLayerId;

                renderer.enabled = true;

                // Adim, karakterin SIRASINA gore hesaplanir (bosluklar dahil) — sabit genislik.
                renderer.transform.localPosition =
                    new Vector3(startX + i * PixelFont.Advance * unit, baseY, 0f);
            }

            // Kisalan metnin artiklari: yok edilmez, kapatilir. Sonraki uzun metin yeniden kullanir.
            for (int i = used; i < _glyphs.Count; i++)
                _glyphs[i].enabled = false;
        }

        /// <summary>Hizalamaya gore ilk glifin sol kenarinin piksel ofseti.</summary>
        private float GetStartPixel()
        {
            int width = PixelFont.Measure(_text);

            if (_align == PixelTextAlign.Center)
                return -width * 0.5f;

            if (_align == PixelTextAlign.Right)
                return -width;

            return 0f;
        }

        private SpriteRenderer GetRenderer(int index)
        {
            while (_glyphs.Count <= index)
            {
                GameObject go = new GameObject("Glyph_" + _glyphs.Count);
                go.transform.SetParent(transform, false);

                SpriteRenderer created = go.AddComponent<SpriteRenderer>();
                created.enabled = false;
                _glyphs.Add(created);
            }

            return _glyphs[index];
        }
    }
}
