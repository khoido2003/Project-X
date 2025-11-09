using UnityEngine;

public class LobbyCharacterController2 : BaseCharacterLobby
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();

        // Khai báo 2 waypoint
        waypoints = new Vector3[]
        {
            new Vector3(0.237f, transform.position.y, 6.41f),
            new Vector3(5.073f, transform.position.y, 6.41f),
            new Vector3(5.073f, transform.position.y, 4.64f),
        };
    }

    void FixedUpdate()
    {
        AutoMove();
        AdjustLookDirection();
    }

    // Điều chỉnh góc nhìn của nhân vật trong trong thời gian nghỉ
    private void AdjustLookDirection()
    {
        if (isWaiting && currentWaypoint == 2)
        {
            animator.SetBool("isWalking", false);

            Vector3 targetPosition = new Vector3(0, transform.position.y, 0);
            Vector3 direction = (targetPosition - transform.position).normalized;

            TurnAround(direction);
        }
    }
}
