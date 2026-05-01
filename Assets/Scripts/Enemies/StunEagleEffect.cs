using UnityEngine;

/// <summary>
/// Sas animáció stun esetén – fentről lecsap a szörnyre, majd eltűnik.
/// Rakd a sas prefabra, a stunEffectPrefab mezőbe húzd be az Enemy Inspectorában.
/// </summary>
public class StunEagleEffect : MonoBehaviour
{
    [Header("Mozgás")]
    [Tooltip("Honnan induljon a sas (a cél felett ennyivel)")]
    public float startHeightOffset = 3f;

    [Tooltip("Lecsapás sebessége")]
    public float diveSpeed = 6f;

    [Tooltip("Ennyi ideig marad a célponton, mielőtt eltűnik")]
    public float holdDuration = 0.3f;

    [Tooltip("Felszállás sebessége a lecsapás után")]
    public float riseSpeed = 4f;

    [Tooltip("Ennyire száll fel mielőtt megsemmisül")]
    public float riseHeight = 2f;

    private Vector3 _targetPos;
    private Vector3 _startPos;
    private enum Phase { Diving, Holding, Rising }
    private Phase _phase = Phase.Diving;
    private float _holdTimer;
    private float _riseTarget;

    void Start()
    {
        _targetPos  = transform.position;                          // szörny pozíciója
        _startPos   = _targetPos + Vector3.up * startHeightOffset;
        _riseTarget = _targetPos.y + riseHeight;
        transform.position = _startPos;
    }

    void Update()
    {
        switch (_phase)
        {
            case Phase.Diving:
                transform.position = Vector3.MoveTowards(
                    transform.position, _targetPos, diveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _targetPos) < 0.05f)
                {
                    transform.position = _targetPos;
                    _holdTimer = holdDuration;
                    _phase     = Phase.Holding;
                }
                break;

            case Phase.Holding:
                _holdTimer -= Time.deltaTime;
                if (_holdTimer <= 0f)
                    _phase = Phase.Rising;
                break;

            case Phase.Rising:
                transform.position += Vector3.up * riseSpeed * Time.deltaTime;
                if (transform.position.y >= _riseTarget)
                    Destroy(gameObject);
                break;
        }
    }
}
