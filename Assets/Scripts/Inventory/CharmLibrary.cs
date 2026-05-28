using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Az összes Charm definíciója egyetlen asset-ben.
/// Létrehozás: Assets → Create → TD → Charm Library
/// </summary>
[CreateAssetMenu(fileName = "CharmLibrary", menuName = "TD/Charm Library")]
public class CharmLibrary : ScriptableObject
{
    public List<CharmDefinition> charms = new List<CharmDefinition>();
}
