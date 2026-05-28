using System.Collections;
using UnityEngine;

/// <summary>
/// Rúna kő pickup – a goblin halála után jelenik meg a pályán.
/// Kattintásra aktiválja a buffot, majd eltűnik.
/// Ha N másodpercig nem nyomják meg, magától eltűnik.
/// </summary>
public class RunePickup : MonoBehaviour
{
    /// <summary>GoblinEventManager tölti ki spawnoláskor – melyik prefabból lett példányosítva.</summary>
    [HideInInspector] public GameObject sourcePrefab;

    [Tooltip("Hány másodpercig marad a pályán kattintás nélkül")]
    public float lifetime = 8f;

    [Tooltip("Lebegés amplitúdója (world unit)")]
    public float bobAmplitude = 0.08f;
    [Tooltip("Lebegés sebessége")]
    public float bobSpeed = 2f;

    public System.Action<RunePickup> OnCollected;

    private Vector3 _basePos;
    private bool _collected = false;

    void Start()
    {
        _basePos = transform.position;
        StartCoroutine(AutoExpire());
    }

    void Update()
    {
        // Kis lebegő animáció
        float y = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = _basePos + new Vector3(0f, y, 0f);
    }

    void OnMouseDown()
    {
        Collect();
    }

    public void Collect()
    {
        if (_collected) return;
        _collected = true;
        OnCollected?.Invoke(this);
        Destroy(gameObject);
    }

    IEnumerator AutoExpire()
    {
        yield return new WaitForSeconds(lifetime);
        if (!_collected)
            Destroy(gameObject);
    }
}
