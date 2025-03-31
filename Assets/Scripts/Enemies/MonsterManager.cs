using UnityEngine;
using System.Collections.Generic;

namespace Enemies
{
    public class MonsterManager : MonoBehaviour
    {
        // Singleton pattern
        private static MonsterManager _instance;
        public static MonsterManager Instance
        {
            get
            {
                // If instance doesn't exist, try to find it
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<MonsterManager>();
                    
                    // If it still doesn't exist, create a new GameObject with the component
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject("MonsterManager");
                        _instance = singletonObject.AddComponent<MonsterManager>();
                        Debug.Log("MonsterManager instance created automatically");
                    }
                }
                
                return _instance;
            }
        }
        
        // List to track all active monsters
        private List<ShadowMonster> activeMonsters = new List<ShadowMonster>();
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
        }
        
        // Method to register a monster with the manager
        public void RegisterMonster(ShadowMonster shadowMonster)
        {
            if (shadowMonster != null && !activeMonsters.Contains(shadowMonster))
            {
                activeMonsters.Add(shadowMonster);
                // You might want to add additional logic here
                Debug.Log("Monster registered: " + shadowMonster.name);
            }
        }
        
        // Overload to handle GameObject parameters
        public void RegisterMonster(GameObject gameObj)
        {
            if (gameObj != null)
            {
                ShadowMonster shadowMonster = gameObj.GetComponent<ShadowMonster>();
                if (shadowMonster != null)
                {
                    RegisterMonster(shadowMonster); // Call the original method
                }
                else
                {
                    Debug.LogWarning("Tried to register a GameObject without a ShadowMonster component: " + gameObj.name);
                }
            }
        }
        
        // Method to unregister a monster from the manager
        public void UnregisterMonster(ShadowMonster shadowMonster)
        {
            if (shadowMonster != null && activeMonsters.Contains(shadowMonster))
            {
                activeMonsters.Remove(shadowMonster);
                // Additional cleanup logic
                Debug.Log("Monster unregistered: " + shadowMonster.name);
            }
        }
        
        // Overload to handle GameObject parameters for unregistering
        public void UnregisterMonster(GameObject gameObj)
        {
            if (gameObj != null)
            {
                ShadowMonster shadowMonster = gameObj.GetComponent<ShadowMonster>();
                if (shadowMonster != null)
                {
                    UnregisterMonster(shadowMonster);
                }
            }
        }
        
        // Get the count of active monsters
        public int GetActiveMonsterCount()
        {
            return activeMonsters.Count;
        }
        
        // Get all monsters of a specific type
        public List<T> GetMonstersOfType<T>() where T : ShadowMonster
        {
            List<T> result = new List<T>();
            foreach (var monster in activeMonsters)
            {
                if (monster is T typedMonster)
                {
                    result.Add(typedMonster);
                }
            }
            return result;
        }
    }
}