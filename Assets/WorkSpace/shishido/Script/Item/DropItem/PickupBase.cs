using FishNet.Object;
using UnityEngine;
using static GameConst;

public abstract class PickupBase : NetworkBehaviour, IPickupable
{
    // Ammo
    public virtual void Initialize(AmmoType type, int _amount) { }
    //  BildingMat
    public virtual void Initialize(BildingMatType type, int _amount) { }
    //  Item
    public virtual void Initialize(ItemData data, ItemState state) { }


    public void RequestPickup(PlayerManager character)
    {
        GameDebug.Log($"{gameObject.name}の[RequestPickup]が呼ばれました");
        Pickup(character);
    }

    public Transform GetTransform() { return transform; }


    public void Pickup(PlayerManager character)
    {
        //  各アイテムのインベントリ追加
        AddInventory(character);

        if (IsServerInitialized)
        {
            DespawnInternal(character);
        }
        else if (IsClientInitialized)
        {
            DespawnServerRpc(character);
        }
    }
    public abstract void AddInventory(PlayerManager character);

    [ServerRpc(RequireOwnership = false)]
    private void DespawnServerRpc(PlayerManager character)
    {
        DespawnInternal(character);
    }

    [Server]
    private void DespawnInternal(PlayerManager character)
    {
        if (!CheckDespawnCondition())
            return;
        // Despawn
        Despawn();

    }

    /// <summary>
    /// Despawn前の状態チェック
    /// </summary>
    private bool CheckDespawnCondition()
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
