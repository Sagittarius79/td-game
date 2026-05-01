using UnityEngine;

/// <summary>
/// Egyszerű jelölő komponens – nem Tower-alapú épületekre (pl. GoldMine, OrkDen) rakható.
/// Ha isLvl2 = true, az épület dekorált cellára is lerakható.
/// </summary>
public class BuildingLevelTag : MonoBehaviour
{
    [Tooltip("Ha be van kapcsolva, ez egy Lvl2-es épület – dekorált cellára is lerakható.\n" +
             "Ha ki van kapcsolva (Lvl1), dekorált cellára nem lehet lerakni.")]
    public bool isLvl2 = false;
}
