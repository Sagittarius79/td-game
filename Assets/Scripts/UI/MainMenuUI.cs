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
    public Button soloButton;
    public Button pvpButton;
    [Tooltip("Ha a PvP gombon kívül egy teljes konténert kell elrejteni SSF karakterhez.")]
    public GameObject pvpButtonRoot;

    [Header("Tutorial")]
    public Button tooltipsButton;

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
        if (soloButton != null)
            soloButton.onClick.AddListener(OnSoloPressed);
        if (pvpButton != null)
            pvpButton.onClick.AddListener(OnPvPPressed);

        if (tooltipsButton != null)
            tooltipsButton.onClick.AddListener(OnTooltipsPressed);

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
        RefreshModeButtons();

        // Karakteradatok feltöltése a szerverre (visszatérés meccsből is lefut)
        ServerSyncManager.GetOrCreate().TriggerSync();
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
        RefreshModeButtons();
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

    public void OnTooltipsPressed()
    {
        TutorialCardsUI.Instance?.Show(() => { }, auto: false);
    }

    public void OnSoloPressed()
    {
        var upm = UserProgressManager.Instance;
        if (upm == null || !upm.HasCharacter)
            return;

        XPManager.Instance?.SetMatchType(isPvP: false);
        SceneManager.LoadScene(soloSceneName);
    }

    public void OnPvPPressed()
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter)
            return;

        var data = mgr.Data;
        if (data.IsSSF)
            return;

        XPManager.Instance?.SetMatchType(isPvP: true);
        SceneManager.LoadScene(pvpSceneName);
    }

    void RefreshModeButtons()
    {
        var mgr = UserProgressManager.Instance;
        bool hasCharacter = mgr != null && mgr.HasCharacter;
        bool isSsf = hasCharacter && mgr.Data.IsSSF;

        if (soloButton != null)
            soloButton.interactable = hasCharacter;

        if (pvpButton != null)
        {
            pvpButton.interactable = hasCharacter && !isSsf;
            if (pvpButtonRoot == null)
                pvpButton.gameObject.SetActive(!isSsf);
        }

        if (pvpButtonRoot != null)
            pvpButtonRoot.SetActive(!isSsf);
    }

    // ── Esemény kezelők ───────────────────────────────────────────

    void OnSignedIn(string userId, string displayName, string email)
    {
        RefreshCharacterCard();
        RefreshLoginUI();
        RefreshModeButtons();
    }

    void OnSignedOut()
    {
        if (_activeCard != null) Destroy(_activeCard.gameObject);
        RefreshLoginUI();
        RefreshModeButtons();
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
