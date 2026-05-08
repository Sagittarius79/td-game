using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Zene és hangeffekt menedzser – singleton, DontDestroyOnLoad.
///
/// Funkciók:
///   - Menü zene (menuMusicTracks) – csak a megadott menü scene-ekben szól
///   - Játék zene (gameMusicTracks) – csak a megadott játék scene-ekben szól
///   - Scene váltáskor automatikus fade átmenet
///   - Hangeffektek (one-shot) lejátszása
///
/// Unity beállítás:
///   1. AudioManager GameObject a MainMenu scene-ben
///   2. Menu Music Tracks → menü zene clip(ek)
///   3. Game Music Tracks → játék zene clip(ek)
///   4. Menu Scenes → pl. "MainMenu"
///   5. Game Scenes → pl. "SampleScene"
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Menü zene")]
    [Tooltip("Ezek a számok szólnak a menüben")]
    public AudioClip[] menuMusicTracks;

    [Tooltip("Ezekben a scene-ekben szól a menü zene")]
    public string[] menuScenes = { "MainMenu", "PvPLobby" };

    [Header("Játék zene")]
    [Tooltip("Ezek a számok szólnak játék közben")]
    public AudioClip[] gameMusicTracks;

    [Tooltip("Ezekben a scene-ekben szól a játék zene")]
    public string[] gameScenes = { "SampleScene" };

    [Header("Lejátszás")]
    [Tooltip("Sorban (false) vagy véletlenszerűen (true) játssza a számokat")]
    public bool shuffle = true;

    [Tooltip("Fade idő zeneszámok között (másodperc)")]
    public float fadeDuration = 1.5f;

    [Header("Hangerő")]
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    // ── Belső állapot ─────────────────────────────────────────────
    private AudioSource _musicSource;
    private AudioSource _sfxSource;

    private AudioClip[] _activeTracks;   // az éppen aktív lista (menü vagy játék)
    private int _currentTrackIndex = -1;
    private Coroutine _fadeCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        _musicSource             = gameObject.AddComponent<AudioSource>();
        _musicSource.loop        = false;
        _musicSource.playOnAwake = false;
        _musicSource.volume      = musicVolume;

        _sfxSource               = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop          = false;
        _sfxSource.playOnAwake   = false;
        _sfxSource.volume        = sfxVolume;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioClip[] tracks = GetTracksForScene(scene.name);

        if (tracks != null && tracks.Length > 0)
        {
            bool listChanged = tracks != _activeTracks;
            _activeTracks = tracks;

            if (listChanged)
            {
                // Menü ↔ játék váltás: új listát kezdünk
                _currentTrackIndex = -1;
                StartCoroutine(PlayAfterSceneReady());
            }
            else if (!_musicSource.isPlaying)
            {
                // Ugyanaz a lista, de valami miatt nem szól – folytatjuk
                StartCoroutine(PlayAfterSceneReady());
            }
            // Ha ugyanaz a lista és már szól → nem nyúlunk hozzá
        }
        else
        {
            _activeTracks = null;
            StopMusic();
        }
    }

    /// <summary>Visszaadja a scene-hez tartozó track listát, vagy null-t ha nincs zene.</summary>
    AudioClip[] GetTracksForScene(string sceneName)
    {
        if (menuScenes != null)
            foreach (var s in menuScenes)
                if (s == sceneName) return menuMusicTracks;

        if (gameScenes != null)
            foreach (var s in gameScenes)
                if (s == sceneName) return gameMusicTracks;

        return null;
    }

    IEnumerator PlayAfterSceneReady()
    {
        yield return null;
        yield return null;
        PlayNextTrack();
    }

    void Update()
    {
        // Ha vége a számnak, következő indul – csak ha van aktív lista
        if (_musicSource != null &&
            !_musicSource.isPlaying &&
            _activeTracks != null &&
            _activeTracks.Length > 0)
        {
            PlayNextTrack();
        }
    }

    // ── Zene vezérlés ─────────────────────────────────────────────

    public void PlayNextTrack()
    {
        if (_activeTracks == null || _activeTracks.Length == 0) return;

        int nextIndex;
        if (_activeTracks.Length == 1)
        {
            nextIndex = 0;
        }
        else if (shuffle)
        {
            do { nextIndex = Random.Range(0, _activeTracks.Length); }
            while (nextIndex == _currentTrackIndex);
        }
        else
        {
            nextIndex = (_currentTrackIndex + 1) % _activeTracks.Length;
        }

        _currentTrackIndex = nextIndex;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeToTrack(_activeTracks[nextIndex]));
    }

    public void StopMusic()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOut());
    }

    public void PauseMusic()  => _musicSource?.Pause();
    public void ResumeMusic() => _musicSource?.UnPause();

    // ── Hangerő ───────────────────────────────────────────────────

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (_musicSource != null) _musicSource.volume = musicVolume;
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (_sfxSource != null) _sfxSource.volume = sfxVolume;
    }

    // ── Hangeffektek ──────────────────────────────────────────────

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    // ── Fade coroutine-ok ─────────────────────────────────────────

    IEnumerator FadeToTrack(AudioClip newClip)
    {
        if (newClip == null) yield break;

        // Fade out
        if (_musicSource.isPlaying)
        {
            float startVol = _musicSource.volume;
            float elapsed  = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed            += Time.deltaTime;
                _musicSource.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeDuration);
                yield return null;
            }
            _musicSource.Stop();
        }

        // Fade in
        _musicSource.clip   = newClip;
        _musicSource.volume = 0f;
        _musicSource.Play();

        float elapsed2 = 0f;
        while (elapsed2 < fadeDuration)
        {
            elapsed2           += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(0f, musicVolume, elapsed2 / fadeDuration);
            yield return null;
        }
        _musicSource.volume = musicVolume;
    }

    IEnumerator FadeOut()
    {
        if (!_musicSource.isPlaying) yield break;

        float startVol = _musicSource.volume;
        float elapsed  = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed            += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeDuration);
            yield return null;
        }
        _musicSource.Stop();
        _musicSource.volume = musicVolume;
    }
}
