using FishNet;
using FishNet.Managing;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SceneDefaultItemSpawner : NetworkBehaviour
{
    public NetworkManager _networkManager;

    [Header("---ドロップオブジェクト---")]
    [SerializeField] public NetworkObject AmmodropObj;
    [SerializeField] public NetworkObject MaterialdropObj;
    [SerializeField] public NetworkObject ItemdropObj;

    [Header("---ドロップデータ---")]
    [SerializeField]
    public List<Transform> spawnItemPointList;

    [SerializeField]
    public List<ItemData> spawnItemList;

    private void Awake()
    {
    }

    public override void OnStartServer()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        SpawneItem();
    }

    public override void OnStopServer()
    {
    }

    private void SpawneItem()
    {
#if false

        if ((spawnItemList == null || spawnItemList.Count <= 0) ||
            (spawnItemPointList == null || spawnItemPointList.Count <= 0))
            return;


        int loopCount = 0;
        if (spawnItemList.Count < spawnItemPointList.Count)
            loopCount = spawnItemList.Count;
        else
            loopCount = spawnItemPointList.Count;

        for (int i = 0; i < loopCount; i++)
        {
            NetworkObject nob = _networkManager.GetPooledInstantiated(
                ItemdropObj,
                spawnItemPointList[i].position,
                spawnItemPointList[i].rotation,
                true);

            if (nob != null)
            {
                Spawn(nob);
                nob.GetComponent<ItemPickup>()
                    .Initialize(spawnItemList[i], null);
            }
        }
#endif

    }

}
