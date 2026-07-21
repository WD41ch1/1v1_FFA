using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropHandlerDebugger : MonoBehaviour
{
    [SerializeField]
    private GameObject dropZoneObject;

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.M))
            DiagnoseDropZone();
    }

    public void DiagnoseDropZone()
    {
        if (dropZoneObject == null)
        {
            Debug.LogError("dropZoneObject が設定されていません");
            return;
        }

        Debug.Log("=== DropZone 診断開始 ===");

        // 1. IDropHandler チェック
        if (dropZoneObject.GetComponent<IDropHandler>() != null)
        {
            Debug.Log("✅ IDropHandler: 実装されている");
        }
        else
        {
            Debug.LogError("❌ IDropHandler: 実装されていない");
        }

        // 2. Image コンポーネントチェック
        Image image = dropZoneObject.GetComponent<Image>();
        if (image != null)
        {
            Debug.Log("✅ Image: 存在する");

            if (image.raycastTarget)
            {
                Debug.Log("✅ Raycast Target: ON");
            }
            else
            {
                Debug.LogWarning("⚠️ Raycast Target: OFF");
            }
        }
        else
        {
            Debug.LogError("❌ Image: 存在しない");
        }

        // 3. CanvasGroup チェック
        CanvasGroup canvasGroup = dropZoneObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            if (canvasGroup.interactable)
            {
                Debug.Log("✅ CanvasGroup.Interactable: ON");
            }
            else
            {
                Debug.LogWarning("⚠️ CanvasGroup.Interactable: OFF");
            }
        }

        // 4. EventSystem チェック
        if (FindObjectOfType<EventSystem>() != null)
        {
            Debug.Log("✅ EventSystem: 存在する");
        }
        else
        {
            Debug.LogError("❌ EventSystem: 存在しない");
        }

        // 5. GraphicRaycaster チェック
        Canvas canvas = GetComponentInParent<Canvas>();
        if(canvas == null)
            dropZoneObject.transform.root.GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            if (canvas.GetComponent<GraphicRaycaster>() != null)
            {
                Debug.Log("✅ GraphicRaycaster: Canvas に存在");
            }
            else
            {
                Debug.LogError("❌ GraphicRaycaster: Canvas に存在しない");
            }
        }

        Debug.Log("=== 診断完了 ===");
    }
}
