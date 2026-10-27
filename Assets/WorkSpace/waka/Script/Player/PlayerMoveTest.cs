using UnityEngine;

public class PlayerMoveTest : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Animator animator;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;

        Vector3 direction = new Vector3(x, 0f, z).normalized;

        transform.position += direction * moveSpeed * Time.deltaTime;

        if (direction.sqrMagnitude > 0f)
            transform.rotation = Quaternion.LookRotation(direction);

        if (animator != null)
            animator.SetFloat("Speed", direction.magnitude);
    }
}