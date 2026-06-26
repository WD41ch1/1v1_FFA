using UnityEngine;

/// <summary>
/// 建築全体を管理するクラス
/// 壁・階段の切り替え、プレビュー更新、左クリック建築を担当
/// </summary>
public class BuildingSystem : MonoBehaviour
{
    public enum BuildType
    {
        None,
        Wall,
        Ramp
    }

    [Header("参照")]
    public PlayerInputController input;
    public BuildWall wallBuilder;
    public BuildRamp rampBuilder;

    [Header("連続建築設定")]
    public float buildInterval = 0.15f;

    private BuildType currentBuildType = BuildType.None;
    private float lastBuildTime;

    private void Update()
    {
        // サイドボタン手前：壁モード
        if (input.BuildWallPressed)
        {
            SelectBuild(BuildType.Wall);
            input.ResetBuildWall();
        }

        // サイドボタン奥：階段モード
       if (input.BuildRampPressed)
       {
           SelectBuild(BuildType.Ramp);
           input.ResetBuildRamp();
       }

        // 選択中の建築プレビューを更新
        UpdateCurrentPreview();

        // 左クリック長押しで建築
        if (currentBuildType != BuildType.None && Input.GetMouseButton(0))
        {
            if (Time.time - lastBuildTime >= buildInterval)
            {
                ConfirmBuild();
                lastBuildTime = Time.time;
            }
        }
    }

    private void SelectBuild(BuildType buildType)
    {
        HideAllPreview();

        currentBuildType = buildType;

        if (currentBuildType == BuildType.Wall)
        {
            wallBuilder.ShowPreview();
        }
        else if (currentBuildType == BuildType.Ramp)
        {
            rampBuilder.ShowPreview();
        }
    }

    private void UpdateCurrentPreview()
    {
        if (currentBuildType == BuildType.Wall)
        {
            wallBuilder.UpdatePreview();
        }
        else if (currentBuildType == BuildType.Ramp)
        {
            rampBuilder.UpdatePreview();
        }
    }

    private void ConfirmBuild()
    {
        if (currentBuildType == BuildType.Wall)
        {
            wallBuilder.Build();
        }
        else if (currentBuildType == BuildType.Ramp)
        {
            rampBuilder.Build();
        }
    }

    private void HideAllPreview()
    {
        wallBuilder.HidePreview();
        rampBuilder.HidePreview();
    }
}