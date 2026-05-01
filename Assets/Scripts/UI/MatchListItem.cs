using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Egy meccs sor a lobby listában.
/// A prefab-on legalább egy Button és egy TextMeshProUGUI szükséges.
///
/// Inspector bekötések:
///   matchNameText    – meccs neve + host neve (pl. "Meccs – KomorI")
///   playerCountText  – "2/4"
///   joinButton       – "Csatlakozás" gomb
/// </summary>
public class MatchListItem : MonoBehaviour
{
    [Header("UI elemek")]
    public TextMeshProUGUI matchNameText;
    public TextMeshProUGUI playerCountText;
    public Button          joinButton;

    private string _matchId;
    private string _matchName;
    private Action<string, string> _onJoin;  // (matchId, matchName)

    public void Setup(MatchmakingClient.MatchInfoJson info, Action<string, string> onJoin)
    {
        _matchId   = info.match_id;
        _matchName = info.match_name;
        _onJoin    = onJoin;

        if (matchNameText   != null)
            matchNameText.text   = $"{info.match_name}  –  {info.host_name}";

        if (playerCountText != null)
            playerCountText.text = $"{info.current_players}/{info.max_players}";

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinClicked);
            joinButton.interactable = info.current_players < info.max_players;
        }
    }

    void OnJoinClicked()
    {
        _onJoin?.Invoke(_matchId, _matchName);
    }
}
