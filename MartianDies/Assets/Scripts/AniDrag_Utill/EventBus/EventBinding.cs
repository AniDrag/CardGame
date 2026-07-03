using System;

namespace AniDrag.EventBus
{
    public interface IEventBinding<T>
    {
        public Action<T> OnEvent { get; set; }
        public Action OnEventNoArgs { get; set; }
    }

    public class EventBinding<T> : IEventBinding<T> where T : IEvBusEvent
    {
        public Action<T> OnEvent = _ => { };
        public Action OnEventNoArgs = () => { };

        Action<T> IEventBinding<T>.OnEvent
        {
            get => OnEvent;
            set => OnEvent = value ?? (_ => { });
        }

        Action IEventBinding<T>.OnEventNoArgs
        {
            get => OnEventNoArgs;
            set => OnEventNoArgs = value ?? (() => { });
        }

        public EventBinding(Action<T> pOnEvent)
        {
            OnEvent = pOnEvent ?? (_ => { });
        }

        public EventBinding(Action pOnEventNoArgs)
        {
            OnEventNoArgs = pOnEventNoArgs ?? (() => { });
        }

        public void Add(Action<T> pOnEvent)
        {
            if (pOnEvent != null)
                OnEvent += pOnEvent;
        }

        public void Remove(Action<T> pOnEvent)
        {
            if (pOnEvent != null)
                OnEvent -= pOnEvent;
        }

        public void Add(Action pOnEventNoArgs)
        {
            if (pOnEventNoArgs != null)
                OnEventNoArgs += pOnEventNoArgs;
        }

        public void Remove(Action pOnEventNoArgs)
        {
            if (pOnEventNoArgs != null)
                OnEventNoArgs -= pOnEventNoArgs;
        }
    }
}