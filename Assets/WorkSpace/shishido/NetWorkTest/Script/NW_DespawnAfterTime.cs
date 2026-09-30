using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NW_DespawnAfterTime : NetworkBehaviour
{
    public float SecondsBeforeDespawn = 3f;

    public override void OnStartServer()
    {
        StartCoroutine(DespawnAfterSeconds());
    }

    private IEnumerator DespawnAfterSeconds()
    {
        yield return new WaitForSeconds(SecondsBeforeDespawn);

        Despawn(); // NetworkBehaviour shortcut for ServerManager.Despawn(gameObject);
    }
}
