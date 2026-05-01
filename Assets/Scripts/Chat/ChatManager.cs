using UnityEngine;

public class ChatManager : MonoBehaviour
{
    [SerializeField] private Transform chatContentTransform;
    [SerializeField] private GameObject chatMessagePrefab;
    private void OnValidate()
    {
        AutoFind();
    }

    private void Awake()
    {
        //One more time during Awake to ensure everything is set up before the game starts and to catch any changes made in the editor
        AutoFind();
        if (chatContentTransform == null)
        {
            chatContentTransform = transform.Find("ChatContent");
            if (chatContentTransform == null)
            {
                Debug.LogError("ChatContentTransform is missing! Create a child GameObject named 'ChatContent'.");
                enabled = false;
                return;
            }
        }

        if (chatMessagePrefab == null)
        {
            Debug.LogError("ChatMessagePrefab is not assigned! Please assign it in the Inspector or via Resources.");
            enabled = false;
            return;
        }

        Debug.Log("ChatManager initialized.");
        SendMessage("Welcome to the chat!", "System");
    }

    public void SendMessage(string message, string sender)
    {
        Instantiate(chatMessagePrefab, chatContentTransform)
            .GetComponent<ChatMessage>()
            .SetMessage(message, sender);
        Debug.Log($"Sending message from {sender}: {message}");
    }

    void AutoFind()
    {
        // Automatically find and assign ChatContent when script is loaded or values change in editor
        if (chatContentTransform == null)
        {
            chatContentTransform = transform.Find("ChatContent");
            if (chatContentTransform == null)
                Debug.LogWarning("ChatContent not found as child. Please create an object named 'ChatContent'.");
        }

        // Optionally assign a default prefab if none is set
        if (chatMessagePrefab == null)
        {
            // Try to load from Resources or assign a known default
            chatMessagePrefab = Resources.Load<GameObject>("Prefabs/Message_Prf");
            if (chatMessagePrefab == null)
                Debug.LogWarning("ChatMessagePrefab not assigned and no default found in Resources/Prefabs/Message_Prf");
        }
    }
}