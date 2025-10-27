using UnityEngine;

public class LobbyCharacterController : MonoBehaviour
{
    public Animator animator;
    public float speed = 0.01f;
    public float rotateSpeed = 5f;

    private Vector3 moveDirection;
    public Vector3[] waypoints;
    private int currentWaypoint = 0;
    private bool isReturning = false;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Khai báo 2 waypoint
        waypoints = new Vector3[]
        {
            new Vector3(3.58f, transform.position.y, 6.46f),
            new Vector3(3.58f, transform.position.y, -0.84f),
        };
    }

    void FixedUpdate()
    {
        //AutoMove();
        HandleMove();
    }

    private void AutoMove()
    {
        if (waypoints.Length == 0) return;
        Vector3 target = waypoints[currentWaypoint];
        moveDirection = (target - transform.position).normalized;

        if (Vector3.Distance(transform.position, target) > 0.1f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotateSpeed);

            transform.position += moveDirection * speed * Time.deltaTime;

            if (animator != null)
                animator.SetBool("isWalking", true);
        }
        else
        {
            if (!isReturning)
            {
                currentWaypoint++;
                if (currentWaypoint >= waypoints.Length)
                {
                    currentWaypoint = waypoints.Length - 2;
                    isReturning = true;
                }
            }
            else
            {
                currentWaypoint--;
                if (currentWaypoint < 0)
                {
                    currentWaypoint = 1;
                    isReturning = false;
                }
            }

        }
    }

    private void HandleMove()
    {
        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
        {
            move.z += 1;
        }
        if (Input.GetKey(KeyCode.S))
        {
            move.z -= 1;
        }
        if (Input.GetKey(KeyCode.A))
        {
            move.x -= 1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            move.x += 1;
        }

        move = move.normalized;

        transform.position += move * Time.deltaTime * speed;

        bool isWalking = move.magnitude > 0;
        animator.SetBool("isWalking", isWalking);

        if (isWalking)
        {
            transform.forward = move;
        }
    }
}

