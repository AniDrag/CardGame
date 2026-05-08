using System;

namespace AniDrag.EventBus
{

    /// <summary>
    /// Structs are allcoted on the stack, so they are more efficient than classes for small data containers.
    /// AKA less pressure on the garbage collector.
    /// Interface for safety and robustnes. 
    /// </summary>
    public interface IEvBusEvent { }


    /// <summary>
    /// This is an example usage event
    /// </summary>
    public struct RefferenceEvent : IEvBusEvent
    {
        public int referenceInt;
        public RefferenceEvent(int pHandIndex)
        {
            referenceInt = pHandIndex;
        }
    }
    /// <summary>
    /// A Showcase class on how to use
    /// </summary>
    class TestSubscription
    {
        EventBinding<RefferenceEvent> RefferenceEventBinding;

        int referenceInt;
        void ReferenceEvent(RefferenceEvent e)
        {
            referenceInt = e.referenceInt;
        }

        void HowToSubscribe()
        {
            RefferenceEventBinding = new EventBinding<RefferenceEvent>(ReferenceEvent);
            EventBus<RefferenceEvent>.Subscribe(RefferenceEventBinding);
        }
        void HowToUnSubscribe()
        {
            EventBus<RefferenceEvent>.Unsubscribe(RefferenceEventBinding);
        }
        void HowToPublish()
        {
            EventBus<RefferenceEvent>.Publish(new RefferenceEvent(7));// will output 7
        }
    }
}