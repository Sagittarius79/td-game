using System.Collections;
using UnityEngine;

/// <summary>
/// Boss spawn event – egy boss szörnyet spawnol, az event addig tart amíg
/// a boss él (vagy lejár az időlimit).
///
/// Használat:
///   1. Add hozzá komponensként az _EventManagers GameObject-re
///   2. Állítsd be a bossPrefab mezőt
///   3. Húzd be a BossEventManager › Boss Events[] listájába
///
/// Több BossSpawnEvent is lehet (különböző prefabokkal) – a BossEventManager
/// véletlenszerűen választ közülük.
/// </summary>
public class BossSpawnEvent : BossEventBase
{
    [Header("Boss szörny")]
    [Tooltip("A spawnolni kívánt boss prefab (Enemy komponenssel)")]
    public GameObject bossPrefab;

    [Header("HP skálázás")]
    [Tooltip("HP szorzó az alap prefab HP-hez képest (1 = változatlan)")]
    public float hpMultiplier = 2f;
    [Tooltip("HP növekedés hullámonként %-ban (0.1 = +10%/hullám). 0 = nincs skálázás)")]
    public float hpPerWavePercent = 0.1f;

    [Header("Spawn")]
    [Tooltip("Hány másodperccel az event indulása után jelenjen meg a boss")]
    public float spawnDelay = 1f;

    protected override IEnumerator OnActivate(float duration)
    {
        if (bossPrefab == null)
        {
            Debug.LogWarning($"[{eventName}] bossPrefab nincs beállítva!");
            yield return new WaitForSeconds(duration);
            yield break;
        }

        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) yield break;
        if (GridManager.Instance == null) yield break;

        // Spawn a pálya kezdőpontján
        Vector3 spawnPos = GridManager.Instance.GridToWorld(GridManager.Instance.SpawnCell);
        GameObject bossGO = Instantiate(bossPrefab, spawnPos, Quaternion.identity);

        var enemy = bossGO.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.enemyTypeName     = "Boss";
            enemy.countAsKill       = true;
            enemy.dealsCastleDamage = true;

            // HP skálázás: alap szorzó + hullám alapú növekedés
            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 1;
            float waveScale = hpPerWavePercent > 0f
                ? Mathf.Pow(1f + hpPerWavePercent, wave - 1)
                : 1f;
            enemy.ScaleHealth(hpMultiplier * waveScale);
        }

        // Az event addig aktív amíg a boss él vagy lejár az időlimit
        bool bossAlive = true;
        if (enemy != null)
            enemy.OnDied += _ => bossAlive = false;

        float elapsed = 0f;
        while (bossAlive && elapsed < duration)
        {
            // Boss elpusztulhatott a GO nélkül (pl. scene reload)
            if (bossGO == null) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Időlimit lejárt, de a boss még él → eltüntetjük
        if (bossAlive && bossGO != null)
        {
            Debug.Log($"[{eventName}] Időlimit lejárt, boss eltávolítva.");
            Destroy(bossGO);
        }
    }
}
