using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Minden boss event alap osztálya.
///
/// Új boss event létrehozása:
///   1. Hozz létre egy osztályt: public class XxxBossEvent : BossEventBase { ... }
///   2. Implementáld az OnActivate(float duration) metódust
///   3. Add hozzá a komponenst az _EventManagers GameObject-re
///   4. Húzd be a BossEventManager › Boss Events listájába
///
/// Automatikusan kezeli:
///   - IsActive flag (warning delay alatt is true)
///   - Warning delay (activateSound + warningDelay mp várakozás event előtt)
///   - Banner / notification megjelenítés
///   - deactivateSound lejátszása az event végén
/// </summary>
public abstract class BossEventBase : MonoBehaviour
{
    [Header("Megjelenítés")]
    [Tooltip("Az event neve – banner/notification szövege")]
    public string eventName = "BOSS EVENT";
    [Tooltip("Banner szín a notification-höz")]
    public Color bannerColor = new Color(0.8f, 0.1f, 0.1f);

    [Header("Figyelmeztetés")]
    [Tooltip("Ennyi másodperccel az event előtt szól az activateSound")]
    public float warningDelay = 5f;

    [Header("Hang")]
    public AudioClip activateSound;
    public AudioClip deactivateSound;

    [Header("Banner (opcionális)")]
    public GameObject bannerPanel;
    public TextMeshProUGUI bannerText;
    public float bannerDuration = 3f;

    /// <summary>True a warning delay alatt és az event teljes ideje alatt.</summary>
    public bool IsActive { get; private set; }

    private Coroutine _routine;

    // ── Publikus API ──────────────────────────────────────────────

    public void Activate(float duration)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(EventRoutine(duration));
    }

    // ── Alap coroutine ────────────────────────────────────────────

    IEnumerator EventRoutine(float duration)
    {
        IsActive = true;

        // Figyelmeztetés
        AudioManager.Instance?.PlaySFX(activateSound);
        yield return new WaitForSeconds(warningDelay);

        ShowBanner(duration);

        // Az esemény egyedi logikája
        yield return StartCoroutine(OnActivate(duration));

        AudioManager.Instance?.PlaySFX(deactivateSound);
        IsActive = false;
    }

    /// <summary>
    /// Az egyedi boss event logikája. Implementáld a leszármazott osztályban.
    /// A coroutine addig fut, amíg az event aktív.
    /// </summary>
    protected abstract IEnumerator OnActivate(float duration);

    // ── Segédfüggvények ───────────────────────────────────────────

    void ShowBanner(float duration)
    {
        if (bannerPanel != null && bannerText != null)
        {
            bannerText.text = eventName;
            StartCoroutine(BannerRoutine());
        }
        else
        {
            UIManager.Instance?.ShowNotification($"{eventName} ({duration:F0}s)", bannerColor);
        }
    }

    IEnumerator BannerRoutine()
    {
        bannerPanel.SetActive(true);
        yield return new WaitForSeconds(bannerDuration);
        bannerPanel.SetActive(false);
    }
}
