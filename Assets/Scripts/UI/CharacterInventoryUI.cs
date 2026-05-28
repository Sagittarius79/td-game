using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Karakter felszerelés / képesség szerkesztő oldal.
///
/// Unity Editor beállítás:
///   1. CharacterInventoryPanel-en belül:
///      - CharacterCardSlot  (üres Transform a kártyának)
///      - KillCounterText    (TextMeshProUGUI)
///      - CrystalCountText   (TextMeshProUGUI)
///      - CharmGrid          (GridLayoutGroup, 6 col × 4 row = 24 slot)
///      - EquipSlotsParent   (HorizontalLayoutGroup, 5 slot)
///      - BackButton
///   2. SlotPrefab           – egy GameObject Image + CharmSlotUI
///   3. Kösd be az Inspector mezőket
/// </summary>
public class CharacterInventoryUI : MonoBehaviour
{
    public static CharacterInventoryUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject inventoryPanel;

    [Header("Karakter kártya")]
    public Transform  characterCardSlot;
    public GameObject characterEntryPrefab;

    [Header("Kill és kristály számláló")]
    public TextMeshProUGUI killCounterText;
    public TextMeshProUGUI crystalCountText;
    [Tooltip("A kristály ikon mögötti ragyogás Image – folyamatosan pulzál")]
    public Image           crystalGlowImage;
    [Tooltip("A kristály ragyogás átmérője pixelben")]
    public float           crystalGlowSize = 90f;

    [Header("Charm rács (6×4 = 24 slot)")]
    public Transform  charmGridParent;    // GridLayoutGroup-os konténer
    public GameObject charmSlotPrefab;    // egy slot prefab (Image + CharmSlotUI)

    [Header("Equip slotok (5 db)")]
    public Transform  equipSlotsParent;   // HorizontalLayoutGroup-os konténer
    [Tooltip("Az equip slotok alá kerülő effekt összesítő szöveg")]
    public TextMeshProUGUI equipEffectsText;

    [Header("Gombok")]
    public Button backButton;

    [Header("Kuka (charm törlés)")]
    [Tooltip("A kuka grafika RectTransform-ja – ide húzva a charm törlődik")]
    public RectTransform trashZone;

    [Header("Hang")]
    [Tooltip("Hang ami lejátszódik két charm egyesítésekor")]
    public AudioClip mergeSound;
    [Tooltip("Hang ami lejátszódik egy charm kukába dobásakor")]
    public AudioClip trashSound;

    // ── Belső állapot ─────────────────────────────────────────────────
    private CharacterEntryUI  _card;
    private List<CharmSlotUI> _gridSlots       = new List<CharmSlotUI>();
    private List<CharmSlotUI> _equipSlots      = new List<CharmSlotUI>();
    private Coroutine         _crystalGlowLoop;

    const int GridSize  = 24;   // 6 × 4
    const int EquipSize = 5;

    // ═════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(Hide);

        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        UIGlowHelper.ApplyTo(crystalGlowImage, crystalGlowSize); // soft sprite + méret + raycast kikapcs.
        CharmSlotUI.TrashZone = trashZone;                       // kuka zóna a slotoknak
        BuildSlots();

