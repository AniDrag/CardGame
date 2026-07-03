using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AniDrag.EventBus
{
    public static class EventBus<T> where T : IEvBusEvent
    {
        private static readonly HashSet<EventBinding<T>> bindings = new HashSet<EventBinding<T>>();

        public static void Subscribe(EventBinding<T> pBinding)
        {
            if (pBinding == null)
            {
                Debug.LogError($"EventBus<{typeof(T).Name}>: Tried to subscribe NULL binding.");
                return;
            }

            bindings.Add(pBinding);
        }

        public static void Unsubscribe(EventBinding<T> pBinding)
        {
            if (pBinding == null)
            {
                Debug.LogWarning($"EventBus<{typeof(T).Name}>: Tried to unsubscribe NULL binding.");
                return;
            }

            bindings.Remove(pBinding);
        }

        public static void Publish(
            T @event,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0)
        {
            string eventName = typeof(T).Name;

            // Works for class-based events. Struct events cannot truly be null.
            object boxedEvent = @event;
            if (boxedEvent == null)
            {
                Debug.LogError(
                    $"EventBus<{eventName}>: Tried to publish NULL event.\n" +
                    $"Caller: {callerName}\n" +
                    $"File: {callerFile}\n" +
                    $"Line: {callerLine}"
                );
                return;
            }

            if (bindings.Count == 0)
            {
                Debug.LogWarning(
                    $"EventBus<{eventName}>: Published event, but there are NO listeners.\n" +
                    $"Caller: {callerName}\n" +
                    $"File: {callerFile}\n" +
                    $"Line: {callerLine}\n" +
                    $"Event data: {@event}"
                );
                return;
            }

            EventBinding<T>[] snapshot = new EventBinding<T>[bindings.Count];
            bindings.CopyTo(snapshot);

            foreach (EventBinding<T> binding in snapshot)
            {
                if (binding == null)
                {
                    Debug.LogError(
                        $"EventBus<{eventName}>: Found NULL binding while publishing.\n" +
                        $"Caller: {callerName}\n" +
                        $"File: {callerFile}\n" +
                        $"Line: {callerLine}"
                    );

                    bindings.Remove(binding);
                    continue;
                }

                if (binding.OnEvent == null && binding.OnEventNoArgs == null)
                {
                    Debug.LogError(
                        $"EventBus<{eventName}>: Binding has no valid callbacks.\n" +
                        $"Caller: {callerName}\n" +
                        $"File: {callerFile}\n" +
                        $"Line: {callerLine}"
                    );

                    bindings.Remove(binding);
                    continue;
                }

                if (HasDeadUnityTarget(binding.OnEvent) || HasDeadUnityTarget(binding.OnEventNoArgs))
                {
                    Debug.LogError(
                        $"EventBus<{eventName}>: Listener target was destroyed but still subscribed.\n" +
                        $"This usually means something forgot to unsubscribe in OnDisable/OnDestroy.\n" +
                        $"Caller: {callerName}\n" +
                        $"File: {callerFile}\n" +
                        $"Line: {callerLine}"
                    );

                    bindings.Remove(binding);
                    continue;
                }

                try
                {
                    binding.OnEvent?.Invoke(@event);
                    binding.OnEventNoArgs?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"EventBus<{eventName}>: Listener threw an exception.\n" +
                        $"Caller: {callerName}\n" +
                        $"File: {callerFile}\n" +
                        $"Line: {callerLine}\n" +
                        $"Event data: {@event}\n" +
                        $"Exception:\n{ex}"
                    );
                }
            }
        }

        private static bool HasDeadUnityTarget(Delegate action)
        {
            if (action == null)
                return false;

            Delegate[] invocationList = action.GetInvocationList();

            foreach (Delegate subscriber in invocationList)
            {
                if (subscriber.Target is UnityEngine.Object unityObject && unityObject == null)
                    return true;
            }

            return false;
        }

        public static void Clear()
        {
            bindings.Clear();
        }
    }
}