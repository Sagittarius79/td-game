using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Egy karakter kártyája a CharacterSelectUI listájában.
///
/// Törlés flow:
///   1. deleteButton megnyomva → normalView elrejtve, confirmView megjelenik
///   2. confirmYesButton → onDelete callback, kártya eltávolítva
///   3. confirmNoButton  → normalView visszaállítva
///
/// Unity Editor beállítás (Prefab-ban):
///   • NormalView      – a kártya normál tartalma (szövegek, select gomb, delete gomb)
///   • ConfirmView     – megerősítő rész ("Biztos?" + Igen + Nem gomb)
///   Mindkettő ugyanazon a kártyán belül, ConfirmView alapból INACTIVE.
/// </summary>
public class CharacterEntryUI : MonoBehaviour
{
    [Header("Normál nézet")]
    public GameObject normalView;               // a kártya fő tartalma
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI statsText;
    public TextMeshProUGUI modeText;
    public TextMeshProUGUI rankText;            // pl. "Rank 21"
    public GameObject skillPointsRow;           // NormalBView/SkillPoint konténer (szám + label együtt)
    public TextMeshProUGUI skillPointsText;     // elosztatlan skill pontok száma
    public Image xpBarFill;
    public Button selectButton;
    public Button deleteButton;                 // szemetes / X ikon gomb

    [Header("Megerősítő nézet")]
    public GameObject confirmView;             // "Biztos törölni akarod?" panel
    public TextMeshProUGUI confirmNameText;    // mutatja a karakter nevét: "Törlöd: DragonSlayer?"
    public Button confirmYesButton;
    public Button confirmNoButton;

    private string         _characterId;
    private Action<string> _onDelete;

    // ── Főmenüs megjelenítés (csak olvasható, törlés nélkül) ──────────

    /// <summary>
    /// Főmenüben használt mód: csak megjeleníti az adatokat.
    /// Törlés gomb elrejtve, kártya kattintásra karakterválasztót nyit.
    /// </summary>
    public void SetupDisplay(UserProgressData data)
    {
        _characterId = data.characterId;

        if (characterNameText != null) characterNameText.text = data.characterName;
        if (levelText         != null) levelText.text         = $"Lvl {data.Level}";
        if (modeText          != null) modeText.text          = data.IsSSF ? "SSF" : "PvP";
        if (statsText         != null) statsText.text         = $"{data.XPInCurrentLevel}/{data.XPNeededForNextLevel}";
        if (xpBarFill         != null) xpBarFill.fillAmount   = data.LevelProgress;
        RefreshSkillPoints(data.availableSkillPoints);
        FetchAndShowRank(data);

        // Törlés gomb elrejtése
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);

        // Kártya megnyomása → részletes karakter oldal megnyitása
        if (selectButton != null)
            selectButton.onClick.AddListener(() => DetailedCharacterUI.Instance?.Show());

        // Megerősítő nézet soha nem kell főmenüben
        SetConfirmVisible(false);
    }

    // ── Inicializálás ─────────────────────────────────────────────────

    public void Setup(UserProgressData data, Action<string> onSelected, Action<string> onDelete)
    {
        _characterId = data.characterId;
        _onDelete    = onDelete;

        // Szövegek
        if (characterNameText != null) characterNameText.text = data.characterName;
        if (levelText         != null) levelText.text         = $"Lvl {data.Level}";
        if (modeText          != null) modeText.text          = data.IsSSF ? "SSF" : "PvP";
        if (statsText         != null) statsText.text         = $"{data.XPInCurrentLevel}/{data.XPNeededForNextLevel}";
        if (xpBarFill         != null) xpBarFill.fillAmount   = data.LevelProgress;
        RefreshSkillPoints(data.availableSkillPoints);
        FetchAndShowRank(data);

        // Gombok
        if (selectButton != null)
            selectButton.onClick.AddListener(() => onSelected(_characterId));

        if (deleteButton != null)
            deleteButton.onClick.AddListener(ShowConfirm);

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(HideConfirm);

        // Megerősítő szöveg
        if (confirmNameText != null)
            confirmNameText.text = $"Törlöd: {data.characterName}?";

        // Kezdeti állapot
        SetConfirmVisible(false);
    }

    // ── Megerősítés ───────────────────────────────────────────────────

    void ShowConfirm()
    {
        SetConfirmVisible(true);
    }

    void HideConfirm()
    {
        SetConfirmVisible(false);
    }

    void OnConfirmYes()
    {
        UserProgressManager.Instance?.DeleteCharacter(_characterId);
        _onDelete?.Invoke(_characterId);
    }

    void SetConfirmVisible(bool show)
    {
        if (normalView  != null) normalView.SetActive(!show);
        if (confirmView != null) confirmView.SetActive(show);
    }

    void RefreshSkillPoints(int points)
    {
        bool show = points > 0;
        if (skillPointsRow  != null) skillPointsRow.SetActive(show);
        if (skillPointsText != null)
        {
            skillPointsText.gameObject.SetActive(show);
            skillPointsText.text = $"{points}";
        }
    }

    void FetchAndShowRank(UserProgressData data)
    {
        if (rankText == null) return;

        var mmc = MatchmakingClient.Instance;

        if (data != null && data.IsSSF)
        {
            // Cache-elt SSF rang azonnali megjelenítése, majd frissítés
            rankText.text = mmc != null && mmc.CachedSSFRank > 0
                ? $"SSF | Rank {mmc.CachedSSFRank}"
                : "SSF | Rank –";

            mmc?.FetchMySSFRank(
                onSuccess: rank => { if (rankText != null) rankText.text = $"SSF | Rank {rank}"; },
                onError:   _    => { }
            );
            return;
        }

        if (mmc == null) { rankText.text = "Rank –"; return; }

        // Cache-elt PvP rang azonnali megjelenítése amíg a friss adat megérkezik
        rankText.text = mmc.CachedRank > 0 ? $"Rank {mmc.CachedRank}" : "Rank –";

        mmc.FetchMyRank(
            onSuccess: rank => { if (rankText != null) rankText.text = $"Rank {rank}"; },
            onError:   _    => { }
        );
    }

}
