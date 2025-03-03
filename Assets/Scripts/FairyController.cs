using UnityEngine;
using System.Collections;

public class FairyController : MonoBehaviour
{
    public Animator fairyAnimator;
    public AudioSource fairyVoice; // Add fairy language sound
    public GameObject treeOfLight;
    public RoomManager roomManager;
    private Vector3 seedPosition;

    void Start()
    {
        gameObject.SetActive(false); // Hidden until summoned
    }

    public void SummonFairy(Vector3 pos)
    {
        seedPosition = pos;
        gameObject.SetActive(true);
        transform.position = pos + Vector3.up * 2f; // Above seed
        StartCoroutine(FairySequence());
    }

    IEnumerator FairySequence()
    {
        fairyVoice.Play(); // Fairy language
        yield return new WaitForSeconds(3f); // Talk for 3 seconds
        fairyAnimator.SetBool("isSinging", true);
        treeOfLight.SetActive(true);
        treeOfLight.transform.position = seedPosition;
        yield return new WaitForSeconds(2f); // Delay before room clears
        // roomManager.ClearRoom();
        yield return new WaitForSeconds(118f); // Sing for ~2 minutes total
        BloomTree();
    }

    void BloomTree()
    {
        fairyAnimator.SetBool("isSinging", false);
        treeOfLight.transform.localScale *= 2f; // Bloom effect
        StartCoroutine(EndSequence());
    }

    IEnumerator EndSequence()
    {
        for (int i = 0; i < 3; i++) // Fly around tree 3 times
        {
            transform.RotateAround(treeOfLight.transform.position, Vector3.up, 360f);
            yield return new WaitForSeconds(1f);
        }
        roomManager.ResetRoom();
        gameObject.SetActive(false);
    }
}