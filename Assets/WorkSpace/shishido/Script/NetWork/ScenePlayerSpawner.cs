using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class ScenePlayerSpawner : NetworkBehaviour
{
    public NetworkManager _networkManager;

    [SerializeField] private NetworkObject _playerPrefab;

    public readonly SyncList<NetworkObject> _playerOBJList
        = new SyncList<NetworkObject>();
    private readonly SyncList<NetworkConnection> _playerList
        = new SyncList<NetworkConnection>();

    [SerializeField] public List<NetworkObject> _viewplayerOBJList;
    [SerializeField] public List<NetworkConnection> _viewplayerList;

    /// <summary>
    /// SceneManager を通じてグローバルシーンが指定されていない場合、アクティブなシーンにプレイヤーを追加するように設定します
    /// </summary>
    [Tooltip("SceneManager を通じてグローバルシーンが指定されていない場合、アクティブなシーンにプレイヤーを追加するように設定します")]
    [SerializeField]
    private bool _addToDefaultScene = true;

    [SerializeField]
    [Tooltip("プレイヤーが出現する可能性のあるエリア")]
    public Transform[] Spawns = new Transform[0];
    private int _nextSpawn;

    private void Awake()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        _viewplayerList = new List<NetworkConnection>();
    }

    public override void OnStartServer()
    {
        SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        _playerOBJList.OnChange += OnChangeOBJList;
        _playerList.OnChange += OnChangeList;
    }

    public override void OnStopServer()
    {
        if (SceneManager != null)
            SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;

        _playerOBJList.OnChange -= OnChangeOBJList;
        _playerList.OnChange -= OnChangeList;
    }

    private void OnChangeOBJList(SyncListOperation op, int index, NetworkObject oldItem, NetworkObject newItem, bool asServer)
    {
        if (_viewplayerOBJList == null ||
            _viewplayerOBJList.Contains(newItem) || newItem == null)
            return;

        _viewplayerOBJList.Add(newItem);
    }

    private void OnChangeList(SyncListOperation op, int index, NetworkConnection oldItem, NetworkConnection newItem, bool asServer)
    {
        if (_viewplayerList == null ||
            _viewplayerList.Contains(newItem) || newItem == null)
            return;

        _viewplayerList.Add(newItem);
    }

    private void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
    {
        // このメソッドは、クライアントがすべての開始シーンを読み込んだときに呼び出されるため、
        // プレイヤーをスポーンする前に、クライアントが実際にこのシーンにいるかどうかを確認する必要があります。
        // Observers.Contains を使用して、この接続がこのオブジェクトを監視しているか、
        // つまりこのシーン内にあるかを確認します。あるいは、次のようにして、接続がこの
        // シーンを読み込んでいるかどうかを確認することも可能です：connection.Scenes.Contains(gameObject.scene)        if (asServer && Observers.Contains(connection))
        SpawnPlayer(connection);
    }

    /// <summary>
    /// プレイヤースポーントリガー
    /// ※クライアントがこのオブジェクトをスポーンしようとする際に、
    /// 　サーバー側で実行される。
    /// </summary>
    /// <param name="connection"></param>
    public override void OnSpawnServer(NetworkConnection connection)
    {
        if (connection.LoadedStartScenes(true))
            SpawnPlayer(connection);
    }

    /// <summary>
    /// playerPrefabの生成
    /// </summary>
    /// <param name="connection"></param>
    public void SpawnPlayer(NetworkConnection connection)
    {
        Vector3 position;
        Quaternion rotation;

        SetSpawn(_playerPrefab.transform, out position, out rotation);

        //  FishNetのプーリングシステムを使用した生成
        NetworkObject obj = NetworkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);

        //グローバルシーンが存在しない場合
        if (_addToDefaultScene)
            _networkManager.SceneManager.AddOwnerToDefaultScene(obj);

        Spawn(obj, connection, gameObject.scene);

        _playerOBJList.Add(obj);
        _playerList.Add(connection);
    }

    public void RespawnPlayer(NetworkConnection connection)
    {
        //  FishNetのプーリングシステムを使用した生成
        NetworkObject obj = NetworkManager.GetPooledInstantiated(_playerPrefab, asServer: true);

        Spawn(obj, connection);
    }

    /// <summary>
    /// スポーン位置と回転を設定
    /// </summary>
    /// <param name = "pos"></param>
    /// <param name = "rot"></param>
    private void SetSpawn(Transform prefab, out Vector3 pos, out Quaternion rot)
    {
        // No spawns specified.
        if (Spawns.Length == 0)
        {
            SetSpawnUsingPrefab(prefab, out pos, out rot);
            return;
        }

        Transform result = Spawns[_nextSpawn];
        if (result == null)
        {
            SetSpawnUsingPrefab(prefab, out pos, out rot);
        }
        else
        {
            pos = result.position;
            rot = result.rotation;
        }

        // Increase next spawn and reset if needed.
        _nextSpawn++;
        if (_nextSpawn >= Spawns.Length)
            _nextSpawn = 0;
    }

    /// <summary>
    /// セットは、プレハブの値を使用して生成
    /// </summary>
    /// <param name = "prefab"></param>
    /// <param name = "pos"></param>
    /// <param name = "rot"></param>
    private void SetSpawnUsingPrefab(Transform prefab, out Vector3 pos, out Quaternion rot)
    {
        pos = prefab.position;
        rot = prefab.rotation;
    }
}
