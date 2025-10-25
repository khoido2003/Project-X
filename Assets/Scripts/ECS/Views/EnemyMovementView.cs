using UnityEngine;

public class EnemyMovementView : EntityView
{
    public bool SmoothPosition = true;
    public float PositionLerpSpeed = 10f;
    public float RotationLerpSpeed = 10f;
    public float SnapThreshold = 0.5f;

    private Transform _tranform;

    private void Awake()
    {
        _tranform = transform;
    }

    private void Update()
    {
        if (WorldInstance == null || EntityInstance.Equals(default))
        {
            return;
        }

        if (!WorldInstance.Components.TryGet(EntityInstance, out TransformComponent tf))
        {
            return;
        }

        Vector3 targetPos = tf.Position;
        Quaternion targetRotation = tf.Rotation;

        if (SmoothPosition)
        {
            Vector3 current = _tranform.position;
            float dist = Vector3.Distance(current, targetPos);

            if (dist < SnapThreshold)
            {
                _tranform.position = targetPos;
            }
            else
            {
                _tranform.position = Vector3.Lerp(current, targetPos, Time.deltaTime * PositionLerpSpeed);
            }

            _tranform.rotation = Quaternion.Slerp(
                _tranform.rotation,
                targetRotation,
                Time.deltaTime * RotationLerpSpeed
            );
        }
        else
        {
            _tranform.SetPositionAndRotation(targetPos, targetRotation);
        }
    }
}
