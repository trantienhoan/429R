using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    public class SpiderPool : MonoBehaviour
    {
        public static SpiderPool Instance;

        [SerializeField] private GameObject spiderPrefab;
        [SerializeField] private int initialPoolSize = 10;

        private readonly Queue<GameObject> pool = new Queue<GameObject>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Preload spiders
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject spider = Instantiate(spiderPrefab);
                spider.SetActive(false);
                pool.Enqueue(spider);
            }
        }

        public GameObject GetSpider(Vector3 position, Quaternion rotation)
        {
            var spider = pool.Count > 0 ? pool.Dequeue() : Instantiate(spiderPrefab);

            spider.transform.position = position;
            spider.transform.rotation = rotation;
            spider.SetActive(true);
            return spider;
        }

        public void ReturnSpider(GameObject spider)
        {
            if (spider == null) return;
            spider.SetActive(false);
            pool.Enqueue(spider);
        }
    }
}