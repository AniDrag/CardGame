using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

public class TextClient : MonoBehaviour
{
	[SerializeField]
	TextMeshProUGUI textField;
	[SerializeField]
	TMP_InputField inputField;

	const int maxLinesDisplay = 22;
	List<string> lines = new List<string>();

	// This method is called from an input field event:
	public void OnMessageEntered()
	{
		// TODO: Instead of displaying the message directly, send it to the server here:
		DisplayMessage(inputField.text);

		// Clear the input field, and activate it again for the next user input:
		inputField.text = "";
		inputField.ActivateInputField();
		inputField.Select();
	}

	/// <summary>
	/// Adds new text to the text display, while ensuring the total number of lines 
	/// doesn't exceed the maximum.
	/// </summary>
	void DisplayMessage(string text)
	{
		string[] newLines = text.Split('\n');
		lines.AddRange(newLines);
		if (lines.Count > maxLinesDisplay)
		{
			lines.RemoveRange(0, lines.Count - maxLinesDisplay);
		}
		textField.text = "";
		foreach (string line in lines)
		{
			textField.text += line + '\n';
		}
	}

	void Start()
	{
		// TODO: create a Udp or Tcp client to communicate with a server
	}

	void Update()
	{
		// TODO: check the Udp or Tcp client for available incoming messages.
		// If there are any, decode and display them.
	}

	public void RequestActionToServer()
	{
		/*
		 A switchcase to manage what tge player decided  and it should serialize data to send to server.
		 */

	}
}
