using UnityEngine;
using TMPro;

/// <summary>
/// A törmelék prefabra kerül. Kattintásra megmutatja hány kör van még hátra.
/// A szöveg automatikusan eltűnik 2 másodperc után, vagy következő kattintásra.
/// </summary>
public class RubbleClickable : MonoBehaviour
{
    [Tooltip("World-space TextMeshPro a prefabon belül")]
    public TextMeshPro label;
    [Tooltip("Hány másodpercig látszik a szöveg")]
    public float displayDuration = 2f;

    private Vector2Int _cell;
    private float _hideAt = -1f;

    public void Init(Vector2Int cell)
    {
        _cell = cell;
        if (label != null) label.gameObject.SetActive(false);
    }

    public void ShowInfo()
    {
        if (GridManager.Instance == null || label == null) return;

        int rounds = GridManager.Instance.GetRubbleRoundsLeft(_cell);
        label.text = rounds == 1 ? "1 round" : $"{rounds} rounds";
        label.gameObject.SetActive(true);
        _hideAt = Time.time + displayDuration;
    }

    void Update()
    {
        if (_hideAt > 0f && Time.time >= _hideAt)
        {
            if (label != null) label.gameObject.SetActive(false);
            _hideAt = -1f;
        }
    }
}
