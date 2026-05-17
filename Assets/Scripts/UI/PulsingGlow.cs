using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A gomb saját Image-ének alpha-ját pulzáltatja lassan.
/// Rakd közvetlenül a nyíl gomb GameObject-re.
/// </summary>
public class PulsingGlow : MonoBehaviour
{
    [Header("Pulzálás")]
    public float minAlpha = 0.3f;
    public float maxAlpha = 1f;
    [Tooltip("Másodpercek egy teljes pulzáláshoz (kisebb = gyorsabb)")]
    public float speed    = 1.5f;

    private Image  _image;
    private Color  _baseColor;
    private float  _time;

    void Awake()
    {
        _image     = GetComponent<Image>();
        if (_image != null) _baseColor = _image.color;
    }

    void OnEnable()
    {
        _time = 0f;
    }

    void Update()
    {
        if (_image == null) return;

        _time += Time.deltaTime;
        float t     = (Mathf.Sin(_time * Mathf.PI * 2f / speed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        _image.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
    }

    void OnDisable()
    {
        // Visszaállítja az eredeti színt ha a gomb elrejtődik
        if (_image != null)
            _image.color = _baseColor;
    }
}
