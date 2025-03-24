using UnityEngine;

public class TestTreeDamage : MonoBehaviour
{
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private KeyCode damageKey = KeyCode.Space;

    private TreeOfLight treeOfLight;

    private void Start()
    {
        treeOfLight = Object.FindFirstObjectByType<TreeOfLight>();
        if (treeOfLight == null)
        {
            Debug.LogWarning("No TreeOfLight found in the scene!");
        }
    }

    private void Update()
    {
        // Press the specified key to damage the tree
        if (Input.GetKeyDown(damageKey) && treeOfLight != null)
        {
            treeOfLight.TakeDamage(damageAmount);
            Debug.Log($"Tree took {damageAmount} damage. Current health: {treeOfLight.GetHealthPercentage() * 100}%");
        }
    }

    public void DamageTree()
    {
        if (treeOfLight != null)
        {
            treeOfLight.TakeDamage(damageAmount);
        }
    }
} 