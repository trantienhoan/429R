using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using Core;
public class Bomb : MonoBehaviour
{
    [SerializeField] private float delay = 2f; // Shortened delay for faster testing
    [SerializeField] private string sceneToRestart = "Level1"; // Name of the scene to restart

    private bool _isArmed = false;

    void OnCollisionEnter(Collision collision)
    {
        // Check if the bomb collided with the Player
        if (collision.gameObject.CompareTag("Player"))
        {
            ArmBomb(); // No need to pass the player
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the bomb entered with the Player
        if (other.gameObject.CompareTag("Player"))
        {
            ArmBomb(); // No need to pass the player
        }
    }

    private void ArmBomb()
    {
        if (!_isArmed)
        {
            _isArmed = true;
            StartCoroutine(Explode()); // No need to pass the player
        }
    }

    private IEnumerator Explode()
    {
        yield return new WaitForSeconds(delay);

        RestartGame(); // Call the restart function
    }


    private void RestartGame()
    {
        SceneManager.LoadScene(sceneToRestart); // Load the specified scene
    }
}
