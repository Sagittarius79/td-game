using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  ANIMATOR EGYSZER LEJÁTSZÓS – automatikus megsemmisítés
///  Az Animator Controller animációját egyszer lejátssza,
///  majd megsemmisíti az objektumot.
///
///  Használat: Impact effect prefabokon (pl. TeleportHit)
///  Unity beállítás:
///   1. Hozz létre egy üres GameObject-et
///   2. Add hozzá: Animator komponens
///   3. Animator → Controller → húzd be a Teleport_hit.controller-t
///   4. Add hozzá ezt a scriptet (AnimatorOneShotDestroy)
///   5. Mentsd el prefabként
/// ═══════════════════════════════════════════════════════
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimatorOneShotDestroy : MonoBehaviour
{
    [Tooltip("Ha 0, automatikusan kiszámolja az animáció hosszát")]
    public float overrideDuration = 0f;

    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();

        float duration = overrideDuration;

        // Ha nincs felülírva, kiszámolja az animáció hosszát
        if (duration <= 0f)
        {
            var info = anim.GetCurrentAnimatorStateInfo(0);
            duration = info.length;

            // Ha még nem töltött be (0 hossz), egy kicsit várunk
            if (duration <= 0f)
                duration = 1f;
        }

        Destroy(gameObject, duration);
    }
}
