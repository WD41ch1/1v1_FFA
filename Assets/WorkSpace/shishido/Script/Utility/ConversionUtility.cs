using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public static class ConversionUtility 
{
    /// <summary>
    /// セットは、プレハブの値を使用して生成
    /// </summary>
    /// <param name = "prefab"></param>
    /// <param name = "pos"></param>
    /// <param name = "rot"></param>
    public static void SetSpawnUsingPrefab(Transform prefab, out Vector3 pos, out Quaternion rot)
    {
        pos = prefab.position;
        rot = prefab.rotation;
    }

    public static string NULLchackString(Object chackTarget)
    {
        string processInfo = "null";

        if (chackTarget != null)
        {
            processInfo =
                $"Name:{chackTarget.name}, ";
        }

        return processInfo;
    }

}
