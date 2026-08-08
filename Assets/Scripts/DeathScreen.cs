using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathScreen : MonoBehaviour
{
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private FirstPersonController playerController;

    private bool isDead = false;

    void Start()
    {
        deathScreen.SetActive(false);
    }

    void Update()
    {
        if (isDead)
            return;

        if (playerController.transform.position.y < -20f)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;

        deathScreen.SetActive(true);

        Time.timeScale = 0;

        playerController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Retry()
    {
        Time.timeScale = 1;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }
}