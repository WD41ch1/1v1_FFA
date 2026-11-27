using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamagerTextManager : MonoBehaviour
{
    public static DamagerTextManager Instance;
    private readonly int POOLING_MAX_COUNT = 100;

    [SerializeField] public Canvas damageTextCanvas;
    [SerializeField] public TextMeshProUGUI damageText;

    [SerializeField] private Transform useTextTrans;
    [SerializeField] private Transform unUseTextTrans;

    private List<GameObject> useTextList =
        new List<GameObject>();
    private List<GameObject> unUseTextList =
        new List<GameObject>();

    private bool isInitileze = false;

    private void Awake()
    {
        // 既に存在する場合は重複を防ぐ
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        GeneratePoolingSystem();
    }

    public void Initialize(Camera targetCamera)
    {
        if (damageTextCanvas == null || targetCamera == null)
            return;

        damageTextCanvas.worldCamera = targetCamera;


        isInitileze = true;
    }

    /// <summary>
    /// プーリング用オブジェクトの生成
    /// </summary>
    private void GeneratePoolingSystem()
    {
        if (damageTextCanvas == null || damageText == null)
            return;

        for (int i = 0; i < POOLING_MAX_COUNT; i++)
        {
            //  Textの生成
            GameObject obj =
                Instantiate(damageText.gameObject, damageTextCanvas.transform);

            unUseDamageText(obj);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void useDamageText()
    {
        if (useTextList == null) return;
    }

    /// <summary>
    /// DamageTextを未使用状態にする
    /// </summary>
    public void unUseDamageText(GameObject obj)
    {
        if (obj == null || unUseTextList == null) return;

        SetText(obj, string.Empty);
        //  UnUseListに追加
        unUseTextList.Add(obj);
        //  親の設定
        obj.transform.SetParent(unUseTextTrans);
    }

    private void SetText(GameObject obj, string damage)
    {
        if (obj == null)
            return;

        if (!obj.TryGetComponent<TextMeshProUGUI>(out TextMeshProUGUI text))
            return;

        text.text = damage;
    }
}
