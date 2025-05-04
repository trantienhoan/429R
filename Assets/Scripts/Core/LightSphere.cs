using UnityEngine;
using System.Collections;

public class LightSphere : MonoBehaviour
{
    public float growthRate = 2f;
    public float maxScale = 10f;
    public float lifespan = 3f;
    public string targetTag = "ShadowMonster"; // Tag of the objects to collide with
    private float _currentScale = 1;

    void Start()
    {
        StartCoroutine(GrowAndDestroy());
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag(targetTag))
        {
            Destroy(other.gameObject);
        }
    }

    IEnumerator GrowAndDestroy()
    {
        float timer = 0;
        while (timer < lifespan)
        {
            timer += Time.deltaTime;
            _currentScale += growthRate * Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Min(_currentScale, maxScale);
            yield return null;
        }

        Destroy(gameObject);
    }
}