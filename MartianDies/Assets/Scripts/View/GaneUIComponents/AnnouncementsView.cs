using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnnouncementsView : MonoBehaviour
{
    [SerializeField] private TMP_Text anouncmentText;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panel;
    private float autoHideDelay = 3f;

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        Hide();
    }

    public void ShowAnnouncement(string message, bool autoHide = true)
    {
        anouncmentText.text = message;
        gameObject.SetActive(true);
        if (autoHide) 
            Invoke(nameof(Hide), 3f);
    }

    public void Hide() => gameObject.SetActive(false);
}