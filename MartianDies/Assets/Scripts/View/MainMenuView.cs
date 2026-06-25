using AniDrag.EventBus;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] public TMP_InputField usernameInput;
    [SerializeField] public TMP_InputField serverIpInput;
    [SerializeField] public Button connectButton;
    [SerializeField] public Button resetIpAdres;

    EventBinding<IncorrectIP> incorectIpBinding;
    EventBinding<IncorrectUsername> emptyUsernameBinding;

    private string originalUsernameText;
    private string originalIpUsedText;

    void Start()
    {
        if (connectButton == null)
            connectButton = transform.Find("btn_Connect")?.GetComponent<Button>();
        if (connectButton == null)
            Debug.LogError("Connect button not found!");
        else
            connectButton.onClick.AddListener(() => EventBus<Connect>.Publish(new Connect()));

        if (resetIpAdres == null)
            resetIpAdres = transform.Find("BTN_DefaultIP")?.GetComponent<Button>();
        if (resetIpAdres == null)
            Debug.LogError("Reset IP button not found!");
        else
            resetIpAdres.onClick.AddListener(ResetIP);

        if (serverIpInput != null)
            serverIpInput.text = "127.0.0.1";

        incorectIpBinding = new EventBinding<IncorrectIP>(IncorectIp);
        emptyUsernameBinding = new EventBinding<IncorrectUsername>(IncorectUsername);
        EventBus<IncorrectIP>.Subscribe(incorectIpBinding);
        EventBus<IncorrectUsername>.Subscribe(emptyUsernameBinding);
    }

    public string GetUsername() => usernameInput != null ? usernameInput.text.Trim() : "";
    public string GetServerIp() => serverIpInput != null ? serverIpInput.text.Trim() : "";

    void ResetIP()
    {
        if (serverIpInput != null)
            serverIpInput.text = "127.0.0.1";
    }

    public void SetButtonsInteractable(bool interactable)
    {
        if (connectButton != null)
            connectButton.interactable = interactable;
        if (usernameInput != null)
            usernameInput.interactable = interactable;
        if (serverIpInput != null)              
            serverIpInput.interactable = interactable;
        if (resetIpAdres != null)                
            resetIpAdres.interactable = interactable;
    }

    private void OnDestroy()
    {
        if (connectButton != null)
            connectButton.onClick.RemoveAllListeners();
        if (resetIpAdres != null)
            resetIpAdres.onClick.RemoveAllListeners();
        EventBus<IncorrectIP>.Unsubscribe(incorectIpBinding);
        EventBus<IncorrectUsername>.Unsubscribe(emptyUsernameBinding);
    }

    private void IncorectUsername(IncorrectUsername e)
    {
        if (usernameInput == null) return;
        originalUsernameText = usernameInput.text;
        usernameInput.text = e.errorMsg;
        DelayedUsernameRestore();
    }

    private void DelayedUsernameRestore()
    {
        StopCoroutine(RestoreUsernameAfterDelay());
        StartCoroutine(RestoreUsernameAfterDelay());
    }

    private IEnumerator RestoreUsernameAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (usernameInput != null)
            usernameInput.text = originalUsernameText;
    }

    public void IncorectIp(IncorrectIP e)  
    {
        if (serverIpInput == null) return;
        originalIpUsedText = serverIpInput.text;
        serverIpInput.text = e.errorMsg;   
        DelayedIPAdressRestore();
    }

    private void DelayedIPAdressRestore()
    {
        StopCoroutine(RestoreIPAdressAfterDelay());
        StartCoroutine(RestoreIPAdressAfterDelay());
    }

    private IEnumerator RestoreIPAdressAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (serverIpInput != null) 
            serverIpInput.text = originalIpUsedText;
    }
}

