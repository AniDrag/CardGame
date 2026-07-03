using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnnouncementsView : MonoBehaviour
{
    [SerializeField] private TMP_Text anouncmentText;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private float autoHideDelay = 3f;

    private void Start()
    {
        if (panel == null)
        {
            Client.Log("AnnouncementsView", "Panel reference missing. Using this gameObject.");
            panel = transform.Find("Panel_Announcements").gameObject;
        }
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        Hide();
    }

    public void Show(string message, bool autoHide = true)
    {
        if (panel == null)
        {
            Client.Log("AnnouncementsView", "Panel reference missing. Using this gameObject.");
            panel = transform.Find("Panel_Announcements").gameObject;
        }
        anouncmentText.text = message;
        panel.SetActive(true);
        if (autoHide) 
            Invoke(nameof(Hide), autoHideDelay);
    }

    public void Hide() => panel.SetActive(false);
}