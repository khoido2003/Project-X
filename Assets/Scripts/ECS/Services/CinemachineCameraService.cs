using Unity.Cinemachine;
using UnityEngine;

public interface ICameraService
{
    void Follow(Transform target);
}

public class CinemachineCameraService : MonoBehaviour, ICameraService
{
    [SerializeField]
    private CinemachineCamera cinemachineCamera;

    private void Awake()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }
    }

    public void Follow(Transform target)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = target;
            cinemachineCamera.LookAt = target;
        }
        else
        {
            Debug.LogWarning("CinemachineCamera component not found.");
        }
    }
}
