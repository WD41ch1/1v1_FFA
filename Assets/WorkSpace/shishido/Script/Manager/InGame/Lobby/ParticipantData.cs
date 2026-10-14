using FishNet.Connection;
using System.Runtime.CompilerServices;

public struct ParticipantData
{
    public string PlayerName;
    public int PlayerId;
    public bool IsHost;
    public bool IsReady;

    public ParticipantData(
        string _playerName, 
        int _playerId, 
        bool _isHost, 
        bool _ready = false)
    {
        PlayerName = _playerName;
        PlayerId = _playerId;
        IsHost = _isHost;
        IsReady = _ready;
    }

    public void SetReady(bool _ready)
    {
        IsReady = _ready;
    }

}   
