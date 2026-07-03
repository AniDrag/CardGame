using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace AniDrag.EventBus.Utils
{
    /// <summary>
    /// Utility class for automatic discovery, initialization, and cleanup of event buses.
    /// 
    /// <para><b>Why we need this:</b></para>
    /// <para>
    /// In a classic event bus pattern, you must manually create and manage a separate static bus for each event type.
    /// This is tedious and error?prone. <see cref="EventBusUtilities"/> automates three critical tasks:
    /// <list type="bullet">
    ///   <item><description><b>Discovery</b> – scans all runtime assemblies to find every type that implements <see cref="IEvBusEvent"/>,
    ///         so you never forget to register an event type.</description></item>
    ///   <item><description><b>Initialization</b> – uses reflection to instantiate the generic <see cref="EventBus{T}"/>
    ///         for each discovered event type, ensuring all buses are ready before the first scene loads.</description></item>
    ///   <item><description><b>Cleanup</b> – when exiting play mode in the Editor, it automatically clears all event buses
    ///         to prevent leftover subscriptions from affecting the next play session.</description></item>
    /// </list>
    /// Without this utility, you would have to write and maintain repetitive boilerplate code for every event.
    /// </para>
    /// </summary>
    public class EventBusUtilities
    {
        /// <summary>
        /// A read?only list of all discovered event types (types that implement <see cref="IEvBusEvent"/>).
        /// Populated when <see cref="Initialize"/> is called.
        /// 
        /// <para><b>Why needed:</b> Provides a central registry of all events in the project, useful for debugging,
        /// editor tooling, or dynamic event subscription systems.</para>
        /// </summary>
        public static IReadOnlyList<Type> EventTypes { get; set; }
        /// <summary>
        /// A read?only list of all concrete <see cref="EventBus{T}"/> types created for each discovered event type.
        /// 
        /// <para><b>Why needed:</b> Allows you to iterate over all existing event buses, e.g., to clear them
        /// or to display their subscriber lists in a debug window. Without this, you would not know which buses exist.</para>
        /// </summary>
        public static IReadOnlyList<Type> EventBusTypes { get; set; }

#if UNITY_EDITOR
        /// <summary>
        /// Gets or sets the current play mode state of the Unity Editor.
        /// Used to detect when exiting play mode to trigger a cleanup.
        /// 
        /// <para><b>Why needed:</b> The Editor does not reset static fields automatically between play sessions.
        /// Subscriptions from a previous run would persist and cause bugs or duplicate event handling.
        /// This property lets us react exactly when play mode ends.</para>
        /// </summary>
        public static PlayModeStateChange CurrentPlayModeState { get; set; }

        [InitializeOnLoadMethod]
        static void OnEditorLoad()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
            EditorApplication.playModeStateChanged += OnPlayModeStateChange;
        }

        /// <summary>
        /// Handles play mode state changes in the Editor.
        /// When the state switches to <see cref="PlayModeStateChange.ExitingPlayMode"/>, it calls <see cref="CleareAllBuses"/>.
        /// 
        /// <para><b>Why needed:</b>
        /// This ensures that all static event buses are reset before the next play session.
        /// Without this, delegates would accumulate and cause memory leaks or unexpected behaviour
        /// (e.g., a listener receiving events from a previous run).</para>
        /// </summary>
        static void OnPlayModeStateChange(PlayModeStateChange state)
        {
            CurrentPlayModeState = state;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                CleareAllBuses();
            }
        }

#endif

        /// <summary>
        /// Initializes the event bus system before the first scene loads.
        /// Discovers all event types via <see cref="PreDefinedAssemlyUtil.GetTypes"/> and creates
        /// the corresponding <see cref="EventBus{T}"/> types.
        /// 
        /// <para><b>Why needed:</b>
        /// <list type="number">
        ///   <item><description><b>Automation</b> – You don't have to manually call a registration method for each event type.
        ///         The utility finds them all for you.</description></item>
        ///   <item><description><b>Timing</b> – Using <see cref="RuntimeInitializeOnLoadMethodAttribute"/> with
        ///         <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/> guarantees that all buses are ready
        ///         before any <c>Awake()</c> or <c>Start()</c> methods run, so other scripts can safely subscribe right away.</description></item>
        ///   <item><description><b>Reflection preparation</b> – Building the generic types early avoids runtime performance hits
        ///         the first time each bus is used.</description></item>
        /// </list>
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            Debug.Log("EventBusUtilities.Initialize() called");
            EventTypes = PreDefinedAssemlyUtil.GetTypes(typeof(IEvBusEvent));
            EventBusTypes = InitializeAllBuses();
        }


        /// <summary>
        /// Creates a concrete <see cref="EventBus{T}"/> type for each discovered event type.
        /// </summary>
        /// <returns>A list of <see cref="Type"/> objects representing each generated event bus.</returns>
        /// <remarks>
        /// <b>Why needed:</b> The static <see cref="EventBus{T}"/> class does not automatically "exist" for every
        /// possible <c>T</c>. You must call <c>MakeGenericType</c> to generate the closed generic type.
        /// This method does that once, stores the types, and also provides a debug log for verification.
        /// </remarks>
        static List<Type> InitializeAllBuses()
        {
            List<Type> eventBusTypes = new List<Type>();
            var typedef = typeof(EventBus<>);
            foreach (var eventType in EventTypes)
            {
                var busType = typedef.MakeGenericType(eventType);
                eventBusTypes.Add(busType);
               Debug.Log($"Initialized EventBus for event type<{eventType.Name}>");
            }

            return eventBusTypes;
        }


        /// <summary>
        /// Clears all registered event buses by invoking their static <c>Clear</c> method.
        /// This method is called automatically when exiting play mode in the Editor,
        /// but can also be called manually to reset all event subscriptions.
        /// 
        /// <para><b>Why needed:</b>
        /// Without a way to clear subscriptions, the event bus would act as a global, persistent state.
        /// In Unity, entering and exiting play mode repeatedly would cause old subscribed objects (which may be destroyed)
        /// to remain in the invocation list, leading to:
        /// <list type="bullet">
        ///   <item><description>Memory leaks (delegates referencing dead objects)</description></item>
        ///   <item><description><c>MissingReferenceException</c> when those dead objects are invoked</description></item>
        ///   <item><description>Duplicate event handling on subsequent play sessions</description></item>
        /// </list>
        /// This method solves that problem by giving each <see cref="EventBus{T}"/> a <c>Clear</c> method that resets its internal
        /// handler set, keeping the system clean between runs.</para>
        /// </summary>
        public static void CleareAllBuses()
        {
            if (EventBusTypes == null)
            {
                Debug.LogWarning("CleareAllBuses called but EventBusTypes was not initialized. Skipping cleanup.");
                return;
            }

            Debug.Log("Clearing all EventBuses...");

            for (int i = 0; i < EventBusTypes.Count; i++)
            {
                Type busType = EventBusTypes[i];

                var clearMethod = busType.GetMethod(
                    "Clear",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic
                );

                if (clearMethod == null)
                {
                    Debug.LogWarning($"Could not find Clear() on {busType.Name}");
                    continue;
                }

                clearMethod.Invoke(null, null);
            }
        }
    }
}