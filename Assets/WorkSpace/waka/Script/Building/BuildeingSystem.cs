using UnityEngine;

/// <summary>
/// 建築全体を管理するクラス
///
/// ・壁、階段、床のモード切り替え
/// ・選択中の建築プレビュー更新
/// ・左クリック長押しによる連続建築
/// ・未設定の参照をシーン内から自動取得
/// </summary>
public class BuildingSystem : MonoBehaviour
{
    /// <summary>
    /// 建築物の種類
    /// </summary>
    public enum BuildType
    {
        None,
        Wall,
        Ramp,
        Floor
    }

    [Header("入力")]
    [Tooltip("PlayerInputControllerを設定する")]
    public PlayerInputController input;

    [Header("建築スクリプト")]
    public BuildWall wallBuilder;
    public BuildRamp rampBuilder;
    public BuildFloor floorBuilder;

    [Header("連続建築設定")]
    [Tooltip("左クリック長押し中に建築する間隔")]
    public float buildInterval = 0.15f;

    // 現在選択中の建築タイプ
    private BuildType currentBuildType =
        BuildType.None;

    // 最後に建築した時間
    private float lastBuildTime;

    private void Awake()
    {
        /*
         * Inspectorで参照が設定されていない場合は
         * シーン内から自動的に探す。
         */

        if (input == null)
        {
            input =
                FindObjectOfType<PlayerInputController>();
        }

        if (wallBuilder == null)
        {
            wallBuilder =
                FindObjectOfType<BuildWall>();
        }

        if (rampBuilder == null)
        {
            rampBuilder =
                FindObjectOfType<BuildRamp>();
        }

        if (floorBuilder == null)
        {
            floorBuilder =
                FindObjectOfType<BuildFloor>();
        }

        // 必要な参照が見つからなかった場合はエラーを表示
        ValidateReferences();
    }

    /// <summary>
    /// 必要な参照が設定されているか確認する
    /// </summary>
    private void ValidateReferences()
    {
        if (input == null)
        {
            Debug.LogError(
                "BuildingSystem: " +
                "PlayerInputControllerが見つかりません"
            );
        }

        if (wallBuilder == null)
        {
            Debug.LogError(
                "BuildingSystem: " +
                "BuildWallが見つかりません"
            );
        }

        if (rampBuilder == null)
        {
            Debug.LogError(
                "BuildingSystem: " +
                "BuildRampが見つかりません"
            );
        }

        if (floorBuilder == null)
        {
            Debug.LogError(
                "BuildingSystem: " +
                "BuildFloorが見つかりません。" +
                "BuildFloorをシーン内のGameObjectへ追加してください"
            );
        }
    }

    private void Update()
    {
        // 入力参照がなければ処理できない
        if (input == null)
        {
            return;
        }

        // 壁モードへ切り替える
        if (input.BuildWallPressed)
        {
            Debug.Log(
                "壁建築モードへ切り替え"
            );

            SelectBuild(BuildType.Wall);
            input.ResetBuildWall();
        }

        // 階段モードへ切り替える
        if (input.BuildRampPressed)
        {
            Debug.Log(
                "階段建築モードへ切り替え"
            );

            SelectBuild(BuildType.Ramp);
            input.ResetBuildRamp();
        }

        // 床モードへ切り替える
        if (input.BuildFloorPressed)
        {
            Debug.Log(
                "床建築モードへ切り替え"
            );

            SelectBuild(BuildType.Floor);
            input.ResetBuildFloor();
        }

        // 選択中の建築プレビューを更新する
        UpdateCurrentPreview();

        // 左クリック長押しで連続建築
        if (currentBuildType != BuildType.None &&
            Input.GetMouseButton(0))
        {
            // 建築間隔が経過している
            if (Time.time - lastBuildTime >=
                buildInterval)
            {
                ConfirmBuild();

                lastBuildTime =
                    Time.time;
            }
        }
    }

