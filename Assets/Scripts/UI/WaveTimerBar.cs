using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  HULLÁM VISSZASZÁMLÁLÓ CSÍK
///  Képernyő tetején végigfutó, teljes szélességű csík.
///  Amikor elfogy → következő hullám indul → újratölt.
/// ═══════════════════════════════════════════════════════
///
/// Unity beállítás:
///   1. Canvas gyerekébe hozz létre: UI → Image  → neve: "WaveTimerBar"
///   2. Rect Transform:
///        Anchor: felső-bal sarok (top-left stretch vízszintesen)
///        Left=0, Right=0, Top=0, Height=12  (módosítható)
///   3. Image → Image Type: Filled, Fill Method: Horizontal, Fill Origin: Left
///   4. Ezt a scriptet húzd rá a WaveTimerBar objektumra
///   5. Fill Image mezőbe kösd be magát a WaveTimerBar Image komponensét
/// </summary>
public class WaveTimerBar : MonoBehaviour
{
    [Header("Képek")]
    [Tooltip("Az Image komponens Image Type=Filled, Fill Method=Horizontal beállítással")]
    public Image fillImage;

    [Header("Színek")]
    [Tooltip("Csík színe amikor sok idő van hátra")]
    public Color colorFull  = new Color(0.2f, 0.8f, 0.2f, 1f);   // zöld

    [Tooltip("Csík színe amikor kevés idő van hátra")]
    public Color colorEmpty = new Color(0.9f, 0.2f, 0.2f, 1f);   // piros

    [Tooltip("Ennyi másodperc alatt vált pirosra (0 = nincs színváltás)")]
    public float dangerThreshold = 3f;

    void Update()
    {
        if (fillImage == null || WaveManager.Instance == null) return;

        float interval  = WaveManager.Instance.waveInterval;
        float countdown = WaveManager.Instance.NextWaveCountdown;

        if (interval <= 0f)
        {
            fillImage.fillAmount = 0f;
            return;
        }

        // fillAmount: 1 = tele (sok idő van), 0 = üres (indul a hullám)
        float fill = Mathf.Clamp01(countdown / interval);
        fillImage.fillAmount = fill;

        // Színváltás: azonnal piros ha eléri a küszöböt
        fillImage.color = (dangerThreshold > 0f && countdown <= dangerThreshold)
            ? colorEmpty
            : colorFull;
    }
}
