using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Betölti a CharmLibrary-t és id alapján visszaadja a definíciókat.
/// Tedd egy Manager GameObject-re a MainMenu scene-ben.
/// </summary>
public class CharmRegistry : MonoBehaviour
{
    public static CharmRegistry Instance { get; private set; }

    [Tooltip("A CharmLibrary asset (egyetlen fájl az összes charm-mal)")]
    public CharmLibrary library;

    [Header("Hang")]
    [Tooltip("Az összes charm hangeffekt hangereje (0 = néma, 1 = teljes)")]
    [Range(0f, 1f)]
    public float charmSoundVolume = 1f;

    private Dictionary<string, CharmDefinition> _lookup;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildLookup();
    }

    void BuildLookup()
    {
        _lookup = new Dictionary<string, CharmDefinition>();
        if (library == null) return;

        foreach (var charm in library.charms)
            if (charm != null && !string.IsNullOrEmpty(charm.id))
                _lookup[charm.id] = charm;
    }

    public CharmDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _lookup.TryGetValue(id, out var def);
        return def;
    }

    /// <summary>Véletlenszerű charmot ad vissza a library-ból. Null ha üres.</summary>
    public CharmDefinition GetRandom()
    {
        if (library == null || library.charms == null) return null;
        var valid = library.charms.FindAll(c => c != null && !string.IsNullOrEmpty(c.id));
        if (valid.Count == 0) return null;
        return valid[UnityEngine.Random.Range(0, valid.Count)];
    }

    /// <summary>Adott szintű charmok közül ad vissza egy véletlent. Null ha nincs ilyen szintű.</summary>
    public CharmDefinition GetRandomByLevel(int level)
    {
        if (library == null || library.charms == null) return null;
        var valid = library.charms.FindAll(c =>
            c != null && !string.IsNullOrEmpty(c.id) && c.level == level);
        if (valid.Count == 0) return null;
        return valid[UnityEngine.Random.Range(0, valid.Count)];
    }

    /// <summary>Az összes érvényes charmot visszaadja a library-ból.</summary>
    public List<CharmDefinition> GetAll()
    {
        if (library == null || library.charms == null) return new List<CharmDefinition>();
        return library.charms.FindAll(c => c != null && !string.IsNullOrEmpty(c.id));
    }

    /// <summary>
    /// Megkeresi az eggyel magasabb szintű, azonos típusú charmot.
    /// Null ha nincs (pl. már max szint, vagy nincs definiálva a következő szint).
    /// </summary>
    public CharmDefinition GetUpgrade(CharmEffectType type, int currentLevel)
    {
        if (library == null || library.charms == null) return null;
        return library.charms.Find(c =>
            c != null && !string.IsNullOrEmpty(c.id)
            && c.effectType == type
            && c.level == currentLevel + 1);
    }
}
