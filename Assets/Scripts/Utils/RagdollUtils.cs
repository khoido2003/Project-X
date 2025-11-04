using UnityEngine;

public static class RagdollUtility
{
    public static void ActivateRagdoll(GameObject enemy)
    {
        if (!enemy)
            return;

        var animator = enemy.GetComponent<Animator>();
        if (animator)
            animator.enabled = false;

        var rigBuilder = enemy.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
        if (rigBuilder)
            rigBuilder.enabled = false;

        // Enable physics
        foreach (var rb in enemy.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        foreach (var col in enemy.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
        }

        foreach (var rb in enemy.GetComponentsInChildren<Rigidbody>())
        {
            rb.AddForce(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        }
    }
}
