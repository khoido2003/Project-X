using UnityEngine;

namespace Utils
{
    public class AutoCleanupVFX : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Time in seconds before this VFX object is destroyed.")]
        private float lifetime = 2f;

        private void Start()
        {
            // Simple self-destruct after 'lifetime' seconds
            Destroy(gameObject, lifetime);
        }
    }
}
