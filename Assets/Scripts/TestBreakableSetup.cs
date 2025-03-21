using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using Core;

public class TestBreakableSetup : MonoBehaviour
{
    [Header("Breakable Object Settings")]
    public GameObject breakableBoxPrefab;
    public Transform spawnPoint;
    public float spawnHeight = 1f;
    public float spawnSpacing = 1f;
    public int gridSize = 3;

    [Header("Weapon Settings")]
    public GameObject weaponPrefab;
    public Transform weaponSpawnPoint;

    [Header("Debug UI")]
    public TextMeshProUGUI debugText;
    public bool showDebugInfo = true;

    [Header("Instant Breakable Objects")]
    [SerializeField] private GameObject instantBreakablePrefab;
    [SerializeField] private int instantBreakableCount = 3;
    [SerializeField] private Vector3 instantBreakableSpacing = new Vector3(1f, 0f, 1f);
    [SerializeField] private Transform instantBreakableSpawnPoint;
    
    [Header("Jiggle Breakable Objects")]
    [SerializeField] private GameObject jiggleBreakablePrefab;
    [SerializeField] private int jiggleBreakableCount = 3;
    [SerializeField] private Vector3 jiggleBreakableSpacing = new Vector3(1f, 0f, 1f);
    [SerializeField] private Transform jiggleBreakableSpawnPoint;
    
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnOrigin = new Vector3(0f, 1f, 0f);
    [SerializeField] private float objectScale = 1f;
    [SerializeField] private float objectMass = 1f;
    [SerializeField] private float objectHealth = 100f;
    [SerializeField] private float jiggleDuration = 1f;
    [SerializeField] private float jiggleIntensity = 0.5f;

    private void Awake()
    {
        Debug.Log("TestBreakableSetup: Awake called");
    }

    private void Start()
    {
        Debug.Log("TestBreakableSetup: Start called");
        
        if (spawnPoint == null)
        {
            Debug.LogWarning("TestBreakableSetup: No spawn point assigned, using this transform");
            spawnPoint = transform;
        }

        // Spawn initial grid of boxes
        SpawnBoxGrid();

        // Spawn weapon
        if (weaponPrefab != null && weaponSpawnPoint != null)
        {
            SpawnWeapon();
        }

        SpawnInstantBreakables();
        SpawnJiggleBreakables();
    }

    public void SpawnBoxGrid()
    {
        if (breakableBoxPrefab == null)
        {
            Debug.LogError("TestBreakableSetup: No breakable box prefab assigned!");
            return;
        }

        // Clear existing boxes
        var existingBoxes = GameObject.FindObjectsOfType<Core.InstantBreakableObject>();
        foreach (var box in existingBoxes)
        {
            Destroy(box.gameObject);
        }

        // Spawn new grid
        Vector3 centerOffset = new Vector3(
            -(gridSize - 1) * spawnSpacing * 0.5f,
            0,
            -(gridSize - 1) * spawnSpacing * 0.5f
        );

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 position = spawnPoint.position + centerOffset + new Vector3(
                    x * spawnSpacing,
                    spawnHeight,
                    z * spawnSpacing
                );

                GameObject box = Instantiate(breakableBoxPrefab, position, Quaternion.identity);
                box.name = $"BreakableBox_{x}_{z}";
            }
        }

        Debug.Log($"Spawned {gridSize * gridSize} breakable boxes in a grid");
    }

    public void SpawnWeapon()
    {
        if (weaponPrefab == null)
        {
            Debug.LogError("TestBreakableSetup: No weapon prefab assigned!");
            return;
        }

        GameObject weapon = Instantiate(weaponPrefab, weaponSpawnPoint.position, weaponSpawnPoint.rotation);
        Debug.Log("Spawned test weapon");
    }

    private void SpawnInstantBreakables()
    {
        if (instantBreakablePrefab == null)
        {
            Debug.LogError("TestBreakableSetup: No instant breakable prefab assigned!");
            return;
        }

        // Use spawn point if assigned, otherwise use spawn origin
        Vector3 spawnPosition = instantBreakableSpawnPoint != null ? instantBreakableSpawnPoint.position : spawnOrigin;

        for (int i = 0; i < instantBreakableCount; i++)
        {
            Vector3 position = spawnPosition + new Vector3(
                instantBreakableSpacing.x * i,
                0f,
                0f
            );

            GameObject obj = Instantiate(instantBreakablePrefab, position, Quaternion.identity);
            
            // Configure the breakable object
            if (obj.TryGetComponent<InstantBreakableObject>(out var breakable))
            {
                breakable.SetHealth(objectHealth);
                if (obj.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.mass = objectMass;
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }
        }

        Debug.Log($"Spawned {instantBreakableCount} instant breakable objects");
    }

    private void SpawnJiggleBreakables()
    {
        if (jiggleBreakablePrefab == null)
        {
            Debug.LogError("TestBreakableSetup: No jiggle breakable prefab assigned!");
            return;
        }

        // Use spawn point if assigned, otherwise use spawn origin
        Vector3 spawnPosition = jiggleBreakableSpawnPoint != null ? jiggleBreakableSpawnPoint.position : spawnOrigin;

        for (int i = 0; i < jiggleBreakableCount; i++)
        {
            Vector3 position = spawnPosition + new Vector3(
                jiggleBreakableSpacing.x * i,
                0f,
                0f
            );

            GameObject obj = Instantiate(jiggleBreakablePrefab, position, Quaternion.identity);
            obj.transform.localScale = Vector3.one * objectScale;
            
            // Configure the breakable object
            if (obj.TryGetComponent<JiggleBreakableObject>(out var breakable))
            {
                breakable.SetHealth(objectHealth);
                breakable.SetJiggleSettings(jiggleIntensity, jiggleDuration, 2f);
                if (obj.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.mass = objectMass;
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }
        }

        Debug.Log($"Spawned {jiggleBreakableCount} jiggle breakable objects");
    }

    private void Update()
    {
        if (showDebugInfo && debugText != null)
        {
            UpdateDebugText();
        }

        // Keyboard shortcuts for testing
        if (Input.GetKeyDown(KeyCode.R))
        {
            SpawnBoxGrid();
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            SpawnWeapon();
        }
        if (Input.GetKeyDown(KeyCode.I))
        {
            SpawnInstantBreakables();
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            SpawnJiggleBreakables();
        }
    }

    private void UpdateDebugText()
    {
        var boxes = GameObject.FindObjectsOfType<Core.InstantBreakableObject>();
        int intactBoxes = 0;
        foreach (var box in boxes)
        {
            if (!box.gameObject.GetComponent<Collider>().enabled) continue; // Skip broken boxes
            intactBoxes++;
        }

        string text = $"Intact Boxes: {intactBoxes}\n";
        text += "Controls:\n";
        text += "R - Respawn Boxes\n";
        text += "T - Spawn New Weapon\n";
        text += "I - Spawn Instant Breakables\n";
        text += "J - Spawn Jiggle Breakables\n";

        debugText.text = text;
    }
} 