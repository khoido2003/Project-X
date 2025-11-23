using UnityEngine;

public class Paralax : MonoBehaviour
{
    [SerializeField]
    private float m_paralaxEffectSpeed;

    private float m_length;

    private float m_startPos;

    private void Start()
    {
        m_startPos = transform.position.x;

        m_length = GetComponent<SpriteRenderer>().bounds.size.x;
    }

    private void Update()
    {
        transform.Translate(Vector3.left * m_paralaxEffectSpeed * Time.deltaTime);

        if (transform.position.x < m_startPos - m_length)
        {
            transform.position = Vector3.right * m_startPos * m_length;
        }
    }
}
