using System;
namespace AniDrag.EventBus
{
    public interface IEventBinding<T>
    {
        public Action<T> OnEvent { get; set; }
        public Action OnEventNoArgs { get; set; }
    }

    public class EventBinding<T> : IEventBinding<T> where T : IEvent
    {
        // delegate(T _) { }; -> for events with arguments, default to an empty delegate to avoid null reference exceptions
        public Action<T> OnEvent = _ => { };
        public Action OnEventNoArgs = () => { };

        Action<T> IEventBinding<T>.OnEvent
        {
            get => OnEvent;
            set => OnEvent = value;
        }
        Action IEventBinding<T>.OnEventNoArgs
        {
            get => OnEventNoArgs;
            set => OnEventNoArgs = value;
        }

        public EventBinding(Action<T> pOnEvent) => OnEvent = pOnEvent;
        public EventBinding(Action pOnEventNoArgs) => OnEventNoArgs = pOnEventNoArgs;


        public void Add(Action<T> pOnEvent) => OnEvent += pOnEvent;
        public void Remove(Action<T> pOnEvent) => OnEvent -= pOnEvent;


        public void Add(Action pOnEventNoArgs) => OnEventNoArgs += pOnEventNoArgs;
        public void Remove(Action pOnEventNoArgs) => OnEventNoArgs -= pOnEventNoArgs;
    }
}