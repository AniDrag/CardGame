using AniDrag.EventBus;
using OSCTools;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private MainMenuView view;

    EventBinding<Connect> connectBinding;


    private void Start()
    {
        if (Client.Instance == null)
        {
            Debug.LogError("Client instance not found!");
            return;
        }

        // Subscribe to event bus with correct signature
        connectBinding = new EventBinding<Connect>(ConnectClicked);
        EventBus<Connect>.Subscribe(connectBinding);

        // Subscribe to client connection event
        Client.Instance.OnConnected += OnConnect;

        Client.Log("Loaded Main Menu");
    }

    private void ConnectClicked(Connect e)
    {
        string username = view.GetUsername();
        string ip = view.GetServerIp();

        if (string.IsNullOrEmpty(username))
        {
            Client.Log("Connection attempt failed: empty username.");
            EventBus<IncorrectUsername>.Publish(new IncorrectUsername("Empty username."));
            return;
        }
        else if (username.Length > 13)
        {
            Client.Log("Connection attempt failed: username too long.");
            EventBus<IncorrectUsername>.Publish(new IncorrectUsername("Username too long."));
            return;
        }
        if (string.IsNullOrEmpty(ip))
        {
            Client.Log("Connection attempt failed: empty IP.");
            EventBus<IncorrectIP>.Publish(new IncorrectIP("Empty IP."));
            return;
        }

        Client.Log($"Connecting to {ip}...");
        Client.Instance.Connect(ip, Msg.PORT);
    }

    private void OnConnect()
    {
        string username = view.GetUsername();

        // Add listener for registration reply
        Client.Instance.AddListener(Msg.S_REGISTERED, OnRegistered, OSCUtil.INT, OSCUtil.STRING);
        Client.Log("Debug", "Registered listener for S_REGISTERED");

        // Start timeout for registration
        Client.Instance.StartTimeout(Msg.REGISTER_TIMEOUT_ID, 10f, () =>
        {
            Client.Log("Registration timeout – disconnecting");
            view.SetButtonsInteractable(true);
            Client.Instance.Disconnect();
        });

        // Send registration
        OSCMessageOut regMsg = new OSCMessageOut(Msg.C_REGISTER);
        regMsg.AddString(username);
        Client.Instance.Send(regMsg);
    }

    private void OnRegistered(OSCMessageIn msg, IPEndPoint sender)
    {
        Client.Log("Debug", "OnRegistered ENTERED");

        Client.Instance.CancelTimeout(Msg.REGISTER_TIMEOUT_ID);
        int id = msg.ReadInt();
        Client.Instance.Username = msg.ReadString();
        Client.Log($"Registration successful! Server assigned ID {id} to {name}");
        Client.Instance.RemoveListener("/registered", OnRegistered);
        SceneManager.LoadSceneAsync(Scenes.Lobby);
    }

    private void OnDisable()
    {
        EventBus<Connect>.Unsubscribe(connectBinding);
        if (Client.Instance != null)
            Client.Instance.OnConnected -= OnConnect;
    }
}