    /// <summary>
    /// 建築タイプを切り替える
    /// </summary>
    private void SelectBuild(
        BuildType buildType
    )
    {
        // 現在表示されているプレビューをすべて削除
        HideAllPreview();

        currentBuildType =
            buildType;

        switch (currentBuildType)
        {
            case BuildType.Wall:
                SelectWall();
                break;

            case BuildType.Ramp:
                SelectRamp();
                break;

            case BuildType.Floor:
                SelectFloor();
                break;

            default:
                currentBuildType =
                    BuildType.None;
                break;
        }
    }

    /// <summary>
    /// 壁モードを開始する
    /// </summary>
    private void SelectWall()
    {
        if (wallBuilder == null)
        {
            Debug.LogError(
                "Wall Builderが設定されていません"
            );

            currentBuildType =
                BuildType.None;

            return;
        }

        wallBuilder.ShowPreview();

        Debug.Log(
            "壁Previewを生成しました"
        );
    }

    /// <summary>
    /// 階段モードを開始する
    /// </summary>
    private void SelectRamp()
    {
        if (rampBuilder == null)
        {
            Debug.LogError(
                "Ramp Builderが設定されていません"
            );

            currentBuildType =
                BuildType.None;

            return;
        }

        rampBuilder.ShowPreview();

        Debug.Log(
            "階段Previewを生成しました"
        );
    }

    /// <summary>
    /// 床モードを開始する
    /// </summary>
    private void SelectFloor()
    {
        /*
         * Awake時に見つからなかった場合も、
         * 床モード選択時にもう一度探す。
         */
        if (floorBuilder == null)
        {
            floorBuilder =
                FindObjectOfType<BuildFloor>();
        }

        if (floorBuilder == null)
        {
            Debug.LogError(
                "Floor Builderが設定されていません。" +
                "BuildFloorをGameObjectへ追加し、" +
                "BuildingSystemのFloor Builderへ設定してください"
            );

            currentBuildType =
                BuildType.None;

            return;
        }

        Debug.Log(
            "BuildFloor.ShowPreviewを呼びます"
        );

        floorBuilder.ShowPreview();

        Debug.Log(
            "床Previewの生成処理が完了しました"
        );
    }

    /// <summary>
    /// 選択中の建築プレビューを更新する
    /// </summary>
    private void UpdateCurrentPreview()
    {
        switch (currentBuildType)
        {
            case BuildType.Wall:
                if (wallBuilder != null)
                {
                    wallBuilder.UpdatePreview();
                }
                break;

            case BuildType.Ramp:
                if (rampBuilder != null)
                {
                    rampBuilder.UpdatePreview();
                }
                break;

            case BuildType.Floor:
                if (floorBuilder != null)
                {
                    floorBuilder.UpdatePreview();
                }
                break;
        }
    }

    /// <summary>
    /// 選択中の建築物を実際に建築する
    /// </summary>
    private void ConfirmBuild()
    {
        switch (currentBuildType)
        {
            case BuildType.Wall:
                if (wallBuilder != null)
                {
                    wallBuilder.Build();
                }
                break;

            case BuildType.Ramp:
                if (rampBuilder != null)
                {
                    rampBuilder.Build();
                }
                break;

            case BuildType.Floor:
                if (floorBuilder != null)
                {
                    floorBuilder.Build();
                }
                break;
        }
    }

    /// <summary>
    /// すべての建築プレビューを削除する
    /// </summary>
    private void HideAllPreview()
    {
        if (wallBuilder != null)
        {
            wallBuilder.HidePreview();
        }

        if (rampBuilder != null)
        {
            rampBuilder.HidePreview();
        }

        if (floorBuilder != null)
        {
            floorBuilder.HidePreview();
        }
    }

    /// <summary>
    /// 建築モードを終了する
    /// </summary>
    public void CancelBuild()
    {
        HideAllPreview();

        currentBuildType =
            BuildType.None;

        Debug.Log(
            "建築モードを終了しました"
        );
    }

    /// <summary>
    /// 現在選択している建築タイプを取得する
    /// </summary>
    public BuildType GetCurrentBuildType()
    {
        return currentBuildType;
    }
}
