using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
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



    public abstract bool AddInventory(PlayerManager character);

    protected readonly SyncVar<ItemData> syncItemData = new SyncVar<ItemData>();
    protected readonly SyncVar<ItemState> syncItemState = new SyncVar<ItemState>();


    #region 見た目の処理

    private float height;
    private float speed;
    private Vector3 startPosition;


    private void Start()
    {
        startPosition = transform.position;
        height = ITEM_MOVE_HEIGHT;
        speed = ITEM_MOVE_SPEED;
        //SetAppearance();
    }

    private void Update()
    {
        AppearanceUpdate();
    }

    public virtual void SetAppearance() { }

    public void RequestPickup(PlayerManager character)
    {
        if (MatchManager.Instance != null)
            MatchManager.Instance.RequestPickup(character,this);
    }

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

    #region Setter

    protected void SetSyncItemData(ItemData _data)
    {
        if (IsServerInitialized)
            SetSyncItemDataServer(_data);
        else
            SetSyncItemDataServerRpc(_data);
    }

    protected void SetSyncItemState(ItemState _state)
    {
        if (IsServerInitialized)
            SetSyncItemStateServer(_state);
        else
            SetSyncItemStateServerRpc(_state);
    }

    #endregion

    #region Server関連

    [ServerRpc]
    private void SetSyncItemDataServerRpc(ItemData _data)
    {
        SetSyncItemDataServer(_data);
    }
    [Server]
    private void SetSyncItemDataServer(ItemData _data)
    {
        syncItemData.Value = _data;
    }

    [ServerRpc]
    private void SetSyncItemStateServerRpc(ItemState _state)
    {
        SetSyncItemStateServer(_state);
    }
    [Server]
    private void SetSyncItemStateServer(ItemState _state)
    {
        syncItemState.Value = _state;
    }


    #endregion
}
