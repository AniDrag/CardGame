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
        FindMissingReferences();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        Hide();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    private void FindMissingReferences()
    {
        if (panel == null)
        {
            panel = ViewAutoFind.FindGameObject(transform, "Panel_Announcements", "Panel_Announcement");

            if (panel == null && name.Contains("Panel_"))
                panel = gameObject;
        }

        Transform searchRoot = panel != null ? panel.transform : transform;

        if (anouncmentText == null)
            anouncmentText = ViewAutoFind.FindFirstComponent<TMP_Text>(searchRoot);

        if (closeButton == null)
        {
            closeButton = ViewAutoFind.FindComponentByNames<Button>(searchRoot, "btn_Close", "BTN_Close", "Close", "Button_Close");

            if (closeButton == null)
                closeButton = ViewAutoFind.FindComponentContainingAll<Button>(searchRoot, "close");
        }
    }

    public void Show(string message, bool autoHide = true)
    {
        FindMissingReferences();

        if (anouncmentText != null)
            anouncmentText.text = message;

        if (panel == null)
        {
            Client.Log("AnnouncementsView", "Panel reference missing. Cannot show announcement: " + message);
            return;
        }

        panel.SetActive(true);

        CancelInvoke(nameof(Hide));

        if (autoHide)
            Invoke(nameof(Hide), autoHideDelay);
    }

    public void Hide()
    {
        FindMissingReferences();

        if (panel != null)
            panel.SetActive(false);
    }
}
