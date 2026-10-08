using FishNet.Connection;
using System.Runtime.CompilerServices;

public struct ParticipantData
{
    public int PlayerId;
    public bool IsHost;
    public string PlayerName;

    public ParticipantData(int _playerId, bool _isHost ,string _playerName)
    {
        PlayerId = _playerId;
        IsHost = _isHost;
        PlayerName = _playerName;
    }
}   
