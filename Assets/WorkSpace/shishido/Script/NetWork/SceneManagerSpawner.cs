using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SceneManagerSpawner : NetworkBehaviour
{
    public NetworkManager _networkManager;

    [SerializeField]
    private List<nobManagerBase> generateManagerList;

    private List<NetworkObject> managers;

    private void Awake()
    {
    }

    public override void OnStartServer()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        if (managers == null)
            managers = new List<NetworkObject>();

        GenerateManager();
    }

    public override void OnStopServer()
    {
        DespawnMangers();
    }

    private void GenerateManager()
    {
        if (generateManagerList == null || generateManagerList.Count <= 0)
            return;

        foreach (nobManagerBase manager in generateManagerList)
        {
            NetworkObject nob = Instantiate(manager.NetworkObject, transform);

            //  すでに同じものがあったら
            if (nob != null && !managers.Contains(nob))
            {
                //  生成したManagerを格納
                managers.Add(nob);

                Spawn(nob);
            }
        }
    }

    private void DespawnMangers()
    {
        if (managers == null || managers.Count <= 0)
            return;

        foreach (NetworkObject nobs in managers)
        {
            if (nobs != null)
                Despawn(nobs);
        }
    }

}
