using UnityEngine;
using System.Linq;
using Enemies; // <-- Needed to access ShadowMonster

public class GameManager : MonoBehaviour
{
    [Header("Monster Settings")]
    [SerializeField] private string monsterTag = "Monster";
    [SerializeField] private float healthIncreasePerLamp = 0.1f; // 10% per lamp
    [SerializeField] private float sizeIncreasePerLamp = 0.1f;    // 10% per lamp

    private GameObject monster;
    private ShadowMonster monsterScript;
    private Vector3 initialMonsterScale;
    private float initialMaxHealth = 100f; // Default starting health

    private static GameManager _instance;
    public static GameManager Instance => _instance;

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

    private void Start()
    {
        monster = GameObject.FindGameObjectWithTag(monsterTag);
        if (monster == null)
        {
            Debug.LogError($"No GameObject tagged as \"{monsterTag}\" found in the scene.");
            return;
        }

        monsterScript = monster.GetComponent<ShadowMonster>();
        if (monsterScript == null)
        {
            Debug.LogError("No ShadowMonster script found on the monster.");
            return;
        }

        initialMonsterScale = monster.transform.localScale;
        initialMaxHealth = 100f; // Update this if your monster has another default

        Lamp.OnLampBroken += HandleLampBroken;
        Debug.Log("Game Manager Started.");
    }

    private void HandleLampBroken(Lamp lamp)
    {
        brokenLampsCount++;
        UpdateMonsterStats();
    }

    private void UpdateMonsterStats()
    {
        if (monsterScript == null) return;

        // Update health
        float healthMultiplier = 1 + (brokenLampsCount * healthIncreasePerLamp);
        float newMaxHealth = initialMaxHealth * healthMultiplier;
        monsterScript.SetMaxHealth(newMaxHealth);

        // Update size
        float sizeMultiplier = 1 + (brokenLampsCount * sizeIncreasePerLamp);
        monster.transform.localScale = initialMonsterScale * sizeMultiplier;

        Debug.Log($"Updated Monster: Health = {newMaxHealth}, Scale = {monster.transform.localScale}");
    }

    private void OnDestroy()
    {
        Lamp.OnLampBroken -= HandleLampBroken;
    }
}