/*
Q & A session – MainMenuController

Q1: What is the primary responsibility of MainMenuController?
A1: It manages the main menu UI, handles user input (username, IP), initiates the connection to the server,
    and orchestrates the registration process. Once registration succeeds, it transitions to the lobby scene.

Q2: Why use EventBus for the Connect event instead of a direct UI button callback?
A2: The UI button publishes a Connect event via EventBus, and MainMenuController subscribes to it. This
    decouples the view (MainMenuView) from the controller. The controller doesn't need to know which
    button was pressed; it just handles the event. This makes the code more maintainable and testable.

Q3: Why does ConnectClicked check username length and IP before calling Client.Instance.Connect?
A3: Basic client-side validation prevents unnecessary network requests. The server would also validate,
    but early checks improve user experience (instant feedback) and reduce server load. The events
    IncorrectUsername and IncorrectIP are published to notify the UI to show error messages.

Q4: Why is the registration process split into two steps: connecting and then registering?
A4: The connection is a TCP handshake; after it succeeds, the client must send a registration message
    (C_REGISTER) to the server to obtain a session ID and username. The server replies with S_REGISTERED.
    This two-step process allows the server to reject invalid usernames without closing the connection.

Q5: Why use OnConnect (Client.OnConnected) to send registration instead of doing it immediately after Connect?
A5: Connect is asynchronous – it doesn't block until the connection is established. OnConnect is an event
    that fires once the connection is fully open. At that point, we can safely send the registration message.
    Doing it immediately after calling Connect() would send the message before the connection is ready.

Q6: What is the purpose of adding an OSC listener for S_REGISTERED before sending the registration?
A6: We register the listener for the expected response before sending the request. This ensures that when
    the server replies, the callback (OnRegistered) will be invoked. If we registered after sending,
    we might miss the response if it arrives quickly.

Q7: Why is there a timeout (StartTimeout) for registration, and how does it work?
A7: Network operations can hang if the server is slow or unresponsive. The timeout ensures that the
    registration attempt doesn't block forever. StartTimeout uses a coroutine that waits a given duration
    and then invokes a callback. In this case, it logs a message, re-enables buttons, and disconnects the
    client, allowing the user to retry.

Q8: Why is the timeout cancelled on successful registration (CancelTimeout)?
A8: If registration succeeds, the timeout coroutine should not fire. Cancelling it prevents a race condition
    where the timeout callback might execute after a delayed success, causing an unnecessary disconnect.

Q9: Why remove the S_REGISTERED listener after receiving the response?
A9: Once registration is complete and the scene transitions to the lobby, the S_REGISTERED listener is no
    longer needed. Removing it prevents memory leaks and avoids accidental callbacks if the server sends
    another S_REGISTERED later (which it shouldn't). Cleanup is good practice.

Q10: Why load the lobby scene with SceneManager.LoadSceneAsync instead of LoadScene?
A10: LoadSceneAsync loads the scene in the background without freezing the main thread. This provides a
     smoother transition, especially if the scene is large. The async loading also allows additional
     operations (like data preloading) while the scene loads. It's the preferred method for Unity.

Q11: How does the client handle asynchronous operations overall (in Client.Connect)?
A11: Client.Connect uses async/await. It creates a TcpNetworkConnection asynchronously, then awaits
     Task.Delay in a loop to check connection status with a timeout. This is non-blocking; the main thread
     continues to run. The method is marked async void because it's an event handler; exceptions are logged
     in a try-catch. This pattern is suitable for fire-and-forget operations with error handling.

Q12: Why use async/await instead of coroutines for network operations?
A12: async/await provides a more natural, linear programming model for asynchronous tasks. It avoids the
     complexity of yield returns and manually tracking coroutine state. It also integrates well with
     Task-based operations (e.g., Task.Delay). However, Unity's main thread requires care – async methods
     continue on a thread pool by default, but we often use them for I/O-bound operations that don't touch
     Unity APIs. In Client.Connect, the connection status check is a simple while loop with Task.Delay,
     which is safe.

Q13: What are the pitfalls of async void in Unity?
A13: async void methods cannot be awaited; they are fire-and-forget. Exceptions thrown inside them are
     not caught by the caller and may crash the application if unhandled. That's why we wrap the async
     logic in try-catch and log errors. Additionally, the calling context (e.g., MonoBehaviour) may be
     destroyed before the async method completes, leading to null references. We avoid accessing
     Unity objects after awaits if they might be destroyed.

Q14: How does the timeout mechanism (StartTimeout) relate to async operations?
A14: StartTimeout uses a coroutine – a Unity-specific construct that runs on the main thread and can yield
     for frames or time. It's not async/await, but serves a similar purpose: delaying execution and
     providing a callback. The choice between coroutine and async/await often depends on whether the
     operation needs to interact with Unity APIs (coroutines are safer for that) or perform I/O (async/await
     is more flexible). Here, timeouts are purely time-based, so a coroutine is fine.

Q15: Why does the controller handle disconnection and scene transition on registration timeout?
A15: If registration times out, the server is likely unreachable or misbehaving. Instead of leaving the
     user in a stuck state, we disconnect and re-enable the UI so they can try again. The timeout callback
     is an example of graceful error recovery.

Q16: Why does the MainMenuController clean up event subscriptions in OnDisable?
A16: To prevent memory leaks and duplicate subscriptions. If the object is disabled or destroyed, we
     unsubscribe from both the EventBus and the Client's OnConnected event. This is a standard practice
     in Unity to avoid stale callbacks.

Q17: How does the event bus help with showing error messages (IncorrectUsername, IncorrectIP)?
A17: The controller publishes these events when validation fails. Other components (likely the view or a
     UI manager) subscribe to these events to display error popups. This keeps the controller focused on
     logic rather than directly manipulating UI elements.

Q18: What is the significance of the registration timeout ID ("REGISTER_TIMEOUT_ID")?
A18: It's a unique identifier used to start and cancel the specific timeout. This allows the controller to
     have multiple concurrent timeouts with different IDs, each with its own callback. It prevents cancelling
     the wrong timeout.

Q19: Why is the username stored in Client.Instance.Username after registration?
A19: Storing it centrally allows other parts of the application (e.g., lobby, game) to access the current
     user's name without passing it around. The server assigns and confirms the username; this is the
     canonical value.

Q20: How does the whole connection and registration flow work in terms of asynchronous events?
A20: The flow is:
     1. User clicks connect ? MainMenuController publishes Connect event.
     2. ConnectClicked is called ? validates input, calls Client.Instance.Connect (async).
     3. Client connects (maybe async) ? fires OnConnected event when done.
     4. OnConnect callback is invoked ? adds OSC listener, starts timeout, sends C_REGISTER.
     5. Server replies with S_REGISTERED ? OnRegistered is called.
     6. OnRegistered cancels timeout, reads ID and username, removes listener, loads lobby scene.
     All these steps are decoupled via events and timeouts, making the system responsive and robust.
*/