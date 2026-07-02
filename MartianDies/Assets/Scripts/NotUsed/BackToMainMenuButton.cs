using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BackToMainMenuButton : MonoBehaviour
{
    #region View References

    [SerializeField] private Button backButton;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (backButton == null)
            backButton = GetComponent<Button>();

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
    }

    #endregion

    #region UI Events

    private void OnBackClicked()
    {
        SceneManager.LoadSceneAsync(Scenes.MainMenu);
    }

    #endregion
}