using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : NetworkBehaviour
{
    private List<NetworkConnection> matchPlayers
    = new List<NetworkConnection>();


    public void StartBattle()
    {
        if (!IsServerInitialized)
            return;

        SceneLoadData sceneLoadData =
            new SceneLoadData("BattleScene");

        InstanceFinder.SceneManager.LoadConnectionScenes(
            sceneLoadData
        );
    }

    public void StartMatch(
        NetworkConnection playerA,
        NetworkConnection playerB)
    {
        if (!IsServerInitialized)
            return;

        matchPlayers.Clear();

        matchPlayers.Add(playerA);
        matchPlayers.Add(playerB);

        SceneLoadData loadData =
            new SceneLoadData("Battle");

        //InstanceFinder.SceneManager.LoadConnectionScenes(
        //    matchPlayers,
        //    loadData
        //);
    }
}