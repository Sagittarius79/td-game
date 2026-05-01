using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MainMenu scene-be kerül. Szerver build esetén azonnal átugorja a login UI-t
/// és betölti a pvplobby scene-t, ahol a NetworkGameManager és
/// DedicatedServerBootstrap él.
///
/// Elhelyezés: MainMenu scene → üres GameObject → add component → ServerSceneBootstrap
/// </summary>
public class ServerSceneBootstrap : MonoBehaviour
{
#if UNITY_SERVER
    void Awake()
    {
        Debug.Log("[ServerBoot] Szerver build – MainMenu átugrása → pvplobby betöltése.");
        SceneManager.LoadScene("Scenes/pvplobby");
    }
#endif
}
