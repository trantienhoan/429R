using UnityEngine;
using System.Linq;
using Core;

public class GameManager : MonoBehaviour
{
    [SerializeField] private string monsterTag = "Monster";
    [SerializeField] private float healthIncreasePerLamp = 0.1f; // 10%
    [SerializeField] private float sizeIncreasePerLamp = 0.1f; // 10%
    private GameObject monster;
    private HealthComponent monsterHealth;
    private Vector3 initialMonsterScale;
    private float initialMaxHealth;

    private static GameManager _instance;
    public static GameManager Instance { get { return _instance; } }

    private int brokenLampsCount = 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    void Start()
    {
        // Find the monster
        monster = GameObject.FindGameObjectWithTag(monsterTag);
        if (monster == null)
        {
            Debug.LogError("No GameObject tagged as " + monsterTag + " found in the scene.");
            return;
        }

        monsterHealth = monster.GetComponent<HealthComponent>();
        if (monsterHealth == null)
        {
            Debug.LogError("No HealthComponent found on the monster.");
            return;
        }

        initialMaxHealth = monsterHealth.MaxHealth;
        initialMonsterScale = monster.transform.localScale;

        Lamp.OnLampBroken += HandleLampBroken;
        Debug.Log("Game Manager Started");
    }

    private void HandleLampBroken(Lamp lamp)
    {
        brokenLampsCount++;
        UpdateMonsterStats();
    }

    private void UpdateMonsterStats()
    {
        if (monsterHealth == null) return;

        float healthIncreaseFactor = 1 + (brokenLampsCount * healthIncreasePerLamp);
        float newMaxHealth = initialMaxHealth * healthIncreaseFactor;
        monsterHealth.SetMaxHealth(newMaxHealth);

        // Scale the monster's size
        float sizeIncreaseFactor = 1 + (brokenLampsCount * sizeIncreasePerLamp);
        monster.transform.localScale = initialMonsterScale * sizeIncreaseFactor;
    }

    private void OnDestroy()
    {
        Lamp.OnLampBroken -= HandleLampBroken;
    }
}