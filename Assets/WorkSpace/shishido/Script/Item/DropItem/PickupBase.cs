using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using static GameConst;

public abstract class PickupBase : NetworkBehaviour, IPickupable
{
    protected GameObject appearanceOBJ;

    // Ammo
    public virtual void Initialize(AmmoType type, int _amount) { }
    //  BildingMat
    public virtual void Initialize(BildingMatType type, int _amount) { }
    //  Item
    public virtual void Initialize(ItemData data, ItemState state) { }

    public Transform GetTransform() { return transform; }

    // 同じアイテムへの取得要求重複そしフラグ
    private bool isBeingPickedUp;

    public void RequestPickup(PlayerManager character)
    {
        if (character == null)
            return;

        if (IsServerInitialized)
        {
            TryPickupOnServer(character);
        }
        else if (IsClientInitialized)
        {
            RequestPickupServerRpc(character);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(
        PlayerManager character,
        NetworkConnection sender = null)
    {
        if (character == null || sender == null)
            return;

        // 要求したクライアントが、このキャラクターの所有者か確認
        if (character.Owner != sender)
        {
            GameDebug.Warning(
                "[PickupBase] 要求者とキャラクターの所有者が一致しません");
            return;
        }

        TryPickupOnServer(character);
    }

    [Server]
    private void TryPickupOnServer(PlayerManager character)
    {
        if (character == null)
        {
            GameDebug.Warning("[Pickup] character が null");
            return;
        }

        if (!CheckCondition())
        {
            GameDebug.Warning("[Pickup] CheckCondition に失敗");
            return;
        }

        GameDebug.Log(
            $"[Pickup] 取得処理開始: " +
            $"Player={character.name}, " +
            $"IsServer={IsServerInitialized}");

        bool success = AddInventory(character);

        GameDebug.Log(
            $"[Pickup] AddInventory 結果: {success}");

        if (!success)
            return;

        GameDebug.Log("[Pickup] 追加成功。Despawnを実行");

        Despawn();
    }

    /// <summary>
    /// Despawn前の状態チェック
    /// </summary>
    private bool CheckCondition()
    {
        // Server側で初期化されているか
        if (!IsServerInitialized)
        {
            GameDebug.Warning("[Despawn] Serverとして初期化されていません");
            return false;
        }

        // NetworkObjectがSpawn済みか
        if (NetworkObject == null || !NetworkObject.IsSpawned)
        {
            GameDebug.Warning("[Despawn] NetworkObjectが存在しないか、Spawnされていません");
            return false;
        }

        return true;
    }

    public abstract bool AddInventory(PlayerManager character);

    #region 見た目の処理

    private float height;
    private float speed;
    private Vector3 startPosition;


    private void Start()
    {
        startPosition = transform.position;
        height = ITEM_MOVE_HEIGHT;
        speed = ITEM_MOVE_SPEED;
        SetAppearance();
    }

    private void Update()
    {
        AppearanceUpdate();
    }

    public virtual void SetAppearance() { }

    /// <summary>
    ///  見た目の更新処理
    /// </summary>
    private void AppearanceUpdate()
    {
        //  回転処理
        gameObject.transform.Rotate(new Vector3(0, ITEM_RORATE_SPEED, 0));

        //  上下運動処理
        float y = Mathf.Sin(Time.time * speed) * height;
        transform.position = startPosition + new Vector3(0f, y, 0f);
    }


    #endregion
}
