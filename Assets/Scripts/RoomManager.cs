using UnityEngine;
using System.Collections;

public class RoomManager : MonoBehaviour
{
    public GameObject[] furniture;
    public GameObject mistyCloud;
    public GameObject shadowMonsterPrefab;
    public GameObject treeOfLight;
    private Renderer[] renderers;

    void Start()
    {
        renderers = FindObjectsOfType<Renderer>(); // Cache all renderers
    }

    public void ClearRoom()
    {
        mistyCloud.SetActive(true);
        foreach (GameObject item in furniture)
        {
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (!rb) rb = item.AddComponent<Rigidbody>();
            rb.AddForce(Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }
        foreach (Renderer r in renderers) // Black and white effect
        {
            if (r.gameObject != treeOfLight)
                r.material.color = Color.gray;
        }
        StartCoroutine(SpawnMonsters());
    }

    IEnumerator SpawnMonsters()
    {
        yield return new WaitForSeconds(2f);
        for (int i = 0; i < 5; i++)
        {
            Vector3 spawnPos = treeOfLight.transform.position + Random.insideUnitSphere * 5f;
            spawnPos.y = 0;
            GameObject monster = Instantiate(shadowMonsterPrefab, spawnPos, Quaternion.identity);
            monster.GetComponent<ShadowMonster>().SetTarget(treeOfLight.transform);
        }
    }

    public void ResetRoom()
    {
        mistyCloud.SetActive(false);
        foreach (GameObject item in furniture)
        {
            item.SetActive(true);
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb) Destroy(rb);
            item.transform.position = item.GetComponent<ResetPosition>().originalPos;
            item.GetComponent<Renderer>().material.color = Color.white; // Restore color
        }
        FindObjectOfType<SeedSpawnManager>().RespawnSeed();
    }
}