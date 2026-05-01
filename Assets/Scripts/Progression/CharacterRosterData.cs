using System;
using System.Collections.Generic;

/// <summary>
/// Fiók szintű adat – tárolja a Google fiók adatait és a karakterek azonosítóit.
/// Fájlnév: roster.dat
/// </summary>
[Serializable]
public class CharacterRosterData
{
    public string userId      = "";
    public string displayName = "Névtelen";
    public string email       = "";

    public List<string> characterIds    = new List<string>();
    public string       activeCharacterId = "";

    public long   lastSavedUtc = 0;
    public string saveVersion  = "2";

    public bool HasCharacters => characterIds != null && characterIds.Count > 0;
}
