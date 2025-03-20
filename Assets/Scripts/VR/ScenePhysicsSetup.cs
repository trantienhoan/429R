using UnityEngine;

public class ScenePhysicsSetup : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private PhysicsSetup physicsSetup;
    [SerializeField] private bool setupOnStart = true;
    
    [Header("Object Tags")]
    [SerializeField] private string[] pushableTags = { "Pushable", "SmallObject", "Item" };
    [SerializeField] private string[] wallTags = { "Wall", "Door" };

    private void Start()
    {
        if (setupOnStart)
        {
            SetupSceneObjects();
        }
    }

    public void SetupSceneObjects()
    {
        if (physicsSetup == null)
        {
            Debug.LogError("PhysicsSetup reference is missing!");
            return;
        }

        // Set up pushable objects
        foreach (string tag in pushableTags)
        {
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in objects)
            {
                physicsSetup.SetupPushableObject(obj);
            }
        }

        // Set up wall objects
        foreach (string tag in wallTags)
        {
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in objects)
            {
                physicsSetup.SetupWallObject(obj);
            }
        }
    }
} 