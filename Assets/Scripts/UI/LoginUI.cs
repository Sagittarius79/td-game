using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Bejelentkezési képernyő – a főmenü Canvas-ára kerül.
///
/// Két állapot:
///   • Nincs bejelentkezve → Google gombot és Vendégként gombot mutat
///   • Be van jelentkezve → profil adatokat mutat (név, szint, XP)
///
/// Unity Editor beállítás:
///   1. Hozz létre egy Panel-t a MainMenu Canvas-on (loginPanel)
///   2. A panel-re add hozzá ezt a scriptet
///   3. Kösd be az UI elemeket az Inspectorban
///   4. A MainMenuUI Start() metódusában hívd meg: LoginUI.Instance?.ShowIfNeeded()
///
/// Szükséges a scene-ben (DontDestroyOnLoad):
///   • GoogleAuthManager (Scripts/Auth/GoogleAuthManager.cs)
///   • UserProgressManager (Scripts/Progression/UserProgressManager.cs)
/// </summary>
public class LoginUI : MonoBehaviour
{
    public static LoginUI Instance { get; private set; }

    // ── UI elemek – bejelentkezési nézet ─────────────────────────────
    [Header("Bejelentkezési panel")]
    public GameObject loginPanel;

    [Header("Bejelentkezési gombok")]
    public Button googleSignInButton;
    public Button guestButton;

    [Header("Státusz szöveg")]
    public TextMeshProUGUI statusText;
    public GameObject loadingIndicator;   // opcionális spinner

    // ── UI elemek – profil nézet (bejelentkezés után) ─────────────────
    [Header("Profil panel (bejelentkezés után)")]
    public GameObject profilePanel;
    public TextMeshProUGUI profileNameText;   // "Kovács János"
    public TextMeshProUGUI profileStatsText;  // "Szint 3 • 720 XP • 5 győzelem"
    public Image profileXPBar;               // XP progress bar (fill image)

    // ── Belső állapot ─────────────────────────────────────────────────
    private bool _awaitingSignIn       = false;
    private bool _characterFlowStarted = false;  // megakadályozza a dupla átirányítást

    // ── Életciklus ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Gombok
        if (googleSignInButton != null)
            googleSignInButton.onClick.AddListener(OnGoogleSignInPressed);
        if (guestButton != null)
            guestButton.onClick.AddListener(OnGuestPressed);

        // GoogleAuthManager esemény feliratkozás
        if (GoogleAuthManager.Instance != null)
        {
            GoogleAuthManager.Instance.OnSignInSuccess += OnSignInSuccess;
            GoogleAuthManager.Instance.OnSignInFailed  += OnSignInFailed;
            GoogleAuthManager.Instance.OnSignedOut     += OnSignedOut;
        }

        if (loadingIndicator != null) loadingIndicator.SetActive(false);
        if (statusText != null)      statusText.gameObject.SetActive(false);

        // Kezdeti UI frissítés
        RefreshUI();

