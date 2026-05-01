using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ═══════════════════════════════════════════════════════
///  AURA RENDSZER
///  Bizonyos szörnyek képességet adnak a közelükben lévő
///  többi szörnynek, amíg a közelükben vannak.
///
///  Működés:
///   - Minden frame-ben megkeresi a hatótávon belüli szörnyeket
///   - Alkalmazza a buff-ot (sebesség, armor, stb.)
///   - Amikor a szörny kimegy a hatótávból (vagy az aura-adó meghal),
///     a buff automatikusan megszűnik
///
///  Bővítés új aura típussal:
///   - AuraType enum-ba új érték
///   - ApplyBuff() / RemoveBuff() metódusokba az új logika
///
///  Unity beállítás:
///   - Wolf prefabra rakni ezt a scriptet
///   - Aura Type: Speed
///   - Aura Radius: 2
///   - Speed Bonus Percent: 30
/// ═══════════════════════════════════════════════════════
/// </summary>
public class AuraEffect : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  AURA TÍPUSOK
    // ══════════════════════════════════════════════════════

    public enum AuraType
    {
        Speed,      // sebesség növelés
        Armor,      // armor növelés
        MagicResist // mágikus ellenállás növelés
    }

    // ══════════════════════════════════════════════════════
    //  INSPECTOR MEZŐK
    // ══════════════════════════════════════════════════════

    [Header("Aura beállítások")]
    [Tooltip("Milyen típusú buff-ot ad a közelben lévő szörnyeknek")]
    public AuraType auraType = AuraType.Speed;

    [Tooltip("Mekkora sugarú körben hat az aura (world unit)")]
    public float auraRadius = 2f;

    [Header("Sebesség aura")]
    [Tooltip("%-os sebesség növelés (pl. 30 = +30%)")]
    public float speedBonusPercent = 30f;

    [Header("Armor aura")]
    [Tooltip("Hozzáadott armor érték")]
    public float armorBonus = 2f;

    [Header("MagicResist aura")]
    [Tooltip("Hozzáadott mágikus ellenállás")]
    public float magicResistBonus = 2f;

    [Header("Ellipszis lapítás")]
    [Tooltip("Egyezzen meg az AuraVisual Vertical Scale értékével! " +
             "Így a detektálás pontosan egyezik a látható ellipszissel.")]
    public float verticalScale = 0.5f;

    [Header("Frissítési sebesség")]
    [Tooltip("Hány másodpercenként frissüljön az aura (0.1 = 10x/mp)")]
    public float updateInterval = 0.1f;

    // ══════════════════════════════════════════════════════
    //  BELSŐ ÁLLAPOT
    // ══════════════════════════════════════════════════════

    // Jelenleg buffolt szörnyek listája
    private HashSet<Enemy> buffedEnemies = new HashSet<Enemy>();
    private float updateTimer = 0f;
    private Enemy selfEnemy;  // saját Enemy komponens (magát ne buffolja)

    // ══════════════════════════════════════════════════════
    //  INICIALIZÁLÁS
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        selfEnemy = GetComponent<Enemy>();
    }

    // ══════════════════════════════════════════════════════
    //  UPDATE
    // ══════════════════════════════════════════════════════

    void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        RefreshAura();
    }

    void OnDestroy()
    {
        // Szörny meghal → minden buff megszűnik
        RemoveAllBuffs();
    }

    // ══════════════════════════════════════════════════════
    //  AURA FRISSÍTÉS
    // ══════════════════════════════════════════════════════

    void RefreshAura()
    {
        HashSet<Enemy> inRange = new HashSet<Enemy>();

        // Hatótávon belüli szörnyek összegyűjtése
        foreach (var enemy in Enemy.AllEnemies)
        {
            if (enemy == selfEnemy) continue;       // magát ne buffolja
            if (enemy.IsDead) continue;

            // Ellipszis alapú távolság vizsgálat – egyezik a vizuális jelzővel
            // dx és dy külön skálázva, majd normalizálva → ellipszis alakú detektálás
            // a 0.55 érték kisebbitésével az aura kisebb lesz a rajzolt körhöz arányitva
            Vector3 diff = enemy.transform.position - transform.position;
            float dx = diff.x / auraRadius;
            float dy = diff.y / (auraRadius * verticalScale);
            if (dx * dx + dy * dy <= 0.45f)
                inRange.Add(enemy);
        }

        // Új szörnyek: buff alkalmazása
        foreach (var enemy in inRange)
        {
            if (!buffedEnemies.Contains(enemy))
            {
                ApplyBuff(enemy);
                buffedEnemies.Add(enemy);
            }
        }

        // Kilépett szörnyek: buff eltávolítása
        List<Enemy> toRemove = new List<Enemy>();
        foreach (var enemy in buffedEnemies)
        {
            if (enemy == null || enemy.IsDead || !inRange.Contains(enemy))
                toRemove.Add(enemy);
        }
        foreach (var enemy in toRemove)
        {
            if (enemy != null && !enemy.IsDead)
                RemoveBuff(enemy);
            buffedEnemies.Remove(enemy);
        }
    }

    void RemoveAllBuffs()
    {
        foreach (var enemy in buffedEnemies)
        {
            if (enemy != null && !enemy.IsDead)
                RemoveBuff(enemy);
        }
        buffedEnemies.Clear();
    }

    // ══════════════════════════════════════════════════════
    //  BUFF ALKALMAZÁS / ELTÁVOLÍTÁS
    // ══════════════════════════════════════════════════════

    void ApplyBuff(Enemy enemy)
    {
        switch (auraType)
        {
            case AuraType.Speed:
                enemy.moveSpeed *= 1f + speedBonusPercent / 100f;
                break;
            case AuraType.Armor:
                enemy.armor += armorBonus;
                break;
            case AuraType.MagicResist:
                enemy.magicResist += magicResistBonus;
                break;
        }
    }

    void RemoveBuff(Enemy enemy)
    {
        switch (auraType)
        {
            case AuraType.Speed:
                enemy.moveSpeed /= 1f + speedBonusPercent / 100f;
                break;
            case AuraType.Armor:
                enemy.armor -= armorBonus;
                break;
            case AuraType.MagicResist:
                enemy.magicResist -= magicResistBonus;
                break;
        }
    }

    // ══════════════════════════════════════════════════════
    //  DEBUG VIZUALIZÁCIÓ
    //  Szerkesztőben látható kör az aura sugarának jelzésére
    // ══════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}
