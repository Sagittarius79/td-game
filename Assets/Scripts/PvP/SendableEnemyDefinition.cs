using UnityEngine;

/// <summary>
/// Küldhető szörny definíció – ScriptableObject adateszköz.
///
/// Új szörny hozzáadásához a Projectben:
///   jobb klikk → Create → TD → Küldhető Szörny
/// Töltsd ki a mezőket, majd add hozzá a PvPSendPanel
/// sendableEnemies listájához az Inspectorban.
/// </summary>
[CreateAssetMenu(fileName = "NewSendableEnemy", menuName = "TD/Küldhető Szörny")]
public class SendableEnemyDefinition : ScriptableObject
{
    [Header("Alap adatok")]
    public string enemyName = "Ork";
    public Sprite icon;
    public int goldCost = 5;
    public GameObject prefab;

    [Header("Küldési darabszám")]
    [Tooltip("Ennyi példányt küld egyszerre – a hullámkor csoportosan, egymás után spawnolja őket")]
    public int sendCount = 1;

    [Header("Spawn késleltetés (csak erre a szörnyre)")]
    [Tooltip("Minimum várakozás két spawn között (mp)")]
    public float minSpawnDelay = 0.2f;
    [Tooltip("Maximum várakozás két spawn között (mp)")]
    public float maxSpawnDelay = 0.5f;

    [Header("Leírás (opcionális)")]
    [TextArea(2, 3)]
    public string description = "";
}
