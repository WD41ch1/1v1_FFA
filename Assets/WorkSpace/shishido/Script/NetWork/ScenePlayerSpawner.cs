using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

public class ScenePlayerSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject _playerPrefab;

    public override void OnStartServer()
    {
        SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
    }

    public override void OnStopServer()
    {
        if (SceneManager != null)
            SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
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
    private void SpawnPlayer(NetworkConnection connection)
    {
        //  FishNetのプーリングシステムを使用した生成
        NetworkObject obj = NetworkManager.GetPooledInstantiated(_playerPrefab, asServer: true);
        Spawn(obj, connection, gameObject.scene);
    }
}
