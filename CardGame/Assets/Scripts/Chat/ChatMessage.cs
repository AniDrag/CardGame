using TMPro;
using UnityEngine;

public class ChatMessage : MonoBehaviour
{
    [SerializeField] private TMP_Text senderText;
    [SerializeField] private TMP_Text messageText;

    public void SetMessage(string message, string sender)
    {
        if(senderText != null)
        {
            senderText.text = $"User: {sender}";
        }
        if (messageText != null)
        {
            messageText.text = message;
        }
    }
}