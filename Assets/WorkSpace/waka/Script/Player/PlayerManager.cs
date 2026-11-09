using FishNet.Object;
using UnityEngine;
using static GameConst;
using static CheckReferenceUtility;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// Playerについているclassを管理するクラス
/// </summary>
public class PlayerManager : NetworkBehaviour
{
    #region SerializeField

    [SerializeField, Header("PlayerのLogを有効にするフラグ(Editorのみ操作可能)")]
    private bool isShowLog = true;

    [Header("Cameraの生成")]
    [SerializeField] private Camera _cameraPrefab;
    [SerializeField] private Transform _cameraHolder;

    [Header("UIの生成")]
    [SerializeField]
    private GameObject canvasPrefab;

    [Header("Player種別")]
    [SerializeField]
    private PlayerType playerType;

    private CancellationToken _token;

    #endregion

    #region public

    /// <summary>
    /// Player種別を外部から取得
    /// </summary>
    public PlayerType PlayerType => playerType;

    /// <summary>
    /// 初期化が完了しているかのフラグ
    /// </summary>
    public bool IsInitialized { get; private set; } = false;

    #endregion

    #region private

    //  生成されるカメラの格納場所
    private Camera _camera = null;

    //  生成されるCanvasの格納場所
    private GameObject _playerCanvas = null;

    #endregion

    #region Components

    [Header("Player Components")]
    public PlayerInputController inputController;
    public PlayerMovement controller;
    public CameraController cameraController;
    public PlayerHealth playerHealth;
    public EquipmentManager equipmentManager;
    public InventoryManager inventoryManager;

    [Header("UI")]
    // 全体処理用変数
    public IGuiInitialize characterGUI;
    //  Player側のGUIManager
    public GUIManager guiManager;

    #endregion

    #region override
    public override void OnStartServer()
    {
        base.OnStartServer();

        GameDebug.Server(
        $"[SERVER] OnStartServer " +
        $"ObjectId:{ObjectId}, OwnerId:{OwnerId}",
        true);
    }

    /// <summary>
    /// ※このオブジェクトが生成されると、このメソッドはクライアント側で実行されます。
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        // この処理は、このオブジェクトがインスタンス化されているすべてのクライアントで実行されるため、
        // 自分たちが管理するオブジェクトに対してのみ、カメラをインスタンス化すればよい。        if (IsOwner)
        if (base.IsOwner)
        {
            CliantSideInitialize();
        }
        else
        {
            GetComponent<PlayerManager>().enabled = false;
        }

