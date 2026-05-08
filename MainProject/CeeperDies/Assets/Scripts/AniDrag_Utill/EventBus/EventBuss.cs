using System.Collections.Generic;
/*
public class EventBuss<T> where T : Ev
{
    public delegate void EventHandler(T e);
    private event EventHandler OnEvent;
    public void Subscribe(EventHandler handler)
    {
        OnEvent += handler;
    }
    public void Unsubscribe(EventHandler handler)
    {
        OnEvent -= handler;
    }

    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <param name="e">The event data.</param>
    /// <param name="callerName">Name of the calling script or method (e.g., "PlayerHealth.TakeDamage").</param>
    /// <param name="callerObject">The GameObject that triggered the event (e.g., the player).</param>
    public void Publish(T e, string callerName = "Unknown", GameObject callerObject = null)
    {
        if (OnEvent == null) 
        { 
            Debug.LogWarning($"Event Published: {e} Has no listeners currently. Did you forget to initialize or set the events?");
            return; // No subscribers, exit early
        }

        Delegate[] subscribers = OnEvent.GetInvocationList();
        foreach (Delegate subscriber in subscribers)
        {
            EventHandler handler = (EventHandler)subscriber;
            try
            {
                handler.Invoke(e);
            }
            catch (Exception ex)
            {
                string targetInfo = subscriber.Target != null ? subscriber.Target.GetType().Name : "Static Method";
                string methodInfo = $"{targetInfo}.{subscriber.Method.Name}";

                string gameObjectName = callerObject != null ? callerObject.name : "Null GameObject";

                string errorMsg = $"EventBus<{typeof(T).Name}>: Exception in subscriber [{methodInfo}] \n while handling event published by {callerName} on GameObject '{gameObjectName}'.\n Event data: {e}\nException: {ex.Message}\n{ex.StackTrace}";
                                  

                Debug.LogError(errorMsg);
            }
        }
    }

    /// <summary>
    /// Returns debug information about all subscribers of this event type.
    /// </summary>
    public string[] GetSubscriberDebugInfo()
    {
        if (OnEvent == null) return Array.Empty<string>();

        Delegate[] subscribers = OnEvent.GetInvocationList();
        return subscribers.Select(s =>
        {
            string target = s.Target != null ? s.Target.GetType().Name : "Static";
            return $"{target}.{s.Method.Name}";
        }).ToArray();
    }

    public void ShowSubscribers()
    {
        var subs = GetSubscriberDebugInfo();
        if (subs.Length == 0)
        {
            Debug.Log($"EventBus<{typeof(T).Name}> has no subscribers.");
            return;
        }

        Debug.Log($"EventBus<{typeof(T).Name}> subscribers ({subs.Length}):\n" +
                  string.Join("\n", subs));
    }
}
public class Ev
{
    public string eventName;
    public Ev(string name) { eventName = name; }
}
#region Discard Card Events
// request to Client script to send a message
public class DiscardCardRequestEvent : Ev
{
    public int handIndex;
    public DiscardCardRequestEvent(int pHandIndex) : base("Discard Card Request Event")
    {
        handIndex = pHandIndex;
    }
}

//Replie from client that yes we can DELETE the card we tried to discard, and we can update the hand and discard pile accordingly.
public class DiscardCardEvent : Ev
{
    public int[] handIndex;
    public DiscardCardEvent(int[] pHandIndex) : base("Discard Card Event")
    {
        handIndex = pHandIndex;
    }
}
#endregion

#region Draw Card Events
public class DrawCardEvent : Ev
{
    public int[] IDs; //for every ID its a draw a card. 
    public DrawCardEvent(int[] pIDs) : base("Draw Card Event")
    {
        IDs = pIDs;
    }
}
// For cards that have the effect of drawing cards.
public class DrawCardRequestEvent : Ev
{
    public int count;
    public DrawCardRequestEvent(int pCount) : base("Draw Card Request Event")
    {
        count = pCount;
    }
}
#endregion

#region Play Card Events
public class PlayCardRequestEvent : Ev
{
    public int handIndex;
    public int[] targetIndex;// row, coll.
    public PlayCardRequestEvent(int pHandIndex,int[] pTargetIndex) : base("Play Card Request Event")
    {
        handIndex = pHandIndex;
        targetIndex = pTargetIndex;
    }
}
#endregion
*/
namespace AniDrag.EventBus
{

    public static class EventBus<T> where T : IEvBusEvent
    {
        static readonly HashSet<EventBinding<T>> bindings = new HashSet<EventBinding<T>>();

