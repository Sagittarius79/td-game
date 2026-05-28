using UnityEngine;

/// <summary>
/// Statikus segédosztály – az aktuálisan equip-elt charmok effektjeit összesíti.
/// Bárhol használható a játék során (pl. Enemy.TakeDamage-ben),
/// így a charm hatások egy központi helyről kérdezhetők le.
/// </summary>
public static class CharmEffects
{
    /// <summary>
    /// Az összes charm hangeffekt hangereje (0–1).
    /// A CharmRegistry Inspector mezőjéből (charmSoundVolume) jön.
    /// </summary>
    public static float Volume =>
        CharmRegistry.Instance != null ? CharmRegistry.Instance.charmSoundVolume : 1f;

    /// <summary>
    /// Az equip slotban lévő charmok adott típusú effektjeinek összegét adja vissza.
    /// Üres equip vagy nincs ilyen effekt → 0.
    /// </summary>
    public static float GetEquippedTotal(CharmEffectType type)
    {
        if (type == CharmEffectType.None) return 0f;

        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return 0f;

        var charms = mgr.Data.charms;
        if (charms == null) return 0f;

        float total = 0f;
        for (int i = 0; i < charms.Count; i++)
        {
            var ci = charms[i];
            if (ci == null || ci.equipSlot < 0) continue;

            var def = CharmRegistry.Instance?.Get(ci.definitionId);
            if (def == null || def.effectType != type) continue;

            total += def.effectValue;
        }
        return total;
    }

    /// <summary>
    /// Véletlen 0–100 dobás az equip-elt charmok adott effektjének százalékos esélye alapján.
    /// Például InstantKillChanceMagic: ha 3% van összesen, 3% eséllyel ad true-t.
    /// </summary>
    public static bool RollPercentChance(CharmEffectType type)
    {
        float chance = GetEquippedTotal(type);
        if (chance <= 0f) return false;
        return Random.value * 100f < chance;
    }

    /// <summary>
    /// Lejátssza az adott típusú equip-elt charm hangját (ha be van állítva).
    /// Akkor hívd, amikor a charm hatása ténylegesen aktiválódik.
    /// </summary>
    public static void PlayEffectSound(CharmEffectType type)
    {
        var mgr = UserProgressManager.Instance;
        if (mgr == null || !mgr.HasCharacter) return;

        var charms = mgr.Data.charms;
        if (charms == null) return;

        for (int i = 0; i < charms.Count; i++)
        {
            var ci = charms[i];
            if (ci == null || ci.equipSlot < 0) continue;

            var def = CharmRegistry.Instance?.Get(ci.definitionId);
            if (def != null && def.effectType == type && def.effectSound != null)
            {
                AudioManager.Instance?.PlaySFX(def.effectSound, Volume);
                return; // csak egyszer, az első ilyen típusú charm hangja
            }
        }
    }
}