        GameDebug.Client(
        $"[CLIENT] OnStartClient " +
        $"ObjectId:{ObjectId}, OwnerId:{OwnerId}, IsOwner:{IsOwner}");
    }

    #endregion

    #region Initialize

    private void Awake()
    {
        _token = this.GetCancellationTokenOnDestroy();

        //  Playerのすべてのクラスを取得
        GetPlayerClass();
        //  Playerのすべてのクラスに自身を登録
        Register();
    }


    private bool CliantSideInitialize()
    {
        //  
        UniTask Playertask = PlayerClassInitialize();

        //  PlayerCanvasの生成
        if (!CreateGUI())
        {
            GameDebug.Warning("CliantSideInitialize:Canvasが生成できませんでした。", isShowLog);
        }
        //  Cameraの生成
        if (!CreateMyCamera())
        {
            GameDebug.Warning("CliantSideInitialize:Cameraが生成できませんでした。", isShowLog);
            return false;
        }


        //  初期化処理
        controller.Initialize(_camera.transform);
        equipmentManager?.Initialize();

        //  UI更新要求
        UniTask HealthUItask = HealthUIUpdateRequest();

        IsInitialized = true;

        return true;
    }


    /// <summary>
    /// 必要なコンポーネント取得
    /// </summary>
    private void GetPlayerClass()
    {
        inputController = GetComponent<PlayerInputController>();
        controller = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        equipmentManager = GetComponent<EquipmentManager>();
        inventoryManager = GetComponent<InventoryManager>();
    }

    /// <summary>
    /// 登録
    /// </summary>
    private void Register()
    {
        inputController?.RegisterPlayer(this);
        controller?.RegisterPlayer(this);
        equipmentManager?.RegisterPlayer(this);
        playerHealth?.RegisterPlayer(this);
    }

    /// <summary>
    /// Playerについているすべてのクラスの初期化
    /// </summary>
    private async UniTask PlayerClassInitialize()
    {
        GameDebug.Server($"PlayerClassInitialize," +
            $"IsServerInitialized:{IsServerInitialized}", isShowLog);

        if (playerHealth == null)
        {
            // PlayerHealthの取得待ち
            if (!await WaitForReference(
                () => playerHealth, nameof(playerHealth), 3f, _token))
            {
                return;
            }
        }

        if (_camera == null)
        {
            // Cameraの取得待ち
            if (!await WaitForReference(
                () => _camera, nameof(_camera), 3f, _token))
            {
                return;
            }
        }

        // Player・Bot初期化
        switch (playerType)
        {
            case PlayerType.Player:
                //  statusの初期化
                playerHealth.Initialize(
                    PLAYER_MAXHEALTH,
                    PLAYER_MAXSHIELDE,
                    100,
                    100
                    );

                break;
            case PlayerType.Bot:
                BotHealth bot = GetComponent<BotHealth>();
                playerHealth?.Initialize(
                    bot.maxHealth,
                    bot.maxShielde,
                    bot.Health,
                    bot.Shielde
                    );

                break;
        }

        // UIを初期化
        characterGUI?.Initialize();
    }


    #endregion

    #region 生成関数

    /// <summary>
    /// Canvasの生成or取得
    /// </summary>
    private bool CreateGUI()
    {
        //  すでにあるなら飛ばす
        if (_playerCanvas != null)
            return false;

        if (!ReferenceCheck(canvasPrefab, "canvasPrefab"))
            return false;

        // UIを登録
        switch (playerType)
        {
            case PlayerType.Player:

                //  canvasPrefabの生成
                _playerCanvas = Instantiate(canvasPrefab);

                //  生成チェック
                if (!ReferenceCheck(_playerCanvas, "_playerCanvas"))
                    return false;

                GameDebug.Log(
                    $"[CreateGUI]IsServerInitialized:{IsServerInitialized}," +
                    $"生成したCanvas: {_playerCanvas.name}," +
                    $"CanvasのPrefab: {canvasPrefab.name}," +
                    $"CanvasのActive: {_playerCanvas.activeInHierarchy}"
                    , isShowLog);

                characterGUI = _playerCanvas.GetComponentInChildren<GUIManager>();
                break;
            case PlayerType.Bot:
                characterGUI = GetComponentInChildren<BOTHealthBer>();
                break;
        }

        if (characterGUI == null)
            GameDebug.Error("GUIManagerがNULLです！", isShowLog, this);

        characterGUI?.RegisterPlayer(this);

        return true;
    }

    /// <summary>
    /// 自身のカメラを生成
    /// </summary>
    private bool CreateMyCamera()
    {
        //  すでにあるなら飛ばす
        if (_camera != null)
            return false;

        //  リファレンスチェック
        if (!ReferenceCheck(_cameraPrefab, "CameraPrefab") ||
            !ReferenceCheck(_cameraHolder, "CameraHolder"))
            return false;

        //  生成
        _camera = Instantiate(
            _cameraPrefab,
            _cameraHolder.position,
            _cameraHolder.rotation,
            _cameraHolder
        );

        if (!ReferenceCheck(_camera, "Camera"))
            return false;

        cameraController = _camera.GetComponent<CameraController>();

        if (!ReferenceCheck(cameraController, "CameraController"))
            return false;

        //  初期化
        cameraController.Initialize(this, transform);

        return true;
    }


    #endregion

    #region 素材要求関数

    /// <summary>
    /// 弾消費要求
    /// </summary>
    /// <param name="type">建材の種類</param>
    /// <param name="amount">消費要求数</param>
    /// <param name="approvalValue">消費承認数</param>
    /// <returns></returns>
    public bool TryConsumeAmmo(
    AmmoType type,
    int amount,
    out int approvalValue)
    {
        return inventoryManager.TryConsumeAmmo(
            type,
            amount,
            out approvalValue);
    }

    /// <summary>
    /// 建材消費要求
    /// </summary>
    /// <param name="type">建材の種類</param>
    /// <param name="amount">消費要求数</param>
    /// <param name="approvalValue">消費承認数 ※場合によっては使用しなくてもよい</param>
    /// <returns></returns>
    public bool TryConsumeBildMat(
    BildingMatType type,
    int amount,
    out int approvalValue)
    {
        return inventoryManager.TryConsumeBildMat(
            type,
            amount,
            out approvalValue);
    }

    private async UniTask HealthUIUpdateRequest()
    {
        if (playerHealth == null) return;

        // PlayerHealthの取得待ち
        if (playerHealth.isInitialize == false)
        {
            if (!await WaitForReference(
                   () => playerHealth.isInitialize, nameof(playerHealth.isInitialize), 5f, _token))
            {
                return;
            }
        }
        //  UI更新要求
        playerHealth.UIUpdateRequest();
    }

    #endregion

    #region 射撃要求

    /// <summary>
    /// 通常弾発射要求
    /// </summary>
    /// <param name="_script"></param>
    /// <param name="_prefab"></param>
    /// <param name="_position"></param>
    /// <param name="_rotation"></param>
    /// <param name="_transform"></param>
    /// <param name="target"></param>
    public void BulletShootRequest(
    WeaponBase _script,
    NetworkObject _prefab,
    Vector3 _position,
    Quaternion _rotation,
    Transform _transform,
    Vector3 target)
    {
        BulletShoot(
            this,
            _script,
            _prefab,
            _position,
            _rotation,
            _transform,
            target
            );
    }

    /// <summary>
    /// 通常弾発射処理
    /// </summary>
    /// <param name="_owner"></param>
    /// <param name="script"></param>
    /// <param name="_prefab"></param>
    /// <param name="_position"></param>
    /// <param name="_rotation"></param>
    /// <param name="_transform"></param>
    /// <param name="target"></param>
    [ServerRpc]
    private void BulletShoot(
    PlayerManager _owner,
    WeaponBase script,
    NetworkObject _prefab,
    Vector3 _position,
    Quaternion _rotation,
    Transform _transform,
    Vector3 target)
    {
        script.AddShootCount();
        if (_prefab == null) return;


        NetworkObject projectile = Instantiate(
            _prefab,
            _position,
            _rotation,
            _transform
            );

        Spawn(projectile);

        ProjectileBase p_Base = projectile.GetComponent<ProjectileBase>();

        if (projectile != null)
        {
            //  仮でここに生成
            WeaponDamageSyncData syncData
                = CreateWeaponDamageSyncData(script.GetWeaponData());

            p_Base.Initialize(_owner, script, syncData, target);
        }
        else
        {
            GameDebug.ServerWarning("ProjectileBaseが取得できませんでした");
        }
    }


    /// <summary>
    /// 散弾発射要求
    /// </summary>
    /// <param name="_owner"></param>
    /// <param name="_prefab"></param>
    /// <param name="_position"></param>
    /// <param name="_rotation"></param>
    public void SGBulletShootRequest(
        WeaponBase script,
        NetworkObject _prefab,
        Vector3 _position,
        Quaternion _rotation)
    {
        SGBulletShoot(
            this,
            script,
            _prefab,
            _position,
            _rotation);
    }

    /// <summary>
    /// 散弾発射処理
    /// </summary>
    /// <param name="_owner"></param>
    /// <param name="_prefab"></param>
    /// <param name="_position"></param>
    /// <param name="_rotation"></param>
    [ServerRpc]
    public void SGBulletShoot(
        PlayerManager _owner,
        WeaponBase script,
        NetworkObject _prefab,
        Vector3 _position,
        Quaternion _rotation)
    {
        NetworkObject projectile = Instantiate(
           _prefab,
            _position,
            _rotation
            );

        ProjectileBase p_Base = projectile.GetComponent<ProjectileBase>();
        bool CompleteInit = false;

        if (projectile != null)
        {
            //  仮でここに生成
            WeaponDamageSyncData syncData
                = CreateWeaponDamageSyncData(script.GetWeaponData());

            CompleteInit = p_Base.Initialize(_owner, script, syncData, Vector3.zero);
        }
        else
        {
            GameDebug.ServerWarning("ProjectileBaseが取得できませんでした");
        }

        //  弾の初期化が完了していれば
        if (CompleteInit)
        {
            Spawn(projectile);
        }
        else
        {
            GameDebug.ServerWarning("SGProjectileの初期化が完了しておらずSpawnできませんでした");
        }

    }

    /// <summary>
    /// 同期用のデータ作成
    /// </summary>
    /// <param name="_wbase"></param>
    /// <param name="_data"></param>
    /// <returns></returns>
    public WeaponDamageSyncData CreateWeaponDamageSyncData( WeaponData _data)
    {
        WeaponDamageSyncData syncData = new WeaponDamageSyncData();

        syncData.IsValid = true;
        syncData.ItemId = _data.itemId;
        syncData.ammoSpeed = _data.ammoSpeed;
        syncData.damage = _data.weaponDamage;
        syncData.headMultiplier = _data.headMultiplier;
        syncData.bodyMultiplier = _data.bodyMultiplier;
        syncData.legMultiplier = _data.legMultiplier;

        return syncData;
    }

    #endregion

    #region checker

    public bool ReferenceCheck(Object target, string objectName)
    {
        return CheckReference(target, objectName, isShowLog, this);
    }

    private bool MyObjectCheck()
    {
        return (ReferenceCheck(_camera, "_camera") && ReferenceCheck(_playerCanvas, "_playerCanvas"));
    }

    #endregion

    #region Getter
    public bool GetIsOwner()
    {
        return IsOwner;
    }

    #endregion
}
