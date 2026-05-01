using UnityEngine;
using UnityEngine.UI;
using ZXing;
using System.Collections;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  QR KÓD OLVASÓ
///  A telefon kameráját megnyitja és beolvassa a QR kódot.
///  Ha megtalálja az IP-t, automatikusan csatlakozik.
///
///  Unity beállítás:
///   - JoinPanel-re rakni
///   - cameraPreview: RawImage a kamera képének (opcionális)
///   - Scan QR gombhoz kösd be az OnScanPressed() metódust
/// ═══════════════════════════════════════════════════════
/// </summary>
public class QRCodeScanner : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("RawImage ahol a kamera kép látszik (opcionális)")]
    public RawImage cameraPreview;

    [Tooltip("Scan gomb – beolvasás közben elrejtjük")]
    public GameObject scanButton;

    [Tooltip("Állapot szöveg (pl. 'Irányítsd a kamerát a QR kódra...')")]
    public TMPro.TextMeshProUGUI statusText;

    [Header("Beállítások")]
    [Tooltip("Hány másodpercig próbáljon olvasni mielőtt megáll")]
    public float scanTimeout = 15f;

    // ── Belső állapot ─────────────────────────────────────
    private WebCamTexture webCam;
    private BarcodeReader  reader;
    private bool           isScanning;

    // A sikeres olvasás után hívjuk meg a csatlakozást
    private System.Action<string> onIPFound;

    // ══════════════════════════════════════════════════════
    //  PUBLIKUS API
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Elindítja a QR olvasást.
    /// onIPFound: callback amit az IP-vel hív meg sikeres olvasáskor.
    /// </summary>
    public void StartScan(System.Action<string> onIPFound)
    {
        this.onIPFound = onIPFound;
        StartCoroutine(ScanRoutine());
    }

    public void StopScan()
    {
        isScanning = false;
        StopAllCoroutines();
        CloseCamera();
        SetStatus("");
    }

    // ── Gomb callback ─────────────────────────────────────

    public void OnScanPressed()
    {
        if (isScanning) { StopScan(); return; }

        // PvPLobbyUI-tól kérjük a callback-et
        var lobby = FindFirstObjectByType<PvPLobbyUI>();
        if (lobby != null)
            StartScan(lobby.OnQRScanned);
    }

    // ══════════════════════════════════════════════════════
    //  SCAN COROUTINE
    // ══════════════════════════════════════════════════════

    IEnumerator ScanRoutine()
    {
        isScanning = true;
        reader     = new BarcodeReader();

        // Kamera engedély és megnyitás
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            SetStatus("Kamera engedély megtagadva!");
            isScanning = false;
            yield break;
        }

        webCam = new WebCamTexture();
        webCam.Play();

        // Várjuk meg amíg a kamera ténylegesen elindult és ismerjük a méretet/orientációt
        yield return new WaitUntil(() => webCam.width > 16);

        if (cameraPreview != null)
        {
            cameraPreview.texture = webCam;
            // A telefon kamera képe forgítva érkezik – korrigáljuk az elforgatást
            cameraPreview.rectTransform.localEulerAngles =
                new UnityEngine.Vector3(0f, 0f, -webCam.videoRotationAngle);
            cameraPreview.gameObject.SetActive(true);
        }
        if (scanButton != null) scanButton.SetActive(false);

        SetStatus("Irányítsd a kamerát a QR kódra...");

        float elapsed = 0f;

        while (isScanning && elapsed < scanTimeout)
        {
            elapsed += Time.deltaTime;

            if (webCam.isPlaying && webCam.width > 16)
            {
                try
                {
                    var result = reader.Decode(
                        webCam.GetPixels32(),
                        webCam.width,
                        webCam.height);

                    if (result != null && !string.IsNullOrEmpty(result.Text))
                    {
                        // IP megtalálva!
                        CloseCamera();
                        isScanning = false;
                        SetStatus($"Csatlakozás: {result.Text}");
                        onIPFound?.Invoke(result.Text);
                        yield break;
                    }
                }
                catch { /* olvasási hiba, folytatjuk */ }
            }

            yield return new WaitForSeconds(0.2f); // 5x / mp ellenőrzés
        }

        // Timeout
        CloseCamera();
        isScanning = false;
        SetStatus("Nem sikerült beolvasni. Próbáld újra.");
        if (scanButton != null) scanButton.SetActive(true);
    }

    // ══════════════════════════════════════════════════════
    //  SEGÉD METÓDUSOK
    // ══════════════════════════════════════════════════════

    void CloseCamera()
    {
        if (webCam != null && webCam.isPlaying)
            webCam.Stop();

        if (cameraPreview != null)
            cameraPreview.gameObject.SetActive(false);

        if (scanButton != null)
            scanButton.SetActive(true);
    }

    void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    void OnDestroy()
    {
        // Scene unload / objektum pusztítás közben NEM hívhatunk SetActive-et a
        // gyerek/testvér GameObject-eken – Unity warning: "GameObjects can not be
        // made active when they are being destroyed." Itt csak a kamerát állítjuk le.
        if (webCam != null && webCam.isPlaying)
            webCam.Stop();
    }
}
