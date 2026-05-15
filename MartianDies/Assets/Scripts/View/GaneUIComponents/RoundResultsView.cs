using AniDrag.EventBus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundResultsView : MonoBehaviour
{
    [SerializeField] private TMP_Text resultsText;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panel;

    EventBinding<RoundResults> roundResultsBinding;

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        roundResultsBinding = new EventBinding<RoundResults>(ShowResults);
        EventBus<RoundResults>.Subscribe(roundResultsBinding);
        Hide();
    }

    public void ShowResults(RoundResults e)
    {
        resultsText.text = e.msg;
        gameObject.SetActive(true);

        // hide outomaticly after 5s
        Invoke(nameof(Hide), 5);
    }

    public void Hide() => gameObject.SetActive(false);
}