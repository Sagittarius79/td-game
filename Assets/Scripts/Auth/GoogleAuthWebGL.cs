using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// WebGL-specifikus Google OAuth2 login.
///
/// Hogyan működik WebGL-en:
///   1. Application.OpenURL() → böngésző új tabban megnyitja a Google login oldalt
///   2. A felhasználó kiválasztja a Google fiókját
///   3. Google visszairányít a google-callback.html-re az id_token-nel
///   4. A callback oldal:
///        a) localStorage-ba írja a tokent (fallback)
///        b) window.postMessage()-gel elküldi a tokent a játék tabnak (elsődleges)
///   5. A játék megkapja a tokent (postMessage vagy localStorage poll) és beküldi a szervernek
///
/// FONTOS: A REDIRECT_URI-nak ugyanazon a domainen kell lennie mint a játéknak,
///         mert localStorage domain-specifikus (same-origin policy).
/// </summary>
public class GoogleAuthWebGL : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR

    // ── Konfiguráció ─────────────────────────────────────────────────

    private const string WEB_CLIENT_ID = "184393218467-o8fat6grcgr0ri75mkc87sj4ssvat6rh.apps.googleusercontent.com";
    private const string REDIRECT_URI  = "https://kakaoo123.asuscomm.com/google-callback.html";
    private const string POLL_KEY      = "google_id_token";

    private const float POLL_INTERVAL = 1.5f;
    private const float POLL_TIMEOUT  = 120f;

    public static GoogleAuthWebGL Instance { get; private set; }

    // Tárolt callback-ek – a postMessage handler ezekhez fér hozzá
    private Action<string> _onTokenReceived;
    private Action<string> _onError;
    private bool           _loginInProgress = false;

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Publikus API ──────────────────────────────────────────────────

    /// <summary>
    /// Megnyitja a Google login oldalt egy új tabban, majd vár a tokenre.
    /// Elsődlegesen window.postMessage-t használ, másodlagosan localStorage poll-t.
    /// </summary>
    public void StartGoogleLogin(Action<string> onTokenReceived, Action<string> onError)
    {
        if (_loginInProgress)
        {
            onError?.Invoke("Bejelentkezés már folyamatban van.");
            return;
        }

        _loginInProgress  = true;
        _onTokenReceived  = onTokenReceived;
        _onError          = onError;

        // Töröljük a régi tokent
        ClearStoredToken();

        // postMessage listener felállítása – ha a callback tab üzen, ez kapja el
        SetupGoogleTokenListener(gameObject.name, nameof(OnGoogleTokenReceived));

        // Google OAuth2 Implicit Flow URL
        string scope    = Uri.EscapeDataString("openid profile email");
        string redirect = Uri.EscapeDataString(REDIRECT_URI);
        string authUrl  = "https://accounts.google.com/o/oauth2/v2/auth"
                        + $"?client_id={WEB_CLIENT_ID}"
                        + $"&redirect_uri={redirect}"
                        + "&response_type=id_token"
                        + $"&scope={scope}"
                        + $"&nonce={Guid.NewGuid():N}";

        Application.OpenURL(authUrl);

        // localStorage poll indítása fallback-ként
        StartCoroutine(PollForToken());
    }

    /// <summary>
    /// Unity SendMessage hívja a JS SetupGoogleTokenListener handler-ből,
    /// amikor a callback oldal postMessage-gel elküldi a tokent.
    /// </summary>
    public void OnGoogleTokenReceived(string idToken)
    {
        if (!_loginInProgress) return;

        StopAllCoroutines();   // poll leállítása
        ClearStoredToken();
        FinishLogin(idToken);
    }

    // ── Privát ───────────────────────────────────────────────────────

    IEnumerator PollForToken()
    {
        float elapsed = 0f;

        while (elapsed < POLL_TIMEOUT)
        {
            yield return new WaitForSeconds(POLL_INTERVAL);
            elapsed += POLL_INTERVAL;

            if (!_loginInProgress) yield break;   // postMessage már megoldotta

            string token = ReadStoredToken();
            if (!string.IsNullOrEmpty(token))
            {
                ClearStoredToken();
                FinishLogin(token);
                yield break;
            }
        }

        if (_loginInProgress)
        {
            _loginInProgress = false;
            var cb = _onError;
            _onTokenReceived = null;
            _onError         = null;
            cb?.Invoke("Google bejelentkezés időtúllépés (120 mp). Próbáld újra.");
        }
    }

    void FinishLogin(string idToken)
    {
        _loginInProgress = false;
        var cb = _onTokenReceived;
        _onTokenReceived = null;
        _onError         = null;
        cb?.Invoke(idToken);
    }

    // ── JS interop ────────────────────────────────────────────────────

    [DllImport("__Internal")]
    private static extern void SetupGoogleTokenListener(string objectName, string methodName);

    [DllImport("__Internal")]
    private static extern string ReadLocalStorage(string key);

    [DllImport("__Internal")]
    private static extern void RemoveLocalStorage(string key);

    string ReadStoredToken()
    {
        try { return ReadLocalStorage(POLL_KEY); }
        catch { return null; }
    }

    void ClearStoredToken()
    {
        try { RemoveLocalStorage(POLL_KEY); }
        catch { }
    }

#else

    // ── Nem WebGL: üres implementáció ────────────────────────────────

    public static GoogleAuthWebGL Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartGoogleLogin(Action<string> onTokenReceived, Action<string> onError)
    {
        onError?.Invoke("GoogleAuthWebGL csak WebGL builden aktív.");
    }

#endif
}
