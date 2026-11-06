using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileDespawnAfterTime : NetworkBehaviour
{
    public float SecondsBeforeDespawn = 3f;

    public override void OnStartServer()
    {
        StartCoroutine(DespawnAfterSeconds());
    }

    private IEnumerator DespawnAfterSeconds()
    {
        yield return new WaitForSeconds(SecondsBeforeDespawn);

        // NetworkBehaviour shortcut for ServerManager.Despawn(gameObject);
        Despawn(); 
    }
}

