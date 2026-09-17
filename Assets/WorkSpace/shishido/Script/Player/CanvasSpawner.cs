
using UnityEngine;

namespace InfimaGames.LowPolyShooterPack.Interface
{
    /// <summary>
    /// Player Interface.
    /// </summary>
    public class CanvasSpawner : MonoBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Settings")]

        [Tooltip("開始時に生成されるキャンバス・プレハブ。プレイヤーのユーザーインターフェースを表示します。")]
        [SerializeField]
        private GameObject canvasPrefab;

        #endregion

        #region UNITY FUNCTIONS

        /// <summary>
        /// Awake.
        /// </summary>
        private void Awake()
        {
            //Spawn Interface.
            Instantiate(canvasPrefab);
        }

        #endregion
    }
}