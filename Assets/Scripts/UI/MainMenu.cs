using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Attach to an empty GameObject in the MainMenu scene.
// Wire up each button's OnClick in the Inspector to the matching method below.
public class MainMenu : MonoBehaviour
{
    [Header("Buttons")]
    public Button continueButton;

    [Header("Panels")]
    public GameObject optionsPanel; // Assign an empty Panel — populated later

    void Start()
    {
        // Disable Continue if there is no save file to load.
        if (continueButton != null)
            continueButton.interactable = SaveManager.SaveExists();

        // Options panel starts hidden.
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    // --- Button callbacks (wire these up in the Inspector) ---

    public void OnNewGame()
    {
        SaveManager.NewGameRequested = true;
        SceneManager.LoadScene("Game");
    }

    public void OnContinue()
    {
        SaveManager.NewGameRequested = false;
        SceneManager.LoadScene("Game");
    }

    public void OnOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(!optionsPanel.activeSelf);
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
