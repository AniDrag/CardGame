using UnityEngine;
using UnityEngine.UI;

public class ConfirmChoiceView : MonoBehaviour
{
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private GameObject panel;

    private System.Action<bool> onChoice;

    private void Start()
    {
        yesButton.onClick.AddListener(() => { onChoice?.Invoke(true); Hide(); });
        noButton.onClick.AddListener(() => { onChoice?.Invoke(false); Hide(); });
        Hide();
    }

    public void Show(string question, System.Action<bool> callback)
    {
        onChoice = callback;
        panel.SetActive(true);
    }

    public void Hide() => panel.SetActive(false);
}