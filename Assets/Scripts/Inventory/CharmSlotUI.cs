using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Egy charm slot – használható grid slotként (6×4) és equip slotként (5 db) egyaránt.
/// Kezeli a drag & drop logikát.
/// </summary>
public class CharmSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum SlotType { Grid, Equip }

    [Header("Vizuális")]
    public Image       iconImage;          // a charm képe
    public Image       backgroundImage;    // slot háttér
    public Image       rarityBorder;       // opcionális: ritkaság szín keret
    public TextMeshProUGUI rarityLabel;    // opcionális: "R", "E", stb.
    [Tooltip("Ragyogás Image – az ikon mögött, ritkaság szerint pulzál")]
    public Image       glowImage;          // rarity glow
    [Tooltip("A ragyogás átmérője pixelben")]
    public float       glowSize = 90f;

    [Header("Ritkaság színek")]
    public Color commonColor    = new Color(0.7f, 0.7f, 0.7f);
    public Color rareColor      = new Color(0.2f, 0.5f, 1.0f);
    public Color epicColor      = new Color(0.7f, 0.2f, 1.0f);
    public Color legendaryColor = new Color(1.0f, 0.7f, 0.1f);
    public Color emptyColor     = new Color(1f, 1f, 1f, 0.15f);

    // ── Állapot ──────────────────────────────────────────────────────
    public SlotType slotType  { get; private set; }
    public int      slotIndex { get; private set; }   // grid: 0–23 | equip: 0–4
    public CharmInstance containedCharm { get; private set; }

    // ── Drag ─────────────────────────────────────────────────────────
    private static GameObject         _dragProxy;   // az egérrel mozgó kép
    private        Canvas             _rootCanvas;
    private static List<CharmSlotUI>  _allSlots = new List<CharmSlotUI>();

    /// <summary>Kuka zóna – ha ide ejtünk egy charmot, törlődik. CharacterInventoryUI állítja be.</summary>
    public static RectTransform TrashZone;

    // ── Callback ─────────────────────────────────────────────────────
    private System.Action<CharmSlotUI, CharmSlotUI> _onSwapRequested;
    private System.Action<CharmSlotUI>              _onTrashRequested;
    private Coroutine _glowCoroutine;

    // ════════════════════════════════════════════════════════════════

    void OnDestroy()
    {
        StopGlow();
        _allSlots.Remove(this);
    }

    public void Init(SlotType type, int index,
                     System.Action<CharmSlotUI, CharmSlotUI> onSwapRequested,
                     System.Action<CharmSlotUI> onTrashRequested = null)
    {
        slotType   = type;
        slotIndex  = index;
        _onSwapRequested  = onSwapRequested;
        _onTrashRequested = onTrashRequested;
        // includeInactive:true kell, mert a panel Start()-ban SetActive(false) állapotban van
        _rootCanvas = GetComponentInParent<Canvas>(true);
        if (!_allSlots.Contains(this)) _allSlots.Add(this);
        UIGlowHelper.ApplyTo(glowImage, glowSize);

        // Átlátszó hit-area Image a slot root-ján – ez fogadja a raycast-ot
        // függetlenül attól, hogy a gyerek képeken mi a raycastTarget beállítás
        var hitArea = GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
        }
        hitArea.raycastTarget = true;

        Refresh(null);
    }

    /// <summary>Frissíti a slot megjelenését a megadott charm alapján (null = üres).</summary>
    public void Refresh(CharmInstance charm)
    {
        containedCharm = charm;

        if (charm == null || string.IsNullOrEmpty(charm.definitionId))
        {
            SetEmpty();
            return;
        }

        var def = CharmRegistry.Instance?.Get(charm.definitionId);
        if (def == null) { SetEmpty(); return; }

        if (iconImage != null)
        {
            iconImage.sprite  = def.icon;
            iconImage.enabled = def.icon != null;
            iconImage.color   = Color.white;
        }

        Color rarColor = RarityColor(def.rarity);
        if (rarityBorder != null)
        {
            rarityBorder.enabled = true;
            rarityBorder.color   = rarColor;
        }
        if (rarityLabel != null)
        {
            rarityLabel.gameObject.SetActive(true);
            rarityLabel.text  = RarityInitial(def.rarity);
            rarityLabel.color = rarColor;
        }
        if (backgroundImage != null)
            backgroundImage.color = Color.white;

        // Ragyogás indítása a ritkaság színével
        StartGlow(rarColor);
    }

    void SetEmpty()
    {
        if (iconImage    != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (rarityBorder != null)   rarityBorder.enabled = false;
        if (rarityLabel  != null)   rarityLabel.gameObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.color = emptyColor;

        StopGlow();
    }

    // ── Egyesítés effekt ─────────────────────────────────────────────

    /// <summary>
    /// Látványos felfejlesztés effekt: a slot felugrik (pop) és egy
    /// táguló fehér fény-gyűrű villan ki belőle.
    /// </summary>
    public void PlayMergeEffect()
    {
        StartCoroutine(MergeEffectCoroutine());
    }

    System.Collections.IEnumerator MergeEffectCoroutine()
    {
        var rt = (RectTransform)transform;

        // Táguló fény-gyűrű létrehozása (külön, ideiglenes objektum)
        var flashGO = new GameObject("MergeFlash", typeof(RectTransform));
        var flashRt = (RectTransform)flashGO.transform;
        flashRt.SetParent(transform, false);
        flashRt.anchorMin        = new Vector2(0.5f, 0.5f);
        flashRt.anchorMax        = new Vector2(0.5f, 0.5f);
        flashRt.pivot            = new Vector2(0.5f, 0.5f);
        flashRt.anchoredPosition = Vector2.zero;
        flashRt.sizeDelta        = new Vector2(80f, 80f);
        flashRt.SetAsLastSibling();

        var flashImg = flashGO.AddComponent<Image>();
        flashImg.sprite        = UIGlowHelper.GetGlowSprite();
        flashImg.raycastTarget = false;

        const float dur = 0.45f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);

            // Slot pop: 1 → 1.4 → 1 (sinus ív)
            float scale = 1f + 0.4f * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * scale;

            // Fény-gyűrű: tágul és elhalványul
            float ring = Mathf.Lerp(0.5f, 2.3f, p);
            flashRt.localScale = Vector3.one * ring;
            flashImg.color = new Color(1f, 1f, 1f, 1f - p);

            yield return null;
        }

        rt.localScale = Vector3.one;
        Destroy(flashGO);
    }

    // ── Ragyogás ─────────────────────────────────────────────────────

    void StartGlow(Color baseColor)
    {
        if (glowImage == null) return;
        if (_glowCoroutine != null) StopCoroutine(_glowCoroutine);
        _glowCoroutine = StartCoroutine(GlowLoop(baseColor));
    }

    void StopGlow()
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }
        if (glowImage != null)
        {
            var c = glowImage.color;
            c.a = 0f;
            glowImage.color = c;
            glowImage.rectTransform.localScale = Vector3.one;
        }
    }

    System.Collections.IEnumerator GlowLoop(Color baseColor)
    {
        float t          = Random.Range(0f, Mathf.PI * 2f); // véletlenszerű fázis
        float speedMult  = Random.Range(0.9f, 1.1f);        // ±10% → 20% különbség

        while (true)
        {
            t += Time.unscaledDeltaTime * speedMult;

            float pulse = (Mathf.Sin(t * Mathf.PI * 0.7f) + 1f) * 0.5f;

            baseColor.a = Mathf.Lerp(0.25f, 0.75f, pulse);
            glowImage.color = baseColor;

            float gs = Mathf.Lerp(1.0f, 1.2f, pulse);
            glowImage.rectTransform.localScale = Vector3.one * gs;

            yield return null;
        }
    }

    // ── Drag & Drop ───────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (containedCharm == null) return;

        // Panel aktív ha ide jutunk – egyszerű lekérés, inaktív szülőkön is átmegy
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>(true);
        if (_rootCanvas == null) return;

        // Proxy kép létrehozása – a Canvas legfelső rétegén lebeg
        _dragProxy = new GameObject("DragProxy");
        _dragProxy.transform.SetParent(_rootCanvas.transform, false);
        _dragProxy.transform.SetAsLastSibling();

        var rt = _dragProxy.AddComponent<RectTransform>();
        rt.sizeDelta = ((RectTransform)transform).sizeDelta;

        var img = _dragProxy.AddComponent<Image>();
        img.raycastTarget = false;

        var def = CharmRegistry.Instance?.Get(containedCharm.definitionId);
        if (def?.icon != null) img.sprite = def.icon;
        img.color = new Color(1f, 1f, 1f, 0.8f);

        // Forrás slot elhalványítása
        if (iconImage != null) iconImage.color = new Color(1f, 1f, 1f, 0.3f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragProxy == null || _rootCanvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_rootCanvas.transform,
            eventData.position,
            _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera,
            out Vector2 localPoint);

        ((RectTransform)_dragProxy.transform).localPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragProxy != null) Destroy(_dragProxy);
        _dragProxy = null;
        if (iconImage != null) iconImage.color = Color.white;

        var cam = eventData.pressEventCamera;

        // Kuka ellenőrzés: ha a kuka zóna fölé ejtettük, töröljük
        if (containedCharm != null && TrashZone != null && TrashZone.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(TrashZone, eventData.position, cam))
        {
            _onTrashRequested?.Invoke(this);
            return;
        }

        // Célslot keresése pozíció alapján – független a raycast/CanvasGroup beállításoktól
        foreach (var slot in _allSlots)
        {
            if (slot == this) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)slot.transform, eventData.position, cam))
            {
                _onSwapRequested?.Invoke(this, slot);
                break;
            }
        }
    }

    // ── Segédek ───────────────────────────────────────────────────────

    Color RarityColor(CharmRarity r) => r switch
    {
        CharmRarity.Rare      => rareColor,
        CharmRarity.Epic      => epicColor,
        CharmRarity.Legendary => legendaryColor,
        _                     => commonColor,
    };

    string RarityInitial(CharmRarity r) => r switch
    {
        CharmRarity.Rare      => "R",
        CharmRarity.Epic      => "E",
        CharmRarity.Legendary => "L",
        _                     => "C",
    };
}
