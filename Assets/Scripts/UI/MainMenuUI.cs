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

    [Header("Google fiók")]
    [Tooltip("Kijelentkezés gomb – csak bejelentkezett állapotban interakcióképes")]
    public Button logoutButton;
    [Tooltip("Opcionális: bejelentkezett user neve/emailje a gomb mellett")]
    public TMP_Text loggedInLabel;

    private CharacterEntryUI _activeCard;

    // ── Életciklus ────────────────────────────────────────────────

    void Start()
    {
        if (characterSelectButton != null)
            characterSelectButton.onClick.AddListener(OnCharacterSelectPressed);

        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutPressed);

        // Bejelentkezési képernyő ha szükséges
        LoginUI.Instance?.ShowIfNeeded();

        // Feliratkozások
        if (GoogleAuthManager.Instance != null)
        {
            GoogleAuthManager.Instance.OnSignInSuccess += OnSignedIn;
            GoogleAuthManager.Instance.OnSignedOut    += OnSignedOut;
        }

        RefreshLoginUI();

        if (UserProgressManager.Instance != null)
            UserProgressManager.Instance.OnXPChanged += OnXPChanged;

        // Karakter kártya megjelenítése
        RefreshCharacterCard();
    }

    void OnDestroy()
    {
        if (GoogleAuthManager.Instance != null)
        {
            GoogleAuthManager.Instance.OnSignInSuccess -= OnSignedIn;
            GoogleAuthManager.Instance.OnSignedOut    -= OnSignedOut;
        }

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
        RefreshLoginUI();
    }

    void OnSignedOut()
    {
        if (_activeCard != null) Destroy(_activeCard.gameObject);
        RefreshLoginUI();
        LoginUI.Instance?.Show();
    }

    public void OnLogoutPressed()
    {
        if (GoogleAuthManager.Instance == null) return;
        GoogleAuthManager.Instance.SignOut();
    }

    void RefreshLoginUI()
    {
        bool signedIn = GoogleAuthManager.Instance != null && GoogleAuthManager.Instance.IsSignedIn;

        if (logoutButton != null)
            logoutButton.interactable = signedIn;

        if (loggedInLabel != null)
        {
            loggedInLabel.gameObject.SetActive(signedIn);
            if (signedIn)
                loggedInLabel.text = GoogleAuthManager.Instance.DisplayName;
        }
    }

    void OnXPChanged(long gained, long total)
    {
        // XP változáskor frissíti a kártyát (pl. visszatérés meccsből)
        RefreshCharacterCard();
    }
}
