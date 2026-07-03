using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClientView : MonoBehaviour
{
    [SerializeField] private GameObject ConsolePanel;
    [SerializeField] private Button OpenConsoleBtn;
    [SerializeField] private Button CloseConsoleBtn;
    [SerializeField] private TMP_Text ConsoleText;

    private bool consoleVisible = false;
    
    private void Start()
    {
        // Find UI elements (your existing find logic)
        
        if (OpenConsoleBtn == null)
        {
            var btnTransform = transform.Find("btn_OpenConsole")?.gameObject;
            if (btnTransform != null) OpenConsoleBtn = btnTransform.GetComponent<Button>();
            else Debug.LogError("Button 'btn_OpenConsole' not found!");
        }

        if (CloseConsoleBtn == null)
        {
            var btnTransformClose = transform.Find("btn_CloseConsole")?.gameObject;
            if (btnTransformClose != null) CloseConsoleBtn = btnTransformClose.GetComponent<Button>();
            else Debug.LogError("Button 'btn_CloseConsole' not found!");
        } 

        if (ConsolePanel == null)
        {
            var panelTransform = transform.Find("Panel_Console")?.gameObject;
            if (panelTransform != null) ConsolePanel = panelTransform;
            else Debug.LogError("Panel 'Panel_Console' not found!");
        }

        if (ConsolePanel != null)
        {
            var textTransform = ConsolePanel.transform.Find("txt_Console");
            if (textTransform != null) ConsoleText = textTransform.GetComponent<TMP_Text>();
        }

        SafetyChecks();
        ToggleConsole(false);

        // Subscribe to global console logs
        Client.OnConsoleLog += AppendMessage;
    }

    private void OnEnable()
    {
        if (OpenConsoleBtn != null) OpenConsoleBtn.onClick.AddListener(OpenConsole);
        if (CloseConsoleBtn != null) CloseConsoleBtn.onClick.AddListener(CloseConsole);
    }

    private void OnDisable()
    {
        if (OpenConsoleBtn != null) OpenConsoleBtn.onClick.RemoveListener(OpenConsole);
        if (CloseConsoleBtn != null) CloseConsoleBtn.onClick.RemoveListener(CloseConsole);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) ToggleConsole(!consoleVisible);
    }

    private void OpenConsole() => ToggleConsole(true);
    private void CloseConsole() => ToggleConsole(false);
    private void ToggleConsole(bool show)
    {
        consoleVisible = show;
        if (OpenConsoleBtn != null) OpenConsoleBtn.gameObject.SetActive(!show);
        if (CloseConsoleBtn != null) CloseConsoleBtn.gameObject.SetActive(show);
        if (ConsolePanel != null) ConsolePanel.SetActive(show);
    }

    private void AppendMessage(string msg)
    {
        if (ConsoleText != null)
            ConsoleText.text += msg + "\n";
    }

    private void SafetyChecks()
    {
        if (Client.Instance == null) Debug.LogError("Client instance missing!");
        if (OpenConsoleBtn == null) Debug.LogError("OpenConsoleBtn missing!");
        if (CloseConsoleBtn == null) Debug.LogError("CloseConsoleBtn missing!");
        if (ConsolePanel == null) Debug.LogError("ConsolePanel missing!");
        if (ConsoleText == null) Debug.LogError("ConsoleText missing!");
    }

    private void OnDestroy()
    {
        Client.OnConsoleLog -= AppendMessage;
        if (OpenConsoleBtn != null) OpenConsoleBtn.onClick.RemoveAllListeners();
        if (CloseConsoleBtn != null) CloseConsoleBtn.onClick.RemoveAllListeners();
    }
}