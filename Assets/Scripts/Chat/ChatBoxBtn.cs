using UnityEngine;
using UnityEngine.UI;

public class ChatBoxBtn : MonoBehaviour
{
    private Button btn;
    [SerializeField] GameObject Target;

    private void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(TriggerButton);
    }
    void TriggerButton()
    {
        if (Target != null)
        {
            Target.SetActive(!Target.activeSelf);
        }
    }
    private void OnDestroy()
    {
        btn.onClick.RemoveListener(TriggerButton);
    }
}

