using System;
using System.Text;
using System.IO; 

namespace OSCTools {

	public class OSCMessageOut : OSCObject {
		public readonly string header;
		StringBuilder typeTagBuilder;
		MemoryStream contents;
		public OSCMessageOut(string header) {
			if (header[0] != '/') throw new Exception("OSC headers need to start with a slash");
			this.header = header;
			contents = new MemoryStream();
			typeTagBuilder = new StringBuilder();
			typeTagBuilder.Append(',');
		}

		public OSCMessageOut AddInt(int value) {
			typeTagBuilder.Append(INT);
			byte[] data = BitConverter.GetBytes(value);
			AddToStream(data, true, contents);
			return this;
		}
		public OSCMessageOut AddFloat(float value) {
			typeTagBuilder.Append(FLOAT);
			byte[] data = BitConverter.GetBytes(value);
			AddToStream(data, true, contents);
			return this;
		}
		public OSCMessageOut AddString(string value) {
			typeTagBuilder.Append(STRING);
			WritePaddedStringToStream(contents, value);
			return this;
		}
		public OSCMessageOut AddBlob(byte[] blob) {
			typeTagBuilder.Append(BLOB);
			int len = blob.Length;
			byte[] data = BitConverter.GetBytes(len);
			AddToStream(data, true, contents);
			contents.Write(blob, 0, len);
			int pad = (4 - (len % 4)) % 4;
			for (int i = 0; i < pad; i++) {
				contents.WriteByte(0);
			}
			return this;
		}
		public OSCMessageOut AddColor(OSCColor color) {
			typeTagBuilder.Append(COLOR);
			contents.WriteByte(color.R);
			contents.WriteByte(color.G);
			contents.WriteByte(color.B);
			contents.WriteByte(color.A);
			return this;
		}
		public OSCMessageOut AddBool(bool val) {
			typeTagBuilder.Append(val ? TRUE : FALSE);
			return this;
		}
		public OSCMessageOut AddNil() {
			typeTagBuilder.Append(NIL);
			return this;
		}
		public OSCMessageOut AddImpulse() {
			typeTagBuilder.Append(IMPULSE);
			return this;
		}
		//public OSCMessageOut AddChar(char ch) {
		//	typeTagBuilder.Append(CHAR);
		//	contents.WriteByte((byte)ch);
		//	for (int i = 0; i < 3; i++) contents.WriteByte(0);
		//	return this;
		//}
		public OSCMessageOut AddDouble(double d) {
			typeTagBuilder.Append(DOUBLE);
			byte[] data = BitConverter.GetBytes(d);
			AddToStream(data, true, contents);
			return this;
		}
		public OSCMessageOut AddLong(long l) {
			typeTagBuilder.Append(LONG);
			byte[] data = BitConverter.GetBytes(l);
			AddToStream(data, true, contents);
			return this;
		}
		public OSCMessageOut AddTime(ulong time) {
			typeTagBuilder.Append(TIME);
			byte[] data = BitConverter.GetBytes(time);
			AddToStream(data, true, contents);
			return this;
		}

		public override void SerializeToStream(Stream packet) {
			WritePaddedStringToStream(packet, header);
			WritePaddedStringToStream(packet, typeTagBuilder.ToString());
			//contents.CopyTo(packet); // ?
			// Older C# version:
			var ar = contents.ToArray();
			packet.Write(ar, 0 ,ar.Length); // works, but less efficient?
		}

		/// <summary>
		/// Serializes the current content into a byte array representing an OSCMessage.
		/// (You can still call AddInt etc. later, but probably shouldn't)
		/// </summary>
		public override byte[] GetBytes() {
			MemoryStream packet = new MemoryStream();
			SerializeToStream(packet);
			return packet.ToArray();
		}

		public override string ToString() {
			OSCMessageIn m = new OSCMessageIn(GetBytes());
			return m.ToString();
		}
	}
}
