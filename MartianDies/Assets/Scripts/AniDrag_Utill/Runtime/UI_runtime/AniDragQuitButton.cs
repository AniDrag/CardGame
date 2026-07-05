using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AniDrag.Utility.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class AniDragQuitButton : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private Button button;

        [Header("WebGL / Browser")]
        [SerializeField] private bool hideInWebGL = true;

        [Tooltip("If true, the button is disabled instead of hidden.")]
        [SerializeField] private bool disableInsteadOfHide = false;

        [Header("Debug")]
        [SerializeField] private bool logQuit = true;

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            HandleWebGLVisibility();

            if (button != null)
                button.onClick.AddListener(QuitGame);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(QuitGame);
        }

        private void HandleWebGLVisibility()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!hideInWebGL)
                return;

            if (disableInsteadOfHide)
            {
                if (button != null)
                    button.interactable = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
#endif
        }

        public void QuitGame()
        {
            if (logQuit)
                Debug.Log("[AniDragQuitButton] Quit button pressed.");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            // WebGL runs inside a browser.
            // Browsers do not allow Unity to close the tab normally.
            // This should normally be hidden instead.
            Debug.Log("[AniDragQuitButton] Quit ignored in WebGL/browser build.");
#else
            Application.Quit();
#endif
        }
    }
}