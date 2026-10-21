using System;
using UnityEngine;

public class BuildDamageTest : MonoBehaviour
{
    public Camera playerCamera;
    public Transform playerRoot;

    public float attackDistance = 6f;
    public int damage = 25;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            Attack();
        }
    }

    private void Attack()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            attackDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // 自分の体は無視する
            if (playerRoot != null &&
                (hit.transform == playerRoot ||
                 hit.transform.IsChildOf(playerRoot)))
            {
                continue;
            }

            BuildHP hp = hit.collider.GetComponentInParent<BuildHP>();

            if (hp != null)
            {
                hp.TakeDamage(damage);
                Debug.Log($"建築物のHP：{hp.CurrentHP} / {hp.MaxHP}");
            }

            // 手前の物に当たったら止める
            return;
        }
    }
}