using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace OSCTools.Internal {
	internal class SimpleMessageDispatcher : IOSCMessageDispatcher {
		Dictionary<string, List<Action<OSCMessageIn, IPEndPoint>>> handlers;
		static Dictionary<Action<OSCMessageIn, IPEndPoint>, Regex> signatureChecker;

		public SimpleMessageDispatcher() {
			handlers = new Dictionary<string,List<Action<OSCMessageIn, IPEndPoint>>>();
			signatureChecker = new Dictionary<Action<OSCMessageIn, IPEndPoint>, Regex>();
		}

		public bool AddListener(string address, Action<OSCMessageIn, IPEndPoint> handler, params string[] args) {
			if (address == null || address.Length <= 1 || address[0] != '/') {
				OSCLog.WriteLine("INVALID ADDRESS: " + address);
				return false;
			}
			string[] parts = address.Split('/');
			foreach (string part in parts) {
				if (!OSCPatternMatcher.IsValidAddress(part)) return false;
			}
			if (!handlers.ContainsKey(address)) {
				handlers[address]=new List<Action<OSCMessageIn, IPEndPoint>>();
			}
			if (!handlers[address].Contains(handler)) {
				handlers[address].Add(handler);
				OSCLog.WriteLine("Adding message handler for header " + address);
			} else {
				OSCLog.WriteLine("Handler for header already added: " + address);
			}

			if (args.Length > 0) {
				Regex sigCheck = OSCUtil.CreateSignatureChecker(args);
				signatureChecker[handler] = sigCheck;
				OSCLog.WriteLine("Adding signature checker: " + sigCheck);
			}
			return true;
		}
		public bool DispatchMessage(OSCMessageIn message, IPEndPoint sender) {
			if (OSCPatternMatcher.IsSpecialPattern(message.header)) {
				OSCLog.WriteLine("SimpleMessageDispatcher cannot handle OSC patterns like " + message.header);
				return false;
			}
			if (handlers.ContainsKey(message.header)) {
				foreach (var handler in handlers[message.header]) {
					if (signatureChecker.ContainsKey(handler)) {
						if (signatureChecker[handler].IsMatch(message.typeTag)) {
							handler(message, sender);
							message.ResetRead();
						}
					} else {
						handler(message, sender);
						message.ResetRead();
					}
				}
			} else {
				OSCLog.WriteLine("No message handlers known for header " + message.header);
			}
			return true;
		}

		/// <summary>
		/// Removes the listener [handler] for the header [address].
		/// Returns true iff a listener was indeed removed.
		/// </summary>
		public bool RemoveListener(string address, Action<OSCMessageIn, IPEndPoint> handler) {
			if (handlers.ContainsKey(address)) {
				if (handlers[address].Contains(handler)) {
					OSCLog.WriteLine("Removing message handler for header "+address);
					handlers[address].Remove(handler);
					if (handlers[address].Count==0) {
						handlers.Remove(address);
					}
					if (signatureChecker.ContainsKey(handler)) {
						signatureChecker.Remove(handler);
					}
					return true;
				}
			}
			OSCLog.WriteLine("Could not remove handler for header "+address);
			return false;
		}
	}
}