        // Charm hozzáadás / elvesztés figyelése
        if (UserProgressManager.Instance != null)
        {
            UserProgressManager.Instance.OnCharmAdded += OnCharmReceived;
            UserProgressManager.Instance.OnCharmLost  += OnCharmDropped;
        }
    }

    void OnDestroy()
    {
        if (UserProgressManager.Instance != null)
        {
            UserProgressManager.Instance.OnCharmAdded -= OnCharmReceived;
            UserProgressManager.Instance.OnCharmLost  -= OnCharmDropped;
        }
    }

    void OnCharmReceived(string definitionId)
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
            RefreshAllSlots();
    }

    void OnCharmDropped(string definitionId)
    {
        var def = CharmRegistry.Instance?.Get(definitionId);
        string name = def != null ? def.displayName : definitionId;
        Debug.LogWarning($"[Inventory] Charm elveszett (teli): {name}");
        // Ide köthetsz UI értesítést (popup, toast, stb.)
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    public void Show()
    {
        if (inventoryPanel == null) return;
        inventoryPanel.SetActive(true);
        RefreshCard();
        RefreshCounters();
        RefreshAllSlots();
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);

        // Kristály ragyogás elindítása
        if (_crystalGlowLoop != null) StopCoroutine(_crystalGlowLoop);
        _crystalGlowLoop = StartCoroutine(CrystalGlowLoop());
    }

    public void Hide()
    {
        // Kristály ragyogás leállítása
        if (_crystalGlowLoop != null)
        {
            StopCoroutine(_crystalGlowLoop);
            _crystalGlowLoop = null;
        }
        if (crystalGlowImage != null)
            crystalGlowImage.rectTransform.localScale = Vector3.one;

        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        DetailedCharacterUI.Instance?.Show();
    }

    /// <summary>Folyamatosan pulzáló kristály ragyogás – Show/Hide között fut.</summary>
    System.Collections.IEnumerator CrystalGlowLoop()
    {
        if (crystalGlowImage == null) yield break;

        // Fekete ragyogás
        Color glowColor = new Color(0f, 0f, 0f);
        float t         = 0f;
        float speedMult = Random.Range(0.9f, 1.1f); // ±10% véletlenszerű eltérés

        while (true)
        {
            t += Time.unscaledDeltaTime * speedMult;

            // Lassú lélegzés (fele olyan gyors mint korábban)
            float pulse = (Mathf.Sin(t * Mathf.PI * 0.8f) + 1f) * 0.5f;

            glowColor.a = Mathf.Lerp(0.25f, 0.80f, pulse);
            crystalGlowImage.color = glowColor;

            float gs = Mathf.Lerp(1.0f, 1.3f, pulse);
            crystalGlowImage.rectTransform.localScale = Vector3.one * gs;

            yield return null;
        }
    }

    // ── Slot építés (egyszer, Start-ban) ─────────────────────────────

    void BuildSlots()
    {
        if (charmSlotPrefab == null) return;

        // Szerkesztői előnézet (GridPreview) gyerekeinek törlése
        if (charmGridParent != null)
            for (int i = charmGridParent.childCount - 1; i >= 0; i--)
                Destroy(charmGridParent.GetChild(i).gameObject);
        if (equipSlotsParent != null)
            for (int i = equipSlotsParent.childCount - 1; i >= 0; i--)
                Destroy(equipSlotsParent.GetChild(i).gameObject);

        // Grid slotok
        if (charmGridParent != null)
        {
            for (int i = 0; i < GridSize; i++)
            {
                var go   = Instantiate(charmSlotPrefab, charmGridParent);
                var slot = go.GetComponent<CharmSlotUI>();
                if (slot == null) slot = go.AddComponent<CharmSlotUI>();
                slot.Init(CharmSlotUI.SlotType.Grid, i, OnSwapRequested, OnTrashRequested);
                _gridSlots.Add(slot);
            }
        }

        // Equip slotok
        if (equipSlotsParent != null)
        {
            for (int i = 0; i < EquipSize; i++)
            {
                var go   = Instantiate(charmSlotPrefab, equipSlotsParent);
                var slot = go.GetComponent<CharmSlotUI>();
                if (slot == null) slot = go.AddComponent<CharmSlotUI>();
                slot.Init(CharmSlotUI.SlotType.Equip, i, OnSwapRequested, OnTrashRequested);
                _equipSlots.Add(slot);
            }
        }
    }

    // ── Frissítések ───────────────────────────────────────────────────

    void RefreshAllSlots()
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return;

        var charms = mgr.Data.charms;

        // Grid slotok
        for (int i = 0; i < _gridSlots.Count; i++)
        {
            var charm = charms.Find(c => c.gridSlot == i && c.equipSlot == -1);
            _gridSlots[i].Refresh(charm);
        }

        // Equip slotok
        for (int i = 0; i < _equipSlots.Count; i++)
        {
            var charm = charms.Find(c => c.equipSlot == i);
            _equipSlots[i].Refresh(charm);
        }

        RefreshEquipEffects();
    }

    void RefreshEquipEffects()
    {
        if (equipEffectsText == null) return;

        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) { equipEffectsText.text = ""; return; }

        // Csak equip slotban lévő charmok
        var equipped = mgr.Data.charms.FindAll(c => c.equipSlot >= 0);

        // Effektek összesítése típus szerint
        var totals = new Dictionary<CharmEffectType, float>();
        foreach (var ci in equipped)
        {
            var def = CharmRegistry.Instance?.Get(ci.definitionId);
            if (def == null || def.effectType == CharmEffectType.None) continue;
            totals.TryGetValue(def.effectType, out float current);
            totals[def.effectType] = current + def.effectValue;
        }

        if (totals.Count == 0) { equipEffectsText.text = ""; return; }

        var sb = new System.Text.StringBuilder();
        foreach (var kv in totals)
            sb.AppendLine(EffectLine(kv.Key, kv.Value));

        equipEffectsText.text = sb.ToString().TrimEnd();
    }

    static string EffectLine(CharmEffectType type, float value)
    {
        // Egész szám ha nincs töredék, egyébként 1 tizedesjegy
        string v = (value % 1f == 0f) ? $"{value:0}" : $"{value:0.#}";

        return type switch
        {
            CharmEffectType.ArrowDamageBonus      => $"+{v}% Arrow Damage",
            CharmEffectType.CritChanceStoneBonus  => $"+{v}% Crit Chance (Stone Towers)",
            CharmEffectType.MultiShotBonus        => $"+{v} Multi Shot",
            CharmEffectType.AOERadiusBonus        => $"+{v}% AOE Radius",
            CharmEffectType.StoneDamageBonus      => $"+{v}% Stone Damage",
            CharmEffectType.AOECritChanceBonus    => $"+{v}% AOE Crit Chance",
            CharmEffectType.StoneStunChance       => $"+{v}% Stun Chance (Stone Towers)",
            CharmEffectType.MageRangeBonus        => $"+{v}% Mage Range",
            CharmEffectType.MageDamageBonus       => $"+{v}% Mage Damage",
            CharmEffectType.AttackSpeedBonus      => $"+{v}% Attack Speed",
            CharmEffectType.GoldBonus             => $"+{v} Gold / 20 kills",
            CharmEffectType.TowerArmorBonus       => $"+{v}% Tower Armor",
            CharmEffectType.InstantKillChanceMagic => $"+{v}% Chance To Instant Kill (Magic)",
            CharmEffectType.LaserArmorStripChance => $"+{v}% Chance To Strip Armor (Laser)",
            CharmEffectType.JavelinBurnChance     => $"+{v}% Burn Chance (Javelin)",
            CharmEffectType.PoisonBounceChance    => $"+{v}% Poison Bounce Chance (Poison)",
            CharmEffectType.TeleportHalveHpChance => $"+{v}% Halve HP on Teleport Chance (Teleport)",
            CharmEffectType.TurulExtraBirdChance  => $"+{v}% Chance To Extra Bird (Turul)",
            _                                     => $"+{v} {type}",
        };
    }

    void RefreshCounters()
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null) return;

        long kills         = mgr.TotalMonstersKilled;
        long perCrystal    = UserProgressManager.KillsPerCrystal;
        long nextIn        = perCrystal - (kills % perCrystal);

        if (killCounterText != null)
            killCounterText.text = $"Kills: {kills:N0}  (next crystal in {nextIn:N0})";  // already English

        if (crystalCountText != null)
            crystalCountText.text = $"× {mgr.Crystals}";
    }

    void RefreshCard()
    {
        if (characterCardSlot == null || characterEntryPrefab == null) return;
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return;

        if (_card != null) Destroy(_card.gameObject);
        var go = Instantiate(characterEntryPrefab, characterCardSlot);
        _card  = go.GetComponent<CharacterEntryUI>();
        _card?.SetupDisplay(mgr.Data);
    }

    // ── Swap logika ───────────────────────────────────────────────────

    void OnSwapRequested(CharmSlotUI from, CharmSlotUI to)
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return;

        var charms = mgr.Data.charms;
        var fromCharm = from.containedCharm;
        var toCharm   = to.containedCharm;

        // ── MERGE: azonos típus ÉS azonos szint → eggyel magasabb szint ──
        if (fromCharm != null && toCharm != null && TryMergeCharms(from, to, fromCharm, toCharm))
            return;

        // Forrás charm pozíciójának frissítése
        if (fromCharm != null)
        {
            fromCharm.gridSlot  = to.slotType == CharmSlotUI.SlotType.Grid  ? to.slotIndex  : -1;
            fromCharm.equipSlot = to.slotType == CharmSlotUI.SlotType.Equip ? to.slotIndex  : -1;
        }

        // Ha a célslotban is volt charm, visszatesszük a forrás helyére
        if (toCharm != null)
        {
            toCharm.gridSlot  = from.slotType == CharmSlotUI.SlotType.Grid  ? from.slotIndex : -1;
            toCharm.equipSlot = from.slotType == CharmSlotUI.SlotType.Equip ? from.slotIndex : -1;
        }

        // UI frissítés
        from.Refresh(toCharm);
        to.Refresh(fromCharm);

        // Effekt összesítő frissítése
        RefreshEquipEffects();

        // Mentés
        mgr.Save();
    }

    /// <summary>
    /// Kukába dobott charm törlése – a slotból és a mentett adatból is.
    /// </summary>
    void OnTrashRequested(CharmSlotUI slot)
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return;

        var charm = slot.containedCharm;
        if (charm == null) return;

        mgr.Data.charms.Remove(charm);
        slot.Refresh(null);

        if (trashSound != null)
            AudioManager.Instance?.PlaySFX(trashSound, CharmEffects.Volume);

        RefreshEquipEffects();
        mgr.Save();
    }

    /// <summary>
    /// Ha a két charm azonos típusú ÉS azonos szintű, egyesíti őket eggyel magasabb szintűvé.
    /// A célslotban (to) jön létre a felfejlesztett charm, a forrás (from) eltűnik.
    /// True ha az egyesítés megtörtént.
    /// </summary>
    bool TryMergeCharms(CharmSlotUI from, CharmSlotUI to, CharmInstance fromCharm, CharmInstance toCharm)
    {
        var reg = CharmRegistry.Instance;
        if (reg == null) return false;

        var fromDef = reg.Get(fromCharm.definitionId);
        var toDef   = reg.Get(toCharm.definitionId);
        if (fromDef == null || toDef == null) return false;

        // Azonos típus + azonos szint?
        if (fromDef.effectType != toDef.effectType || fromDef.level != toDef.level)
            return false;

        // Van eggyel magasabb szint?
        var upgrade = reg.GetUpgrade(toDef.effectType, toDef.level);
        if (upgrade == null) return false; // pl. már max szint → sima csere fut le

        var mgr = UserProgressManager.Instance;

        // Célslot charm felfejlesztése, forrás charm eltávolítása
        toCharm.definitionId = upgrade.id;
        mgr.Data.charms.Remove(fromCharm);

        from.Refresh(null);
        to.Refresh(toCharm);

        // Látványos felfejlesztés effekt a célsloton
        to.PlayMergeEffect();

        // Egyesítés hang
        if (mergeSound != null)
            AudioManager.Instance?.PlaySFX(mergeSound, CharmEffects.Volume);

        RefreshEquipEffects();
        mgr.Save();
        return true;
    }
}
