using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net; // For IPEndPoint
using OSCTools.Internal;

namespace OSCTools {

	/// <summary>
	/// This class is in charge of dispatching incoming packets to any listeners that match the 
	/// address pattern of the incoming packet. It also takes the time of bundles into account.
	/// 
	/// Interface: call 
	///  - HandlePacket to handle a packet or queue it in case of a future bundle (e.g. incoming packets from a UDPClient),
	///  - Add/RemoveListener to add/remove listeners,
	///  - Update regularly to do the actual dispatching of queued bundles.
	/// </summary>
	public class OSCDispatcher {

		#region Data

		List<OSCBundleIn> bundleQueue = new List<OSCBundleIn>();
		ulong currentTime = OSCUtil.GetCurrentOSCTime();
		IOSCMessageDispatcher messageDispatcher = DispatcherCreator.Create();
		bool handlingMessage = false;

		struct ListenerInfo {
			public string address;
			public Action<OSCMessageIn, IPEndPoint> handler;
			public string[] args;
			public ListenerInfo(string pAddress, Action<OSCMessageIn, IPEndPoint> pHandler, string[] pArgs) {
				address = pAddress;
				handler = pHandler;
				args = pArgs;
			}
		}
		List<ListenerInfo> toAdd = new List<ListenerInfo>();
		List<ListenerInfo> toRemove = new List<ListenerInfo>();
		#endregion

		#region Interface

		/// <summary>
		/// Set this to true to log incoming messages and bundles using OSCLog.
		/// </summary>
		public bool ShowIncomingMessages = false;
		/// <summary>
		/// Adds listener [handler] for incoming packets with header [address].
		/// Optionally: add a combination of OSC tags (e.g. OSCUtil.BOOL, OSCUtil.INT) to 
		///  filter incoming messages to match that signature.
		/// </summary>
		public void AddListener(string address, Action<OSCMessageIn, IPEndPoint> handler, params string[] args) {
			if (handlingMessage) {
				toAdd.Add(new ListenerInfo(address, handler, args));
			} else {
				messageDispatcher.AddListener(address, handler, args);
			}
		}
		/// <summary>
		/// Removes the listener [handler] for incoming packets with header [address].
		/// </summary>
		public void RemoveListener(string address, Action<OSCMessageIn, IPEndPoint> handler) {
			if (handlingMessage) {
				toRemove.Add(new ListenerInfo(address, handler, null));
			} else {
				messageDispatcher.RemoveListener(address, handler);
			}
		}
		/// <summary>
		/// Call Update regularly to handle queued/delayed incoming bundles.
		/// </summary>
		public void Update() {
			currentTime = OSCUtil.GetCurrentOSCTime();
			while (bundleQueue.Count > 0 && bundleQueue[0].time <= currentTime) {
				HandleBundle(bundleQueue[0]);
				bundleQueue.RemoveAt(0);
			}
		}
		/// <summary>
		/// If [packet] is an OSCBundle or OSCMessage, this method forwards the packet to any
		///  listener matching the packet's address pattern(s).
		/// Optionally, set [updateTime] to true to update the current time (which otherwise is done in the next Update). 
		/// </summary>
		public void HandlePacket(byte[] packet, IPEndPoint sender, bool updateTime = false) {
			if (updateTime) {
				currentTime = OSCUtil.GetCurrentOSCTime();
			}
			if (OSCObject.IsBundle(packet)) {
				OSCBundleIn bundle = new OSCBundleIn(packet, sender);
				if (!bundle.corrupt) {
					if (ShowIncomingMessages) {
						OSCLog.WriteDirect("Incoming bundle packet: "+bundle.ToString());
					}
					HandleOrQueueBundle(bundle);
				}
			} else {
				OSCMessageIn message = new OSCMessageIn(packet);
				if (!message.corrupt) {
					HandleMessage(message, sender);
				}
			}
		}
		#endregion

		#region PrivateMethods

		void HandleOrQueueBundle(OSCBundleIn bundle) {
			if (bundle.time < currentTime) {
				HandleBundle(bundle);
			} else {
				bundleQueue.Add(bundle);
				// On tie-break: This should respect the order they came in...?
				bundleQueue.Sort((a, b) => { return a.time.CompareTo(b.time); });
				// invariant: the bundleQueue is ALWAYS sorted by increasing time
			}
		}

		void HandleBundle(OSCBundleIn bundle) {
			while (true) {
				OSCObject obj = bundle.GetNextObject();
				if (obj == null) break;

				if (obj is OSCBundleIn) {
					HandleOrQueueBundle((OSCBundleIn)obj);
				} else { // obj is OSCMessageIn
					HandleMessage((OSCMessageIn)obj, bundle.sender);
				}
			}
		}

		void HandleMessage(OSCMessageIn message, IPEndPoint sender) {
			if (ShowIncomingMessages) {
				OSCLog.WriteDirect("Handling incoming message: " + message.ToString());
			}

			handlingMessage = true;
			messageDispatcher.DispatchMessage(message, sender);
			handlingMessage = false;

			if (toAdd.Count > 0 || toRemove.Count > 0) {
				OSCLog.WriteLine("Handling {0} delayed adds and {1} delayed removes!", toAdd.Count, toRemove.Count);
				HandleDelayedListeners();
			}
		}

		void HandleDelayedListeners() {
			if (handlingMessage) throw new Exception("Cannot add/remove listeners while handling messages!");
			foreach (var lInfo in toAdd) {
				AddListener(lInfo.address, lInfo.handler, lInfo.args);
			}
			toAdd.Clear();
			foreach (var lInfo in toRemove) {
				RemoveListener(lInfo.address, lInfo.handler);
			}
			toRemove.Clear();
		}

		#endregion
	}
}
