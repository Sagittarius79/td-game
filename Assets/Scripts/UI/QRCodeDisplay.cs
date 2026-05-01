using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.QrCode;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  QR KÓD GENERÁTOR
///  Az IP címből QR kódot generál és megjeleníti egy RawImage-en.
///  A másik játékos ezt beolvassa a QRCodeScanner-rel.
///
///  Unity beállítás:
///   - HostPanel-re rakni
///   - qrImage: egy RawImage UI elem a panelen
/// ═══════════════════════════════════════════════════════
/// </summary>
public class QRCodeDisplay : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("RawImage amire a QR kód kerül")]
    public RawImage qrImage;

    [Tooltip("QR kód mérete pixelben")]
    public int qrSize = 256;

    // ── QR kód generálása ────────────────────────────────

    /// <summary>IP cím alapján generál és megjelenít egy QR kódot.</summary>
    public void ShowQR(string ipAddress)
    {
        if (qrImage == null) return;

        Texture2D tex = GenerateQR(ipAddress);
        qrImage.texture = tex;
        qrImage.gameObject.SetActive(true);
    }

    public void HideQR()
    {
        if (qrImage != null)
            qrImage.gameObject.SetActive(false);
    }

    Texture2D GenerateQR(string text)
    {
        var writer = new BarcodeWriter
        {
            Format  = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width  = qrSize,
                Height = qrSize,
                Margin = 1
            }
        };

        Color32[] pixels = writer.Write(text);
        Texture2D tex    = new Texture2D(qrSize, qrSize);
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
