using FishNet.Object;
using System.Threading.Tasks;
using Unity.VisualScripting;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static GameConst;

/// <summary>
/// Playerについているclassを管理するクラス
/// </summary>
public class PlayerManager : NetworkBehaviour
{
    [SerializeField, Header("ローカル上でのデバッグ")]
    private bool isLocalDebug = false;

    #region SerializeField

    [Header("Cameraの生成")]
    [SerializeField] private Camera _cameraPrefab;
    [SerializeField] private Transform _cameraHolder;

    [Header("Player種別")]
    [SerializeField]
    private PlayerType playerType;

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

    #endregion

    #region Components

    [Header("Player Components")]
    public PlayerInputController inputController;
    public PlayerMovement controller;
    public CameraController cameraController;
    public PlayerHealth playerHealth;
    public EquipmentManager equipmentManager;
    public InventoryManager inventoryManager;

    [Header("UI")]      // UI全体の見た目、処理を管理
    public IGuiInitialize guiManager;

    #endregion

    #region Initialize

    /// <summary>
    /// 初期化
    /// </summary>
    private async void Initialize()
    {
        //  Playerのすべてのクラスを初期化
        PlayerClassInitialize();

        IsInitialized = true;

        //  status管理側からのUI更新要求
        playerHealth?.UIUpdateRequest();
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


        // HACK:    オンライン対応時 変更予定
        // UIを登録
        switch (playerType)
        {
            case PlayerType.Player:
                if (IsOwner || isLocalDebug)
                {
                    guiManager = GUIManager.instance;
                }
                break;
            case PlayerType.Bot:
                guiManager = GetComponentInChildren<BOTHealthBer>();
                break;
        }
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
        guiManager?.RegisterPlayer(this);
    }

    /// <summary>
    /// Playerについているすべてのクラスの初期化
    /// </summary>
    private void PlayerClassInitialize()
    {
        // Player・Bot初期化
        switch (playerType)
        {
            case PlayerType.Player:
                //  statusの初期化
                playerHealth?.Initialize(
                    PLAYER_MAXHEALTH,
                    PLAYER_MAXSHIELDE,
                    100,
                    100
                    );

                //  初期化処理
                controller.Initialize(_camera.transform);
                equipmentManager?.Initialize();

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
        guiManager?.Initialize();
    }

    #endregion

    /// <summary>
    /// カメラの生成
    /// ※このオブジェクトが生成されると、このメソッドはクライアント側で実行されます。
    /// </summary>
    public override void OnStartClient()
    {
        Debug.Log("OnStartClient");
        // この処理は、このオブジェクトがインスタンス化されているすべてのクライアントで実行されるため、
        // 自分たちが管理するオブジェクトに対してのみ、カメラをインスタンス化すればよい。        if (IsOwner)
        if (IsOwner)
        {
            CreateMyCamera();
            //  初期化
            Initialize();
        }
    }

    private void Awake()
    {
        //  Playerのすべてのクラスを取得
        GetPlayerClass();
        //  Playerのすべてのクラスに自身を登録
        Register();
    }

    void Start()
    {
#if UNITY_EDITOR
        Debug.Log("Start");
        //  ローカルデバッグ時のカメラ生成
        if (isLocalDebug) CreateMyCamera();
#endif
        //  初期化
        //Initialize();
    }

    /// <summary>
    /// 自身のカメラを生成
    /// </summary>
    private void CreateMyCamera()
    {
        //  すでに生成されていれば
        if (_camera != null) return;
        //  カメラ生成
        _camera =
            Instantiate(_cameraPrefab, _cameraHolder.position, _cameraHolder.rotation, _cameraHolder);
        //  コンポーネント取得 + 初期化
        cameraController = _camera.gameObject.GetComponent<CameraController>();
        cameraController?.Initialize(this, transform);
    }

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

}
