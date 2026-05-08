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
    public TextMeshProUGUI skillPointsText;     // elosztatlan skill pontok
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
        if (statsText         != null) statsText.text         = $"{data.totalXP} XP  •  {data.totalWins}W / {data.totalLosses}L";
        if (xpBarFill         != null) xpBarFill.fillAmount   = data.LevelProgress;
        RefreshSkillPoints(data.availableSkillPoints);

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
        if (statsText         != null) statsText.text         = $"{data.totalXP} XP  •  {data.totalWins}W / {data.totalLosses}L";
        if (xpBarFill         != null) xpBarFill.fillAmount   = data.LevelProgress;
        RefreshSkillPoints(data.availableSkillPoints);

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
        if (skillPointsText == null) return;
        skillPointsText.gameObject.SetActive(points > 0);
        skillPointsText.text = $"{points}";
    }
}
