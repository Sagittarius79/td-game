using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// WebGL-specifikus Google OAuth2 login.
///
/// Hogyan működik WebGL-en:
///   1. Application.OpenURL() → böngésző megnyit egy Google login oldalt
///   2. Google visszairányít a játék URL-jére egy "?code=..." paraméterrel
///   3. A játék elolvas egy cookie-t vagy localStorage értéket, amit a
///      login oldal (redirect page) ír ki a válasz után
///   4. A code-ot elküldjük a matchmaking szerverünknek → Google token csere
///
/// FONTOS: Ez a megközelítés egy egyszerű "hosted redirect" oldalt igényel
///         a szerveren (pl. https://kakaoo123.asuscomm.com/google-callback.html)
///         amit a Google OAuth redirect_uri-ba be kell állítani.
///
/// Alternatíva fejlesztéshez: az Android GoogleSignIn plugin adja az id_token-t,
/// azt közvetlenül beküldjük a matchmaking /auth/verify-nak (#if !UNITY_WEBGL blokk).
/// </summary>
public class GoogleAuthWebGL : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR

    // ── Konfiguráció ─────────────────────────────────────────────────

    // Google Cloud Console-ból – Web Client ID (nem Android!)
    // A matchmaking szerver auth.py-ban lévő GOOGLE_CLIENT_ID-nek is ez kell!
    private const string WEB_CLIENT_ID   = "184393218467-o8fat6grcgr0ri75mkc87sj4ssvat6rh.apps.googleusercontent.com";
    private const string REDIRECT_URI    = "https://kakaoo123.asuscomm.com/google-callback.html";
    private const string POLL_KEY        = "google_id_token";  // localStorage kulcs

    private const float POLL_INTERVAL    = 1.5f;
    private const float POLL_TIMEOUT     = 120f;

    // ── Publikus API ──────────────────────────────────────────────────

    /// <summary>
    /// Megnyitja a Google login popup-ot a böngészőben,
    /// majd periodikusan ellenőrzi, hogy megkaptuk-e a tokent.
    /// </summary>
    public void StartGoogleLogin(Action<string> onTokenReceived, Action<string> onError)
    {
        // Google OAuth2 Implicit Flow URL összerakása
        string scope    = Uri.EscapeDataString("openid profile email");
        string redirect = Uri.EscapeDataString(REDIRECT_URI);
        string authUrl  = $"https://accounts.google.com/o/oauth2/v2/auth" +
                          $"?client_id={WEB_CLIENT_ID}" +
                          $"&redirect_uri={redirect}" +
                          $"&response_type=id_token" +
                          $"&scope={scope}" +
                          $"&nonce={Guid.NewGuid():N}";

        // Töröljük a régi token értéket (ha maradt)
        ClearStoredToken();

        // Megnyitjuk a Google login oldalt
        Application.OpenURL(authUrl);

        // Elkezdünk pollozni a localStorage-ban a tokenért
        StartCoroutine(PollForToken(onTokenReceived, onError));
    }

    // ── Privát ───────────────────────────────────────────────────────

    IEnumerator PollForToken(Action<string> onTokenReceived, Action<string> onError)
    {
        float elapsed = 0f;

        while (elapsed < POLL_TIMEOUT)
        {
            yield return new WaitForSeconds(POLL_INTERVAL);
            elapsed += POLL_INTERVAL;

            string token = ReadStoredToken();
            if (!string.IsNullOrEmpty(token))
            {
                ClearStoredToken();
                onTokenReceived?.Invoke(token);
                yield break;
            }
        }

        onError?.Invoke("Google bejelentkezés időtúllépés. Próbáld újra.");
    }

    // JavaScript interop: localStorage olvasás/írás
    // A google-callback.html oldalnak kell elmentenie a tokent:
    //   localStorage.setItem("google_id_token", id_token);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string ReadLocalStorage(string key);

    [System.Runtime.InteropServices.DllImport("__Internal")]
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

    // ── Nem WebGL: üres implementáció (Android/Desktop GoogleSignIn-t használ) ──

    /// <summary>
    /// Nem WebGL platformon ez a komponens nem csinál semmit.
    /// Az Android build a GoogleSignIn Unity plugin-t használja (LoginUI.cs).
    /// </summary>
    public void StartGoogleLogin(Action<string> onTokenReceived, Action<string> onError)
    {
        onError?.Invoke("GoogleAuthWebGL csak WebGL builden aktív.");
    }

#endif
}
