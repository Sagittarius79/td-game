// ShadowBlob.cs — húzd rá minden toronyra / ellenségre
using UnityEngine;


public class ShadowBlob : MonoBehaviour
{
    [SerializeField] private Sprite shadowSprite;     // sötét ellipszis png
    [SerializeField] private Vector2 offset = new(0f, -0.3f);
    [SerializeField] private Vector2 scale = new(1f, 0.4f);
    [SerializeField][Range(0, 1)] private float alpha = 0.35f;

    private GameObject _blob;

    void Awake()
    {
        _blob = new GameObject("Shadow");
        _blob.transform.SetParent(transform, false);
        _blob.transform.localPosition = new Vector3(offset.x, offset.y, 0.01f);
        _blob.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        var sr = _blob.AddComponent<SpriteRenderer>();
        sr.sprite = shadowSprite;
        sr.color = new Color(0, 0, 0, alpha);
        sr.sortingLayerName = "Enemies";
        sr.sortingOrder = -1;   // torony mögé kerüljön
    }
}