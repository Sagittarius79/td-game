using UnityEngine;

/// <summary>
/// Rúna konfiguráció – ScriptableObject.
/// Egyszer állítsd be, aztán húzd rá az összes projektil prefab
/// altalanos_fejlesztesek komponensének "Rune Config" mezőjére.
/// Az alap értékek felülírják a prefabon lévő értékeket.
/// A bónusz értékek stackenként adódnak hozzá.
/// </summary>
[CreateAssetMenu(fileName = "RuneConfig", menuName = "TD/Rune Config")]
public class RuneConfig : ScriptableObject
{

    [Header("── Gyújtás ────────────────────────────────")]
    public GameObject gyujtasRunePrefab;
    public AudioClip gyujtasCollectSound;
    [Tooltip("Találatonkénti aktiválási esély (0–1, 1 = mindig)")]
    [Range(0f, 1f)]
    public float gyujtasEsely = 1f;
    [Tooltip("Közvetlen találati tűzsebzés")]
    public float gyujtasTalalatSebzes = 0f;
    [Tooltip("Alap DoT sebzés mp-enként")]
    public float gyujtasDoTAlap = 5f;
    [Tooltip("Alap égés időtartam (mp)")]
    public float gyujtasIdotartamAlap = 3f;
    [Space]
    [Tooltip("DoT sebzés növekedés rúnánként")]
    public float gyujtasDoTBonusz = 3f;
    [Tooltip("Időtartam növekedés rúnánként (mp)")]
    public float gyujtasIdotartamBonusz = 0.5f;

    [Header("── Stun ────────────────────────────────────")]
    public GameObject stunRunePrefab;
    public AudioClip stunCollectSound;
    public GameObject stunVfxPrefab;
    [Tooltip("Alap stun esély (0–1)")]
    [Range(0f, 1f)]
    public float stunEselyAlap = 0.25f;
    [Tooltip("Alap stun időtartam (mp)")]
    public float stunIdotartamAlap = 1.5f;
    [Space]
    [Tooltip("Esély növekedés rúnánként")]
    public float stunEselyBonusz = 0.10f;
    [Tooltip("Időtartam növekedés rúnánként (mp)")]
    public float stunIdotartamBonusz = 0.3f;

    [Header("── Multi Shot ──────────────────────────────")]
    public GameObject multiShotRunePrefab;
    public AudioClip multiShotCollectSound;
    [Tooltip("Találatonkénti aktiválási esély (0–1, 1 = mindig)")]
    [Range(0f, 1f)]
    public float multiShotEsely = 1f;
    [Tooltip("Alap lövedék mennyiség")]
    [Min(1)]
    public int multiShotMennyisegAlap = 2;
    [Space]
    [Tooltip("Lövedék mennyiség növekedés rúnánként")]
    public int multiShotMennyisegBonusz = 1;

    [Header("── Pattanás ────────────────────────────────")]
    public GameObject pattanasRunePrefab;
    public AudioClip pattanasCollectSound;
    [Tooltip("Alap pattanási esély (%)")]
    [Range(0f, 100f)]
    public float pattanasEselyAlap = 20f;
    [Tooltip("Maximum pattanási esély (%)")]
    [Range(0f, 100f)]
    public float pattanasMaxEsely = 80f;
    [Tooltip("Alap max pattanás szám")]
    [Min(1)]
    public int pattanasSzamAlap = 1;
    [Tooltip("Armor bónusz pattanási esélyhez")]
    public float pattanasArmorBonusz = 5f;
    [Tooltip("Keresési sugár (tile)")]
    public float pattanasSugar = 4f;
    [Space]
    [Tooltip("Max pattanás szám növekedés rúnánként")]
    public int pattanasSzamBonusz = 1;
    [Tooltip("Alap esély növekedés rúnánként (%)")]
    public float pattanasEselyBonusz = 10f;

    [Header("── Küldési kedvezmény rúna ──────────────────")]
    public GameObject sendDiscountRunePrefab;
    public AudioClip sendDiscountCollectSound;
    [Tooltip("Kedvezmény mértéke stackenként (0–1, pl. 0.10 = 10%)")]
    [Range(0f, 1f)]
    public float sendDiscountPerStack = 0.10f;
    [Tooltip("Maximum kedvezmény összesen (0–1)")]
    [Range(0f, 1f)]
    public float sendDiscountMax = 0.50f;

    [Header("── Méreg ───────────────────────────────────")]
    public GameObject meregRunePrefab;
    public AudioClip meregCollectSound;
    [Tooltip("Találatonkénti aktiválási esély (0–1, 1 = mindig)")]
    [Range(0f, 1f)]
    public float meregEsely = 1f;
    [Tooltip("Alap méreg sebzés mp-enként")]
    public float meregDoTAlap = 3f;
    [Tooltip("Alap stack időtartam (mp)")]
    public float meregIdotartamAlap = 4f;
    [Tooltip("Alap max stack szám")]
    public int meregMaxStackAlap = 5;
    [Space]
    [Tooltip("DoT sebzés növekedés rúnánként")]
    public float meregDoTBonusz = 2f;
    [Tooltip("Max stack növekedés rúnánként")]
    public int meregStackBonusz = 1;

    public AudioClip GetCollectSound(GameObject runePrefab)
    {
        if (runePrefab == null) return null;
        if (runePrefab == gyujtasRunePrefab)       return gyujtasCollectSound;
        if (runePrefab == stunRunePrefab)           return stunCollectSound;
        if (runePrefab == multiShotRunePrefab)      return multiShotCollectSound;
        if (runePrefab == pattanasRunePrefab)       return pattanasCollectSound;
        if (runePrefab == sendDiscountRunePrefab)   return sendDiscountCollectSound;
        if (runePrefab == meregRunePrefab)          return meregCollectSound;
        return null;
    }
}
