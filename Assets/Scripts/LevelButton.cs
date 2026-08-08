using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelButton : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string nextScene;
    [SerializeField] private GameObject interactPanel;

    private Camera playerCamera;

    void Start()
    {
        playerCamera = Camera.main;
        interactPanel.SetActive(false);
    }

    void Update()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        bool canInteract = false;

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            if (hit.collider.gameObject == gameObject)
            {
                canInteract = true;

                if (Input.GetKeyDown(interactKey))
                {
                    CompleteLevel();
                }
            }
        }

        interactPanel.SetActive(canInteract);
    }

    void CompleteLevel()
    {
        UnlockNewLevel();
        SceneManager.LoadScene(nextScene);
    }

    void UnlockNewLevel()
    {
        if (SceneManager.GetActiveScene().buildIndex >= PlayerPrefs.GetInt("ReachedIndex"))
        {
            PlayerPrefs.SetInt("ReachedIndex", SceneManager.GetActiveScene().buildIndex + 1);
            PlayerPrefs.SetInt("UnlockedLevel", PlayerPrefs.GetInt("UnlockedLevel", 1) + 1);
            PlayerPrefs.Save();
        }
    }
}