/*
Q & A session – MainMenuView

Q1: What is the primary role of MainMenuView?
A1: It manages the main menu UI elements (input fields, buttons) and handles user interactions. It also 
    subscribes to error events (IncorrectIP, IncorrectUsername) to display feedback temporarily.

Q2: Why does the view publish a Connect event when the connect button is clicked, instead of calling a method directly?
A2: This follows the event-driven architecture. The view does not know about the controller; it simply 
    publishes an event. Any interested party (like MainMenuController) can subscribe and handle the 
    connection logic. This decouples the UI from the business logic, making the system more modular and 
    testable.

Q3: Why use EventBinding for IncorrectIP and IncorrectUsername?
A3: EventBinding is a wrapper that allows subscribing to EventBus events with strong typing and automatic 
    cleanup. It simplifies subscription management and ensures that the view reacts to these error events 
    without needing direct references to other components.

Q4: Why are UI elements found dynamically using transform.Find if not assigned in the Inspector?
A4: This provides flexibility – if the scene structure changes, the view can still locate the buttons 
    by name. However, it's a fallback; the Inspector assignment is preferred. This pattern is common in 
    Unity for prefabs where the hierarchy is known.

Q5: Why is the IP address default set to "127.0.0.1" in Start?
A5: The loopback address is a sensible default for local testing. It saves the user from typing it 
    repeatedly during development. The reset button allows quick restoration if the user changes it.

Q6: What is the purpose of SetButtonsInteractable(bool interactable)?
A6: This method allows the controller to disable all UI inputs during connection attempts (or timeouts) 
    to prevent multiple clicks. It centralises interactivity control, ensuring all relevant elements are 
    toggled together.

Q7: Why do we use coroutines (DelayedUsernameRestore) to restore the original text after displaying an error?
A7: When an error occurs (e.g., "Empty username"), we temporarily replace the input text with the error 
    message for 2 seconds, then revert. This provides visual feedback without cluttering the UI with 
    persistent error labels. The coroutine handles the delay, and we ensure only one coroutine runs at a 
    time by stopping any previous instance.

Q8: Why store originalUsernameText and originalIpUsedText as fields?
A8: We need to remember the user's original input to restore it after showing the error message. Storing 
    these values in fields ensures they persist across the coroutine execution.

Q9: How do we prevent multiple coroutines from running simultaneously?
A9: In DelayedUsernameRestore, we call StopCoroutine before starting a new one. This cancels any pending 
    restore operation, ensuring that only the most recent error message will be shown and then reverted.

Q10: Why is there a separate DelayedIPAdressRestore method, similar to the username one?
A10: The logic for IP address restoration is identical in structure but operates on a different field. 
    Keeping them separate (rather than a generic method) improves readability and allows different 
    behaviours in the future if needed.

Q11: Why use OnDestroy to remove event listeners and button listeners?
A11: Cleanup is essential to prevent memory leaks and stale subscriptions. When the view is destroyed, 
    we unsubscribe from all events and remove button click listeners to avoid calls on null references.

Q12: How does this view interact with asynchronous operations (e.g., connection)?
A12: The view itself does not contain any async code. It only publishes events and reacts to error events. 
    The asynchronous work (TCP connection, registration) is handled by the Client and MainMenuController. 
    This separation of concerns keeps the view simple and focused on UI presentation.

Q13: Why does the view not handle the success case (registration success)?
A13: Success leads to a scene transition (Lobby), which is orchestrated by MainMenuController. The view 
    does not need to know about that; it only deals with visible UI elements. This keeps the view 
    lightweight and maintainable.

Q14: Why do we check for null references (e.g., if (usernameInput == null))?
A14: This is defensive programming. In case the Inspector references are missing, we avoid 
    NullReferenceException crashes and gracefully handle the situation. It also allows dynamic 
    finding as a fallback.

Q15: What is the advantage of using TMP_InputField instead of the legacy InputField?
A15: TextMeshPro (TMP) provides better text rendering, more styling options, and improved performance. 
    It's the recommended UI text component in modern Unity projects.

Q16: Why is the reset IP button assigned to a separate field and listener?
A16: The reset IP button is an extra convenience feature. By having a dedicated listener, we keep the 
    logic separate from the connect button, making the code easier to modify.

Q17: How would you extend this view to support different error types or more complex feedback?
A17: We could add more error event types (e.g., IncorrectPort, ServerFull) and handle them similarly. 
    Alternatively, we could use a generic error event with a message string and display it in a dedicated 
    label. The current approach is simple but limited to two specific errors.

Q18: Why does the view reset the input fields only after a delay, not immediately?
A18: Showing the error message directly in the input field provides immediate, in-place feedback. If we 
    cleared it instantly, the user might miss the reason. A 2?second delay gives enough time to read the 
    message before it reverts.

Q19: What about the async/await pattern – is it used anywhere in this script?
A19: No, this script is purely synchronous UI logic. The async operations are encapsulated within the 
    Client class (Connect using async/await) and the MainMenuController (which uses the Client). The 
    view remains oblivious to this, adhering to the Single Responsibility Principle.

Q20: How do the error events (IncorrectIP, IncorrectUsername) get published?
A20: They are published by MainMenuController when validation fails (e.g., empty username). The view 
    subscribes to these events and updates the UI accordingly. This is a classic example of the 
    EventBus pattern in action.
*/