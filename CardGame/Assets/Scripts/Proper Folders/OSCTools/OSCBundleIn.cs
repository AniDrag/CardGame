using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace OSCTools {
	public class OSCBundleIn : OSCObject {
		public readonly ulong time;
		public readonly IPEndPoint sender;

		List<OSCObject> contents;
		int objectIndex = 0;

		public OSCBundleIn(byte[] data, IPEndPoint sender) {
			this.sender = sender;

			contents = new List<OSCObject>();
			int index = 0;
			string bundleString = GetString(data, ref index);
			if (bundleString != "#bundle") {
				corrupt = true;
				if (ThrowExceptions) { throw new FormatException("No valid bundle header"); }
				return;
			}
			try {
				time = GetULong(data, ref index);
				while (index < data.Length) {
					int len = GetInt(data, ref index);
					if (len + index > data.Length) {
						OSCLog.WriteLine("Too long bundle element length - corrupt bundle!");
						corrupt = true;
						if (ThrowExceptions) { throw new FormatException("Bundle contains excessive length value"); }
						break;
					}
					byte[] subdata = new byte[len];
					Array.Copy(data, index, subdata, 0, len);
					if (IsBundle(subdata)) {
						contents.Add(new OSCBundleIn(subdata, sender));
					} else {
						contents.Add(new OSCMessageIn(subdata));
					}
					index += len;
				}
			} catch (Exception error) {
				OSCLog.WriteLine("Got exception while parsing bundle: {0}\nStack:\n{1}", error.Message, error.StackTrace);
				corrupt = true;
				if (ThrowExceptions) { throw new FormatException("Corrupt bundle contents"); }
			}
		}
		// TODO: maybe implement iterator (IEnumerator)

		public OSCObject GetNextObject() {
			if (objectIndex < contents.Count) {
				return contents[objectIndex++];
			} else {
				return null;
			}
		}
		public void ResetRead() {
			objectIndex = 0;
		}
		public int GetLength() {
			return contents.Count;
		}

		public override string ToString() {
			return CreateBundleString(time, contents);
		}

		public static string CreateBundleString(ulong time, List<OSCObject> contents) { 
			StringBuilder builder = new StringBuilder("#bundle( ");
			ulong currentOSCTime = OSCUtil.GetCurrentOSCTime();
			if (time<=currentOSCTime) {
				builder.Append("NOW");
			} else {
				double seconds = OSCUtil.OSCTimeToSeconds(time - currentOSCTime);
				builder.Append($"{seconds:0.0000}s");
			}
			for (int i = 0; i < contents.Count; i++) {
				builder.Append(", ");
				builder.Append(contents[i].ToString());
			}
			builder.Append(" )");
			return builder.ToString();
		}
	}
}