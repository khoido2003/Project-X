using UnityEngine;

public class LobbyCharacterController1 : BaseCharacterLobby
{
    void Start()
    {
        animator = GetComponent<Animator>();

        // Khai báo 2 waypoint
        waypoints = new Vector3[]
        {
            new Vector3(8.124f, transform.position.y, 6.794f),
            new Vector3(8.124f, transform.position.y, 4.277f),
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
        if (isWaiting)
        {
            animator.SetBool("isWalking", false);
            Vector3 direction = new Vector3(1, 0, 0);

            TurnAround(direction);
        }
    }
}
