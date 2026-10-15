using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

// 完成Prefabのルートに編集時に追加する。実行時AddComponentしない。
public class NetworkBuildPiece : NetworkBehaviour
{
    public LayerMask groundLayer;
    private BuildingSystem.BuildType kind;
    private static readonly HashSet<NetworkBuildPiece> serverPieces =
        new HashSet<NetworkBuildPiece>();

    public void Configure(BuildingSystem.BuildType type, LayerMask ground)
    {
        kind = type;
        groundLayer = ground;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        serverPieces.Add(this);
        BuildSupport.Attach(gameObject, groundLayer, null);
    }

    public override void OnStopServer()
    {
        serverPieces.Remove(this);
        BuildSupport support = GetComponent<BuildSupport>();
        if (support != null) BuildSupportManager.Unregister(support);
        base.OnStopServer();
    }

    public static bool Occupied(
        BuildingSystem.BuildType type, Vector3 position, Quaternion rotation)
    {
        serverPieces.RemoveWhere(p => p == null || !p.IsSpawned);
        foreach (NetworkBuildPiece piece in serverPieces)
        {
            if (piece.kind != type ||
                (piece.transform.position - position).sqrMagnitude > 0.0001f)
                continue;
            if (type == BuildingSystem.BuildType.Floor ||
                type == BuildingSystem.BuildType.Cone)
                return true;

            float angle = Mathf.Abs(Mathf.DeltaAngle(
                piece.transform.eulerAngles.y, rotation.eulerAngles.y));
            if (angle < 1f ||
                (type == BuildingSystem.BuildType.Wall && Mathf.Abs(angle - 180f) < 1f))
                return true;
        }
        return false;
    }

    // 耐久値の処理がサーバーでHP=0と判定したときに呼ぶ。
    public void DestroyOnServer()
    {
        if (!IsServerInitialized || !IsSpawned) return;
        Despawn(gameObject, DespawnType.Destroy);
    }

    [ContextMenu("サーバーで削除（崩壊テスト）")]
    private void TestDestroy()
    {
        if (Application.isPlaying) DestroyOnServer();
    }
}
