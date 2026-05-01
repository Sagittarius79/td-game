using UnityEngine;
using TMPro;

/// <summary>
/// Lebegő sebzés szám – megjelenik a találat helyén, felszáll és eltűnik.
/// Típusonként (fizikai, mágikus, kritikus, armor) külön stílus állítható be
/// az Inspectorban a prefabon.
/// </summary>
public class FloatingDamageText : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  STÍLUS OSZTÁLY – per-típus beállítások
    // ══════════════════════════════════════════════════════

    [System.Serializable]
    public class DamageTextStyle
    {
        [Tooltip("Szöveg mérete")]
        public float fontSize = 4f;

        [Tooltip("Eltolás a spawn pozícióhoz képest (X = vízszintes, Y = függőleges)")]
        public Vector3 spawnOffset = new Vector3(0f, -1f, 0f);

        [Tooltip("Felszállási sebesség (egység/s)")]
        public float riseSpeed = 1.2f;

        [Tooltip("Mennyi ideig látsszon (másodperc)")]
        public float lifetime = 1f;

        [Tooltip("Szín")]
        public Color color = Color.white;
    }

    // ══════════════════════════════════════════════════════
    //  INSPECTOR – típusonkénti stílusok
    // ══════════════════════════════════════════════════════

    [Header("Stílusok típusonként")]
    public DamageTextStyle physicalStyle = new DamageTextStyle
    {
        fontSize    = 4f,
        spawnOffset = new Vector3(0f, -1f, 0f),
        riseSpeed   = 1.2f,
        lifetime    = 1f,
        color       = Color.white
    };

    public DamageTextStyle magicStyle = new DamageTextStyle
    {
        fontSize    = 4f,
        spawnOffset = new Vector3(0.3f, -0.8f, 0f),
        riseSpeed   = 1.4f,
        lifetime    = 1f,
        color       = new Color(0.6f, 0.3f, 1f)   // lila
    };

    public DamageTextStyle critStyle = new DamageTextStyle
    {
        fontSize    = 6f,
        spawnOffset = new Vector3(0f, -0.6f, 0f),
        riseSpeed   = 1.8f,
        lifetime    = 1.2f,
        color       = new Color(1f, 0.4f, 0f)      // narancs
    };

    public DamageTextStyle fireStyle = new DamageTextStyle
    {
        fontSize    = 4f,
        spawnOffset = new Vector3(0.2f, -0.9f, 0f),
        riseSpeed   = 1.3f,
        lifetime    = 1f,
        color       = new Color(1f, 0.25f, 0f)      // tűznarancs
    };

    public DamageTextStyle armorReduceStyle = new DamageTextStyle
    {
        fontSize    = 3f,
        spawnOffset = new Vector3(-0.3f, -1.2f, 0f),
        riseSpeed   = 0.9f,
        lifetime    = 1f,
        color       = new Color(0.4f, 0.8f, 1f)    // világoskék
    };

    [Header("Sorting")]
    [Tooltip("Ugyanaz legyen mint a szörnyek sorting layer-e")]
    public string sortingLayerName = "Default";

    // ══════════════════════════════════════════════════════
    //  BELSŐ ÁLLAPOT
    // ══════════════════════════════════════════════════════

    private TextMeshPro _tmp;
    private MeshRenderer _meshRenderer;
    private float _timer    = 0f;
    private Color _startColor;
    private float _riseSpeed;
    private float _lifetime;

    // ══════════════════════════════════════════════════════
    //  ÉLETCIKLUS
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        _tmp          = GetComponent<TextMeshPro>();
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    void LateUpdate()
    {
        if (_meshRenderer != null)
        {
            _meshRenderer.sortingLayerName = sortingLayerName;
            _meshRenderer.sortingOrder     = Mathf.RoundToInt(-transform.position.y * 100f) + 10000;
        }
    }

    void Update()
    {
        if (_tmp == null) return;

        _timer += Time.deltaTime;

        transform.position += Vector3.up * _riseSpeed * Time.deltaTime;

        float alpha = Mathf.Lerp(1f, 0f, _timer / _lifetime);
        _tmp.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);

        if (_timer >= _lifetime)
            Destroy(gameObject);
    }

    // ══════════════════════════════════════════════════════
    //  INICIALIZÁLÁS
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Enemy.TakeDamage() hívja. A spawn pozíció az ellenség pozíciója –
    /// a stílus spawnOffset-je tolja el a végső helyre.
    /// </summary>
    public void Initialize(float damage, DamageType damageType, bool isCrit = false, float resist = 0f)
    {
        _tmp = GetComponent<TextMeshPro>();
        if (_tmp == null) return;

        DamageTextStyle style = isCrit                          ? critStyle
                              : damageType == DamageType.Magic  ? magicStyle
                              : damageType == DamageType.Fire   ? fireStyle
                              : physicalStyle;

        ApplyStyle(style);

        bool detailed = GameSettingsUI.DetailedNumbers;
        string dmgStr = detailed
            ? damage.ToString("0.#")
            : Mathf.RoundToInt(damage).ToString();

        if (detailed && resist > 0f)
            dmgStr += $" ({Mathf.RoundToInt(resist)} resist)";

        _tmp.text = dmgStr;
    }

    /// <summary>
    /// Enemy.ReduceArmor() hívja armor csökkentéskor.
    /// </summary>
    public void InitializeArmorReduction(int amount)
    {
        _tmp = GetComponent<TextMeshPro>();
        if (_tmp == null) return;

        ApplyStyle(armorReduceStyle);
        _tmp.text = $"-{amount} armor";
    }

    // ══════════════════════════════════════════════════════
    //  SEGÉD
    // ══════════════════════════════════════════════════════

    private void ApplyStyle(DamageTextStyle style)
    {
        _tmp.fontSize = style.fontSize;
        _startColor   = style.color;
        _riseSpeed    = style.riseSpeed;
        _lifetime     = style.lifetime;

        transform.position += style.spawnOffset;

        _tmp.color = _startColor;
    }
}
