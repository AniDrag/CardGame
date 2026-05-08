using System;
using UnityEngine;

public class TempGameplay : MonoBehaviour
{
    public TempGameplay instance;
    private void Awake()
    {
        if(instance == null)
            instance = this;
        else
            Destroy(this);        
    }

    #region Field notifications
    public Action<bool> playSlotsNotify;
   public void PlayCard()
    {
        playSlotsNotify?.Invoke(true);
    }
    public void HasPlayedCard()
    {
        playSlotsNotify?.Invoke(false);
    }
    #endregion
}
