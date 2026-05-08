using System;
using System.IO;
using System.Collections.Generic;

namespace OSCTools {

	public class OSCBundleOut : OSCObject {
		public readonly ulong time;
		List<OSCObject> contents;

		public OSCBundleOut(ulong time) {
			this.time = time;
			contents = new List<OSCObject>();
		}

		public OSCBundleOut AddBundle(OSCBundleOut bundle) {
			contents.Add(bundle);
			return this;
		}
		public OSCBundleOut AddMessage(OSCMessageOut message) {
			contents.Add(message);
			return this;
		}

		public override void SerializeToStream(Stream packet) {
			WritePaddedStringToStream(packet, "#bundle");

			byte[] data = BitConverter.GetBytes(time);
			AddToStream(data, true, packet);

			foreach (OSCObject obj in contents) {
				data = obj.GetBytes();
				byte[] lengthData = BitConverter.GetBytes((int)data.Length);
				AddToStream(lengthData, true, packet);
				//obj.SerializeToStream(packet); // More efficient, but then we don't have the length...
				packet.Write(data, 0, data.Length);
			}
		}

		/// <summary>
		/// Serializes the current content into a byte array representing an OSCBundle.
		/// </summary>
		public override byte[] GetBytes() {
			MemoryStream packet = new MemoryStream();
			SerializeToStream(packet);
			return packet.ToArray();
		}
		public override string ToString() {
			return OSCBundleIn.CreateBundleString(time, contents);
		}
	}
}
