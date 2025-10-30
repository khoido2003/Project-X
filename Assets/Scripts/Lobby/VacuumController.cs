using UnityEngine;

public class VacuumController : MonoBehaviour
{
    public float moveSpeed = 1.5f;     // tốc độ di chuyển
    public float rotateSpeed = 3f;     // tốc độ xoay
    public Vector3[] waypoints;

    private int currentIndex = 0;
    private float waitTime = 0f;
    private bool isRotating = true;    // trạng thái: đang xoay hay đang đi

    void Start()
    {
        // Tạo sẵn các điểm di chuyển
        waypoints = new Vector3[]
        {
            new Vector3(7.6f, transform.position.y, -0.69f),
            new Vector3(8.25f, transform.position.y, -0.69f),
            new Vector3(8.18f, transform.position.y, 3.61f),
            new Vector3(8.93f, transform.position.y, 3.61f),
            new Vector3(8.93f, transform.position.y, 6.27f),
            new Vector3(7.61f, transform.position.y, 6.42f),
        };
    }

    void FixedUpdate()
    {
        VacuumAutoMove();
    }

    private void VacuumAutoMove()
    {
        if (waitTime > 0)
        {
            waitTime -= Time.deltaTime;
            return;
        }

        Vector3 target = waypoints[currentIndex];
        Vector3 direction = (target - transform.position).normalized;

        if (isRotating)
        {
            if (direction == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotateSpeed * Time.deltaTime * 100
            );
            if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
            {
                isRotating = false;
            }
        }
        else
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, target) < 0.05f)
            {
                currentIndex++;

                if (currentIndex >= waypoints.Length)
                {
                    currentIndex = 0;
                    waitTime = 10f;  // nghỉ 10 giây
                }
                else
                {
                    waitTime = 1f;
                }

                isRotating = true;
            }
        }
    }
}
