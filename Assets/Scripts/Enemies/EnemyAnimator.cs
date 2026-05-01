using UnityEngine;

/// <summary>
/// Egyszerű sprite animáció – ciklikusan váltogatja a megadott képkockákat.
/// Tedd az Ork prefabra a SpriteRenderer mellé.
/// </summary>
public class EnemyAnimator : MonoBehaviour
{
    [Tooltip("A 4 animációs képkocka sorban (1→2→3→4→1→...)")]
    public Sprite[] frames;

    [Tooltip("Hány képkockát játsszon le másodpercenként")]
    public float fps = 8f;

    private SpriteRenderer sr;
    private float timer = 0f;
    private int currentFrame = 0;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;

        timer += Time.deltaTime;

        if (timer >= 1f / fps)
        {
            timer -= 1f / fps;
            currentFrame = (currentFrame + 1) % frames.Length;
            sr.sprite = frames[currentFrame];
        }
    }
}
