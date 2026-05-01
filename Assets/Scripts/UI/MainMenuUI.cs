using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Főmenü – játékmód választó + bejelentkezési állapot megjelenítés.
/// A kiválasztott karaktert a CharacterEntryPrefab segítségével jeleníti meg.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Scene nevek")]
    public string soloSceneName = "SampleScene";
    public string pvpSceneName  = "PvPLobby";

    [Header("Aktív karakter kártya")]
    [Tooltip("Ide kerül a CharacterEntryPrefab példánya")]
    public Transform  characterCardParent;
    [Tooltip("Ugyanaz a prefab mint a CharacterSelectUI-ban")]
    public GameObject characterEntryPrefab;

    [Header("Navigáció")]
    public Button characterSelectButton;

    private CharacterEntryUI _activeCard;

    // ── Életciklus ────────────────────────────────────────────────

    void Start()
    {
        if (characterSelectButton != null)
            characterSelectButton.onClick.AddListener(OnCharacterSelectPressed);

        // Bejelentkezési képernyő ha szükséges
        LoginUI.Instance?.ShowIfNeeded();

        // Feliratkozások
        if (GoogleAuthManager.Instance != null)
            GoogleAuthManager.Instance.OnSignInSuccess += OnSignedIn;

        if (UserProgressManager.Instance != null)
            UserProgressManager.Instance.OnXPChanged += OnXPChanged;

        // Karakter kártya megjelenítése
        RefreshCharacterCard();
    }

    void OnDestroy()
    {
        if (GoogleAuthManager.Instance != null)
            GoogleAuthManager.Instance.OnSignInSuccess -= OnSignedIn;

        if (UserProgressManager.Instance != null)
            UserProgressManager.Instance.OnXPChanged -= OnXPChanged;
    }

    // ── Karakter kártya ───────────────────────────────────────────

    public void RefreshCharacterCard()
    {
        if (characterCardParent == null || characterEntryPrefab == null) return;

        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return;

        // Régi kártya törlése
        if (_activeCard != null)
            Destroy(_activeCard.gameObject);

        // Új kártya létrehozása
        var go = Instantiate(characterEntryPrefab, characterCardParent);
        _activeCard = go.GetComponent<CharacterEntryUI>();
        _activeCard?.SetupDisplay(mgr.Data);
    }

    public void SetCharacterCardVisible(bool visible)
    {
        if (characterCardParent != null)
            characterCardParent.gameObject.SetActive(visible);
    }

    // ── Gomb callbackok ───────────────────────────────────────────

    public void OnCharacterSelectPressed()
    {
        if (CharacterSelectUI.Instance == null)
        {
            Debug.LogError("CharacterSelectUI.Instance NULL – a script nincs a scene-ben!");
            return;
        }
        CharacterSelectUI.Instance.Show();
    }

    public void OnSoloPressed()
    {
        XPManager.Instance?.SetMatchType(isPvP: false);
        SceneManager.LoadScene(soloSceneName);
    }

    public void OnPvPPressed()
    {
        XPManager.Instance?.SetMatchType(isPvP: true);
        SceneManager.LoadScene(pvpSceneName);
    }

    // ── Esemény kezelők ───────────────────────────────────────────

    void OnSignedIn(string userId, string displayName, string email)
    {
        RefreshCharacterCard();
    }

    void OnXPChanged(long gained, long total)
    {
        // XP változáskor frissíti a kártyát (pl. visszatérés meccsből)
        RefreshCharacterCard();
    }
}
