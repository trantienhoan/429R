using UnityEngine;

public class MagicalSeedManager : MonoBehaviour
{
    public static MagicalSeedManager Instance { get; private set; }
    
    [SerializeField] private GameObject magicalSeedPrefab;
    private bool hasSeedBeenFound = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnSeedFound()
    {
        hasSeedBeenFound = true;
    }

    public bool HasSeedBeenFound()
    {
        return hasSeedBeenFound;
    }
} 