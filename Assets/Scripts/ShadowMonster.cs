using UnityEngine;

public class ShadowMonster : MonoBehaviour
{
    private Transform target;
    public float speed = 2f;
    public int health = 3;

    public void SetTarget(Transform t)
    {
        target = t; // Initially tree
    }

    void Update()
    {
        if (target != null)
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }

    public void TakeDamage(int damage, Transform player)
    {
        health -= damage;
        if (health > 0) target = player; // Switch to player if hit
        else Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == target.gameObject && target != null)
        {
            // Damage tree or player logic here
            Destroy(gameObject);
        }
    }
}