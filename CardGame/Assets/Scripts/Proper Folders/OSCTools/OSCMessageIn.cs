using System;
using System.Text;

namespace OSCTools {
	public class OSCMessageIn : OSCObject {
		public readonly string header;
		public readonly string typeTag;

		readonly int typeTagIndexStart;
		readonly int contentIndexStart;

		byte[] data;
		int readIndex = 0;
		int typeTagIndex = 1;

		public OSCMessageIn(byte[] data) {
			this.data = data;
			header = GetString(data, ref readIndex);
			typeTagIndex = readIndex + 1;
			typeTag = GetString(data, ref readIndex);
			OSCLog.WriteLine($"Header: [{header}] TypeTag: [{typeTag}]");
			if (header == null || typeTag == null || header[0] != '/' || typeTag[0] != ',') {
				corrupt = true;
				if (ThrowExceptions) throw new FormatException("Corrupt header or type tag");
			}
			contentIndexStart = readIndex;
			typeTagIndexStart = typeTagIndex;
		}

		public void ResetRead() {
			readIndex = contentIndexStart;
			typeTagIndex = typeTagIndexStart;
		}

		/// <summary>
		/// Returns a char representing the next value, such as 'i' for integer, 'f' for float, 's' for string.
		/// See https://ccrma.stanford.edu/groups/osc/spec-1_0.html (or OSCObject constants) for a full list.
		/// If there's no more values to be read, returns a null character ((char)0).
		/// </summary>
		public char NextType() {
			return typeTagIndex < data.Length ? (char)data[typeTagIndex] : (char)0;
		}
		bool ReadTag(char tag) {
			if (NextType() != tag) {
				if (ThrowExceptions) { throw new FormatException("The next element is not of type " + tag); }
				return false;
			}
			typeTagIndex++;
			return true;
		}

		public int ReadInt(int defaultValue = 0) {
			if (!ReadTag(INT)) return defaultValue;
			return GetInt(data, ref readIndex, defaultValue);
		}
		public float ReadFloat(float defaultValue = 0) {
			if (!ReadTag(FLOAT)) return defaultValue;
			return GetFloat(data, ref readIndex, defaultValue);
		}
		public string ReadString(string defaultValue = null) {
			if (!ReadTag(STRING)) return defaultValue;
			return GetString(data, ref readIndex) ?? defaultValue;
		}

		public byte[] ReadBlob() {
			if (!ReadTag(BLOB)) return null;

			// read length int32:
			ArrayCopy(data, readIndex, 4, word32);
			readIndex += 4;
			int len = BitConverter.ToInt32(word32, 0);
			// read blob:
			byte[] blob = new byte[len];
			Array.Copy(data, readIndex, blob, 0, len);
			readIndex += len + (4 - len % 4) % 4;
			return blob;
		}
		public OSCColor ReadColor() {
			if (!ReadTag(COLOR)) return new OSCColor(1, 1, 1, 1);
			if (readIndex + 4 > data.Length) {
				if (ThrowExceptions) { throw new IndexOutOfRangeException(); }
				return new OSCColor(1, 1, 1, 1);
			}
			return new OSCColor(
				data[readIndex++],
				data[readIndex++],
				data[readIndex++],
				data[readIndex++]
			);
		}
		public bool ReadBool() {
			char c = NextType();
			if (NextType() != TRUE && NextType() != FALSE) {
				if (ThrowExceptions) { throw new FormatException("The next element is not of type bool"); }
				return false;
			}
			typeTagIndex++;
			return c == TRUE;
		}
		/// <summary>
		/// Returns true iff the next element is indeed NIL
		/// </summary>
		public bool ReadNil() {
			if (!ReadTag(NIL)) return false;
			return true;
		}
		/// <summary>
		/// Returns true iff the next element is indeed an impulse
		/// </summary>
		public bool ReadImpulse() {
			if (!ReadTag(IMPULSE)) return false;
			return true;
		}
		//public char ReadChar(char defaultValue=(char)0) {
		//	if (!ReadTag(CHAR)) return defaultValue;
		//	char c = (char)data[readIndex];
		//	readIndex += 4;
		//	return c;
		//}
		public double ReadDouble(double defaultValue = 0.0) {
			if (!ReadTag(DOUBLE)) return defaultValue;
			ArrayCopy(data, readIndex, 8, word64);
			readIndex += 8;
			return BitConverter.ToDouble(word64, 0);
		}
		public long ReadLong(long defaultValue = -1) {
			if (!ReadTag(LONG)) return defaultValue;
			return GetLong(data, ref readIndex, defaultValue);
		}
		public ulong ReadTime(ulong defaultValue = 0) {
			if (!ReadTag(TIME)) return defaultValue;
			return GetULong(data, ref readIndex, defaultValue);
		}

		public override string ToString() {
			if (corrupt) {
				return "Corrupt header or type tag string";
			}
			StringBuilder sb = new StringBuilder(header);
			ResetRead();
			try {
				while (NextType() != (char)0) {
					sb.Append(' ');
					char c = NextType();
					switch (c) {
						case OSCObject.INT:
							sb.Append(ReadInt().ToString()); break;
						case OSCObject.FLOAT:
							sb.Append($"{ReadFloat():0.0000}f"); break;
						case OSCObject.STRING:
							sb.Append($"\"{ReadString()}\""); break;
						case OSCObject.BLOB:
							sb.Append("[ ");
							byte[] blob = ReadBlob();
							foreach (byte b in blob) {
								sb.Append($"{b:X2} ");
							}
							sb.Append(']');
							break;
						case OSCObject.TRUE:
							ReadBool();
							sb.Append("True "); break;
						case OSCObject.FALSE:
							ReadBool();
							sb.Append("False"); break;
						case OSCObject.COLOR:
							sb.Append('[');
							sb.Append(ReadColor().ToString());
							sb.Append(']');
							break;
						case OSCObject.NIL:
							ReadNil();
							sb.Append("NIL"); break;
						case OSCObject.IMPULSE:
							ReadImpulse();
							sb.Append("Impulse"); break;
						case OSCObject.DOUBLE:
							sb.Append($"{ReadDouble():0.0000}"); break;
						case OSCObject.LONG:
							sb.Append($"{ReadLong()}L"); break;
						case OSCObject.TIME:
							sb.Append(OSCUtil.SecondsToDateTime(OSCUtil.OSCTimeToSeconds(ReadTime()))); break;
						default:
							sb.Append("Unrecognized type: " + c);
							typeTagIndex++;
							break;
					}
				}
			} catch (Exception error) {
				ResetRead();
				return sb.ToString() + " corrupt content: "+error.Message;
			}
			ResetRead();
			return sb.ToString();
		}
	}
}
