#if GOOGLE_SIGN_IN
using Google;
#endif

using System;
using UnityEngine;

/// <summary>
/// Google Sign-In kezelő – singleton, DontDestroyOnLoad.
///
/// ═══════════════════════════════════════════════════════════════
///  TELEPÍTÉSI ÚTMUTATÓ
/// ═══════════════════════════════════════════════════════════════
///
///  1. GOOGLE SIGN-IN UNITY PLUGIN
///     https://github.com/googlesamples/google-signin-unity/releases
///     → Töltsd le a legfrissebb .unitypackage-t
///     → Unity: Assets → Import Package → Custom Package
///
///  2. GOOGLE CLOUD CONSOLE / FIREBASE
///     → Hozz létre projektet: https://console.firebase.google.com/
///     → Authentication → Sign-in method → Google: engedélyezd
///     → Project settings → Add app → Android:
///         • Package name: (Unity Player Settings → Bundle Identifier)
///         • Debug SHA-1: futtatsd terminálban:
///           keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
///     → Töltsd le a google-services.json-t → helyezd az Assets/ mappába
///
///  3. WEB CLIENT ID
///     Firebase Console → Authentication → Settings → Web SDK configuration
///     Vagy Google Cloud Console → APIs & Services → Credentials → OAuth 2.0 Client IDs
///     → Másold be a webClientId mezőbe az Inspectorban
///
///  4. SCRIPTING DEFINE SYMBOL HOZZÁADÁSA
///     Unity → Edit → Project Settings → Player → Other Settings
///     → Scripting Define Symbols: add hozzá: GOOGLE_SIGN_IN
///     (Ez aktiválja a valódi Google bejelentkezési kódot)
///
///  Ha GOOGLE_SIGN_IN nincs definiálva:
///     Tesztelési mód – SimulateSignIn("test_id", "Teszt Játékos", "test@example.com") hívható,
///     minden más function no-op. Így a game compile-olható a plugin nélkül is.
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class GoogleAuthManager : MonoBehaviour
{
    public static GoogleAuthManager Instance { get; private set; }

    [Header("Google Sign-In konfiguráció")]
    [Tooltip("Firebase Console → Authentication → Web SDK configuration → Web API Key\nVagy Google Cloud Console → OAuth 2.0 Web Client ID")]
    public string webClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

    // ── Állapot ──────────────────────────────────────────────────────
    public bool   IsSignedIn  { get; private set; } = false;
    public string UserId      { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string Email       { get; private set; } = "";
    /// <summary>Google id_token – csak éles Android bejelentkezésnél töltődik fel, Editorban üres.</summary>
    public string IdToken     { get; private set; } = "";

    // ── Események ────────────────────────────────────────────────────
    /// <summary>Sikeres bejelentkezés. Paraméterek: (userId, displayName, email)</summary>
    public event Action<string, string, string> OnSignInSuccess;
    /// <summary>Sikertelen bejelentkezés. Paraméter: hibaüzenet</summary>
    public event Action<string> OnSignInFailed;
    /// <summary>Kijelentkezés után tűzik.</summary>
    public event Action OnSignedOut;

    // ── Életciklus ───────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

#if GOOGLE_SIGN_IN
    // ════════════════════════════════════════════════════════════════
    //  ÉLES GOOGLE SIGN-IN (plugin telepítve)
    // ════════════════════════════════════════════════════════════════

    void Start()
    {
#if UNITY_EDITOR
        // Unity Editorban nincs Android JNI – szimulált bejelentkezés
        TrySignInSilently();
#else
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId    = webClientId,
            RequestIdToken = true,
            RequestEmail   = true,
            RequestProfile = true,
            UseGameSignIn  = false
        };
        TrySignInSilently();
#endif
    }

    /// <summary>
    /// Csendes (felugró ablak nélküli) bejelentkezés – app indulásakor hívandó.
    /// Editorban: mentett profil visszaállítása. Eszközön: valódi Google Sign-In.
    /// </summary>
    public void TrySignInSilently()
    {
#if UNITY_EDITOR
        // Editorban nincs Android JNI – mentett profil visszaállítása szimulációval
        if (UserProgressManager.Instance != null && UserProgressManager.Instance.IsLoggedIn)
        {
            var roster = UserProgressManager.Instance.Roster;
            ApplySignIn(roster.userId, roster.displayName, roster.email);
            Debug.Log("GoogleAuthManager [Editor]: mentett profil visszaállítva.");
        }
#else
        GoogleSignIn.DefaultInstance.SignInSilently()
            .ContinueWith(HandleSignInResult,
                System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
#endif
    }

    /// <summary>Interaktív Google Sign-In – megnyitja a Google fiókválasztót.</summary>
    public void SignIn()
    {
#if UNITY_EDITOR
        Debug.LogWarning("GoogleAuthManager: Editor módban valódi Google Sign-In nem lehetséges. Szimulált bejelentkezés fut.");
        SimulateSignIn("editor_test_001", "Teszt Játékos", "test@example.com");
#else
        GoogleSignIn.DefaultInstance.SignIn()
            .ContinueWith(HandleSignInResult,
                System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
#endif
    }

    private void HandleSignInResult(System.Threading.Tasks.Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted)
        {
            string error = "Ismeretlen hiba";
            if (task.Exception?.InnerException != null)
                error = task.Exception.InnerException.Message;
            Debug.LogWarning($"GoogleAuthManager: bejelentkezési hiba – {error}");
            IsSignedIn = false;
            OnSignInFailed?.Invoke(error);
            return;
        }

        if (task.IsCanceled)
        {
            IsSignedIn = false;
            OnSignInFailed?.Invoke("Bejelentkezés megszakítva.");
            return;
        }

        var user = task.Result;
        IdToken = user.IdToken ?? "";      // eltároljuk, hogy MatchmakingClient.Start() használhassa
        ApplySignIn(user.UserId, user.DisplayName, user.Email, user.IdToken);
    }

    /// <summary>Kijelentkezés – megtartja a helyi mentést, csak a munkamenetet törli.</summary>
    public void SignOut()
    {
#if !UNITY_EDITOR
        GoogleSignIn.DefaultInstance.SignOut();
#endif
        ClearSession();
    }

    /// <summary>Teljes lecsatlakozás – a következő SignIn() újra megmutatja a fiókválasztót.</summary>
    public void Disconnect()
    {
#if !UNITY_EDITOR
        GoogleSignIn.DefaultInstance.Disconnect();
#endif
        ClearSession();
    }

#else
    // ════════════════════════════════════════════════════════════════
    //  TESZTELÉSI MÓD (plugin nincs telepítve)
    //  GOOGLE_SIGN_IN define nincs beállítva → compile hiba nélkül fut
    // ════════════════════════════════════════════════════════════════

    void Start()
    {
        // Ha van elmentett profil, csendes bejelentkezés szimulálása
        TrySignInSilently();
    }

    public void TrySignInSilently()
    {
        if (UserProgressManager.Instance != null && UserProgressManager.Instance.IsLoggedIn)
        {
            var roster = UserProgressManager.Instance.Roster;
            ApplySignIn(roster.userId, roster.displayName, roster.email);
            Debug.Log("GoogleAuthManager [TESZT]: csendes bejelentkezés – mentett profil visszaállítva.");
        }
    }

    public void SignIn()
    {
        Debug.LogWarning("GoogleAuthManager: GOOGLE_SIGN_IN define nincs beállítva – TESZTELÉSI mód! " +
                         "Telepítsd a Google Sign-In Unity Plugint, majd add hozzá a GOOGLE_SIGN_IN szimbólumot.");
        SimulateSignIn("test_user_001", "Teszt Játékos", "test@example.com");
    }

    public void SignOut()    => ClearSession();
    public void Disconnect() => ClearSession();

#endif

    // ── Közös metódusok ──────────────────────────────────────────────

    /// <summary>
    /// Szimulált bejelentkezés – Editor módban és teszteléskor használható.
    /// GOOGLE_SIGN_IN define esetén is elérhető (Editor play mode tesztelés).
    /// </summary>
    public void SimulateSignIn(string userId, string displayName, string email)
    {
        ApplySignIn(userId, displayName, email);
        Debug.Log($"GoogleAuthManager [SZIMULÁCIÓ]: bejelentkezve – {displayName}");
    }

    private void ApplySignIn(string userId, string displayName, string email, string idToken = null)
    {
        IsSignedIn    = true;
        UserId        = userId;
        DisplayName   = displayName;
        Email         = email;

        UserProgressManager.Instance?.SetUserInfo(userId, displayName, email);
        Debug.Log($"GoogleAuthManager: bejelentkezve – {displayName} ({email})");
        OnSignInSuccess?.Invoke(userId, displayName, email);

        // Matchmaking session megszerzése
        if (MatchmakingClient.Instance == null) return;

        if (!string.IsNullOrEmpty(idToken))
        {
            // Éles mód: valódi Google token elküldése a szervernek
            MatchmakingClient.Instance.VerifyGoogleToken(idToken,
                onSuccess: (dn, pid) => Debug.Log($"[MMC] Matchmaking session OK: {dn}"),
                onError:   err       => Debug.LogWarning($"[MMC] Matchmaking auth hiba: {err}")
            );
        }
        else
        {
            // Editor / szimulált mód: dev bejelentkezés
            MatchmakingClient.Instance.DevLogin(userId, displayName,
                onSuccess: () => Debug.Log($"[MMC] Dev session OK: {displayName}"),
                onError:   err => Debug.LogWarning($"[MMC] Dev login hiba: {err}")
            );
        }
    }

    private void ClearSession()
    {
        IsSignedIn    = false;
        UserId        = "";
        DisplayName   = "";
        Email         = "";
        OnSignedOut?.Invoke();
        Debug.Log("GoogleAuthManager: kijelentkezve.");
    }
}
