using UnityEngine;

public class BaseCharacterLobby : MonoBehaviour
{
    protected Animator animator;
    protected float speed = 0.1f;
    protected float rotateSpeed = 5f;

    protected Vector3 moveDirection;
    protected Vector3[] waypoints;
    protected int currentWaypoint = 0;

    protected float waitTime = 3f;
    protected float waitCounter = 0f;
    protected bool isWaiting = false;

    // Xoay nhân vật từ từ về hướng đích
    public void TurnAround(Vector3 direction)
    {

        float angle = Vector3.Angle(transform.forward, direction);

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );

        if (angle > 3f)
        {
            return;
        }
    }

    // Tự động di chuyển tự động giữa các waypoint
    public void AutoMove()
    {
        if (isWaiting)
        {
            waitCounter += Time.deltaTime;
            if (waitCounter >= waitTime)
            {
                isWaiting = false;
                waitCounter = 0f;
                currentWaypoint++;
                if (currentWaypoint >= waypoints.Length)
                    currentWaypoint = 0;
            }
            return;
        }
        else
        {
            Vector3 target = waypoints[currentWaypoint];
            Vector3 direction = (target - transform.position).normalized;

            // Quay dần về hướng di chuyển
            if (direction != Vector3.zero)
            {
                animator.SetBool("isWalking", true);
                TurnAround(direction);
            }

            // Di chuyển về hướng waypoint
            transform.position += direction * speed * Time.deltaTime;
            animator.SetBool("isWalking", true);

            // Kiểm tra đã đến waypoint chưa
            if (Vector3.Distance(transform.position, target) < 0.1f)
            {
                isWaiting = true;
                animator.SetBool("isWalking", false);
            }
        }
    }
}
