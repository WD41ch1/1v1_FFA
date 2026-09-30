using FishNet.Object;
using UnityEngine;

public class NW_PlayerCubeCreator : NetworkBehaviour
{
    public NetworkObject CubePrefab;

    private void Update()
    {
        // Only the local player object should perform these actions.
        if (!IsOwner)
            return;

        if (Input.GetButtonDown("Fire1"))
            SpawnCube();
    }

    // We are using a ServerRpc here because the Server needs to do all network object spawning.
    [ServerRpc]
    private void SpawnCube()
    {
        NetworkObject obj = Instantiate(CubePrefab, transform.position, Quaternion.identity);
        obj.GetComponent<NW_SyncMaterialColor>().Color.Value = Random.ColorHSV();
        Spawn(obj); // NetworkBehaviour shortcut for ServerManager.Spawn(obj);
    }
}
