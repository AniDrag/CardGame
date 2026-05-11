using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundResultsView : MonoBehaviour
{
    [SerializeField] private TMP_Text resultsText;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panel;

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        Hide();
    }

    public void ShowResults(string results)
    {
        resultsText.text = results;
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
}