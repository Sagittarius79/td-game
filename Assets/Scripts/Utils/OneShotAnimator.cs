using UnityEngine;

/// <summary>
/// Egyszer lejátszódó sprite animáció – a végén megsemmisíti saját magát.
/// Használható robbanás, csapás, területi sebzés effekthez.
/// </summary>
public class OneShotAnimator : MonoBehaviour
{
    [Tooltip("A képkockák sorban (1→2→3→megsemmisül)")]
    public Sprite[] frames;

    [Tooltip("Hány képkockát játsszon le másodpercenként")]
    public float fps = 12f;

    private SpriteRenderer sr;
    private float timer = 0f;
    private int currentFrame = 0;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null && frames != null && frames.Length > 0)
            sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;

        timer += Time.deltaTime;

        if (timer >= 1f / fps)
        {
            timer -= 1f / fps;
            currentFrame++;

            // Utolsó képkocka után megsemmisül
            if (currentFrame >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            sr.sprite = frames[currentFrame];
        }
    }
}