        // Auto-login ellenőrzés: ha a GoogleAuthManager már tüzelt mielőtt
        // feliratkoztunk volna az eseményre, itt kapjuk el
        CheckAutoLogin();
    }

    void OnDestroy()
    {
        if (GoogleAuthManager.Instance == null) return;
        GoogleAuthManager.Instance.OnSignInSuccess -= OnSignInSuccess;
        GoogleAuthManager.Instance.OnSignInFailed  -= OnSignInFailed;
        GoogleAuthManager.Instance.OnSignedOut     -= OnSignedOut;
    }

    // ── Megjelenítés ──────────────────────────────────────────────────

    /// <summary>Megjeleníti a panelt ha a felhasználó nincs bejelentkezve.</summary>
    public void ShowIfNeeded()
    {
        if (loginPanel == null) return;
        bool loggedIn = GoogleAuthManager.Instance != null && GoogleAuthManager.Instance.IsSignedIn;
        loginPanel.SetActive(!loggedIn);
    }

    public void Show()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        RefreshUI();
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(false);
    }

    public void Hide()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        FindObjectOfType<MainMenuUI>()?.SetCharacterCardVisible(true);
    }

    // ── Gomb kezelők ──────────────────────────────────────────────────

    void OnGoogleSignInPressed()
    {
        if (_awaitingSignIn) return;
        _awaitingSignIn = true;
        SetLoading(true);
        SetStatus("Bejelentkezés folyamatban...");

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: böngészős Google OAuth2 Implicit Flow
        // 1. Megnyílik a Google login lap (új ablak/tab)
        // 2. Visszairányít google-callback.html-re → localStorage-ba írja a tokent
        // 3. Itt pollozzuk a localStorage-t és beküldjük a szervernek
        var webglAuth = GoogleAuthWebGL.Instance;
        if (webglAuth == null)
        {
            OnSignInFailed("GoogleAuthWebGL komponens hiányzik a scene-ből.");
            return;
        }
        webglAuth.StartGoogleLogin(
            onTokenReceived: (idToken) =>
            {
                SetStatus("Token megvan, azonosítás...");
                MatchmakingClient.Instance?.VerifyGoogleToken(
                    idToken,
                    onSuccess: (displayName, playerId) =>
                    {
                        GoogleAuthManager.Instance?.SetWebGLSignIn(playerId, displayName, "");
                    },
                    onError: (err) => OnSignInFailed(err)
                );
            },
            onError: (err) => OnSignInFailed(err)
        );
#else
        // Android / Editor: natív Google Sign-In plugin
        GoogleAuthManager.Instance?.SignIn();
#endif
    }

    void OnGuestPressed()
    {
        // Vendég mód: nincs Google fiók, az adatok helyben tárolódnak de névtelen marad
        SetStatus("You are playing as a GUEST – no data sync!");
        StartCoroutine(HideAfterDelay(3.8f));
    }

    // ── GoogleAuthManager callback-ek ─────────────────────────────────

    void OnSignInSuccess(string userId, string displayName, string email)
    {
        _awaitingSignIn = false;
        SetLoading(false);
        SetStatus($"Welcome, {displayName}!");
        RefreshUI();

        if (!_characterFlowStarted)
        {
            _characterFlowStarted = true;
            StartCoroutine(ShowCharacterFlowAfterDelay(1.2f));
        }
    }

    /// <summary>
    /// Induláskor ellenőrzi, hogy az auto-login már megtörtént-e
    /// mielőtt a LoginUI feliratkozott volna az eseményre.
    /// Ha be van lépve de nincs karakter → CharacterSetupUI.
    /// Ha be van lépve és van karakter → főmenü marad (aktív karakter betöltve).
    /// </summary>
    void CheckAutoLogin()
    {
        var auth = GoogleAuthManager.Instance;
        var mgr  = UserProgressManager.Instance;

        if (auth == null || !auth.IsSignedIn) return;
        if (_characterFlowStarted) return;

        _characterFlowStarted = true;
        Hide();

        if (mgr != null && !mgr.HasAnyCharacter)
        {
            // Be van lépve, de még nincs egyetlen karaktere sem → kötelező létrehozni
            CharacterSetupUI.Instance?.Show();
        }
        // Ha van karakter, a főmenü normálisan jelenik meg az aktív karakterrel
    }

    IEnumerator ShowCharacterFlowAfterDelay(float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        Hide();

        bool hasCharacters = UserProgressManager.Instance != null
                          && UserProgressManager.Instance.HasAnyCharacter;

        if (hasCharacters)
            CharacterSelectUI.Instance?.Show();
        else
            CharacterSetupUI.Instance?.Show();
    }

    void OnSignInFailed(string error)
    {
        _awaitingSignIn = false;
        SetLoading(false);
        SetStatus($"Hiba: {error}");
    }

    void OnSignedOut()
    {
        _characterFlowStarted = false;
        _awaitingSignIn       = false;
        RefreshUI();
        Show();
    }

    // ── UI frissítés ──────────────────────────────────────────────────

    void RefreshUI()
    {
        bool loggedIn = GoogleAuthManager.Instance != null && GoogleAuthManager.Instance.IsSignedIn;

        // Bejelentkezési nézet vs profil nézet
        if (googleSignInButton != null) googleSignInButton.gameObject.SetActive(!loggedIn);
        if (guestButton != null)        guestButton.gameObject.SetActive(!loggedIn);
        if (profilePanel != null)       profilePanel.SetActive(loggedIn);

        if (!loggedIn) return;

        var data  = UserProgressManager.Instance?.Data;
        int level = UserProgressManager.Instance?.Level ?? 1;

        if (data == null) return;

        var mgr = UserProgressManager.Instance;

        if (profileNameText != null)
            profileNameText.text = mgr?.AccountDisplayName ?? "";

        if (profileStatsText != null)
            profileStatsText.text = $"Szint {level}  •  {data.totalXP} XP  •  {data.totalWins} győzelem";

        if (profileXPBar != null)
            profileXPBar.fillAmount = data.LevelProgress;
    }

    // ── Segédmetódusok ────────────────────────────────────────────────

    void SetLoading(bool loading)
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(loading);
        if (googleSignInButton != null) googleSignInButton.interactable = !loading;
        if (guestButton != null)        guestButton.interactable = !loading;
    }

    void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Hide();
    }
}
