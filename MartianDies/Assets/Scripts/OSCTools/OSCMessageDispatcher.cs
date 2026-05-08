#define USE_SIMPLE_DISPATCHER
using System;
using System.Net;

namespace OSCTools.Internal {
	internal interface IOSCMessageDispatcher {
		/// <summary>
		/// Adds listener [handler] for incoming header [address].
		/// Returns true iff the address is valid (and thus listener adding was successful).
		/// </summary>
		bool AddListener(string address, Action<OSCMessageIn, IPEndPoint> handler, params string[] args);
		/// <summary>
		/// Removes the listener [handler] for incoming header [address].
		/// Returns true iff a listener was indeed removed.
		/// </summary>
		bool RemoveListener(string address, Action<OSCMessageIn, IPEndPoint> handler);
		/// <summary>
		/// Dispatches [message] to all listeners that match the given address pattern.
		/// Returns true iff the addressPattern is valid. (Note: it does not matter if there were actual listeners for it.)
		/// </summary>
		bool DispatchMessage(OSCMessageIn message, IPEndPoint sender);
	}

	internal class DispatcherCreator {
		public static IOSCMessageDispatcher Create() {
#if USE_SIMPLE_DISPATCHER
			return new SimpleMessageDispatcher();
#else
			// If you want to add OSC address pattern handling, this is where you should add it:
			// (Tips: Use a tree data structure that matches the subscribed address structures, and
			//  translate OSC address patterns to C# RegEx (see also OSCPatternMatcher).)
			throw new NotImplementedException();
#endif
		}
	}
}