        public static void Subscribe(EventBinding<T> pBinding) => bindings.Add(pBinding);
        public static void Unsubscribe(EventBinding<T> pBinding) => bindings.Remove(pBinding);

        public static void Publish(T @event)
        {
            foreach (var binding in bindings)
            {
                binding.OnEvent(@event);
            }
        }
        public static void Clear() => bindings.Clear();
    }
}
/*
public class EventBuss<T> where T : Ev
{
    public delegate void EventHandler(T e);
    private event EventHandler OnEvent;
    public void Subscribe(EventHandler handler)
    {
        OnEvent += handler;
    }
    public void Unsubscribe(EventHandler handler)
    {
        OnEvent -= handler;
    }

    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <param name="e">The event data.</param>
    /// <param name="callerName">Name of the calling script or method (e.g., "PlayerHealth.TakeDamage").</param>
    /// <param name="callerObject">The GameObject that triggered the event (e.g., the player).</param>
    public void Publish(T e, string callerName = "Unknown", GameObject callerObject = null)
    {
        if (OnEvent == null) 
        { 
            Debug.LogWarning($"Event Published: {e} Has no listeners currently. Did you forget to initialize or set the events?");
            return; // No subscribers, exit early
        }

        Delegate[] subscribers = OnEvent.GetInvocationList();
        foreach (Delegate subscriber in subscribers)
        {
            EventHandler handler = (EventHandler)subscriber;
            try
            {
                handler.Invoke(e);
            }
            catch (Exception ex)
            {
                string targetInfo = subscriber.Target != null ? subscriber.Target.GetType().Name : "Static Method";
                string methodInfo = $"{targetInfo}.{subscriber.Method.Name}";

                string gameObjectName = callerObject != null ? callerObject.name : "Null GameObject";

                string errorMsg = $"EventBus<{typeof(T).Name}>: Exception in subscriber [{methodInfo}] \n while handling event published by {callerName} on GameObject '{gameObjectName}'.\n Event data: {e}\nException: {ex.Message}\n{ex.StackTrace}";
                                  

                Debug.LogError(errorMsg);
            }
        }
    }

    /// <summary>
    /// Returns debug information about all subscribers of this event type.
    /// </summary>
    public string[] GetSubscriberDebugInfo()
    {
        if (OnEvent == null) return Array.Empty<string>();

        Delegate[] subscribers = OnEvent.GetInvocationList();
        return subscribers.Select(s =>
        {
            string target = s.Target != null ? s.Target.GetType().Name : "Static";
            return $"{target}.{s.Method.Name}";
        }).ToArray();
    }

    public void ShowSubscribers()
    {
        var subs = GetSubscriberDebugInfo();
        if (subs.Length == 0)
        {
            Debug.Log($"EventBus<{typeof(T).Name}> has no subscribers.");
            return;
        }

        Debug.Log($"EventBus<{typeof(T).Name}> subscribers ({subs.Length}):\n" +
                  string.Join("\n", subs));
    }
}
public class Ev
{
    public string eventName;
    public Ev(string name) { eventName = name; }
}
#region Discard Card Events
// request to Client script to send a message
public class DiscardCardRequestEvent : Ev
{
    public int handIndex;
    public DiscardCardRequestEvent(int pHandIndex) : base("Discard Card Request Event")
    {
        handIndex = pHandIndex;
    }
}

//Replie from client that yes we can DELETE the card we tried to discard, and we can update the hand and discard pile accordingly.
public class DiscardCardEvent : Ev
{
    public int[] handIndex;
    public DiscardCardEvent(int[] pHandIndex) : base("Discard Card Event")
    {
        handIndex = pHandIndex;
    }
}
#endregion

#region Draw Card Events
public class DrawCardEvent : Ev
{
    public int[] IDs; //for every ID its a draw a card. 
    public DrawCardEvent(int[] pIDs) : base("Draw Card Event")
    {
        IDs = pIDs;
    }
}
// For cards that have the effect of drawing cards.
public class DrawCardRequestEvent : Ev
{
    public int count;
    public DrawCardRequestEvent(int pCount) : base("Draw Card Request Event")
    {
        count = pCount;
    }
}
#endregion

#region Play Card Events
public class PlayCardRequestEvent : Ev
{
    public int handIndex;
    public int[] targetIndex;// row, coll.
    public PlayCardRequestEvent(int pHandIndex,int[] pTargetIndex) : base("Play Card Request Event")
    {
        handIndex = pHandIndex;
        targetIndex = pTargetIndex;
    }
}
#endregion
*/