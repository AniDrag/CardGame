using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CheckBoxbehaviour : MonoBehaviour
{
    public UnityEvent ONActive;
    public UnityEvent DEActive;

    [SerializeField] Image checkMark;

    bool isTicked = false;

    public void ClickCheckMark()
    {
        isTicked = !isTicked;

        checkMark.gameObject.SetActive(isTicked);

        if (isTicked)
        {
            ONActive?.Invoke();
        }
        else
        {
            DEActive?.Invoke();
        }
    }
}

