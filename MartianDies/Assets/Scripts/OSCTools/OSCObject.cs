////////////////////////////////////////////////////////////////////////////////////////////////////
//
// OSCTools: a C# implementation of the OSC protocol, spec 1.0
//  (See https://ccrma.stanford.edu/groups/osc/index.html )
//  Paul Bonsma, 2025-2026.
// 
// License: CC0 / Public Domain
// (Though credit is appreciated and misrepresentation is frowned upon :-)
//
// Feel free to extend it and/or change the user interface!
//
// Notes: 
//  - MIDI messages, chars and arrays are not implemented
//  - OSC Address patterns are not included/supported in this version
//     (see OSCMessageDispatcher for more info)
//  - The time implementation follows the original spec with start time 1900,
//     so there's a year 2038 problem (See OSCUtil)
//
////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Text;
using System.IO;

namespace OSCTools {

	/// <summary>
	/// This is the root class for all OSC packet objects, and contains mostly static helper functions.
	/// </summary>
	public class OSCObject {
		public const char INT = 'i';
		public const char FLOAT = 'f';
		public const char STRING = 's';
		public const char BLOB = 'b';
		public const char COLOR = 'r';
		public const char TRUE = 'T';
		public const char FALSE = 'F';
		public const char NIL = 'N';
		public const char IMPULSE = 'I';
		//public const char CHAR = 'c'; // It seems the char implementation is a bit ambiguous (which byte?) and it's superfluous anyway (consider string) - skipped
		public const char DOUBLE = 'd';
		public const char LONG = 'h';
		public const char TIME = 't';

		/// <summary>
		/// Set this to true during testing, and false for release mode.
		/// </summary>
		public static bool ThrowExceptions = false;

		// cached, for temporary use:
		protected static byte[] word32 = new byte[4];
		protected static byte[] word64 = new byte[8];

		public bool corrupt { get; protected set; } = false;

		public static bool IsBundle(byte[] data) {
			// light weight method to avoid doing things twice - we won't check validity of the entire bundle here anyway
			return (data != null && data.Length > 0 && data[0] == '#');
		}

		/// <summary>
		/// Copies [length] bytes from [input] to [output], starting at [index].
		/// This prepares for BigEndian conversion using BitConverter, so it inverts the array order
		///  if the BitConverter is LittleEndian.
		/// </summary>
		public static void ArrayCopy(byte[] input, int index, int length, byte[] output) {
			if (input.Length < index + length || output.Length < length) {
				if (ThrowExceptions) { throw new IndexOutOfRangeException(); }
				return;
			}
			if (BitConverter.IsLittleEndian) {
				for (int i = 0; i < length; i++) {
					output[i] = input[index + length - 1 - i];
				}
			} else {
				for (int i = 0; i < length; i++) {
					output[i] = input[index + i];
				}
			}
		}

		/// <summary>
		/// Reads an int32 from the position [index] of [bytes], using OSC's BigEndian convention.
		/// </summary>
		public static int GetInt(byte[] bytes, ref int index, int defaultValue = -1) {
			if (index < 0 || index + 4 > bytes.Length) {
				if (ThrowExceptions) throw new IndexOutOfRangeException();
				return defaultValue;
			}
			ArrayCopy(bytes, index, 4, word32);
			index += 4;
			return BitConverter.ToInt32(word32, 0);
		}
		/// <summary>
		/// Reads an int64 from the position [index] of [bytes], using OSC's BigEndian convention.
		/// </summary>
		public static long GetLong(byte[] bytes, ref int index, long defaultValue = -1) {
			if (index < 0 || index + 8 > bytes.Length) {
				if (ThrowExceptions) throw new IndexOutOfRangeException();
				return defaultValue;
			}
			ArrayCopy(bytes, index, 8, word64);
			index += 8;
			return BitConverter.ToInt64(word64, 0);
		}
		/// <summary>
		/// Reads a uint64 from the position [index] of [bytes], using OSC's BigEndian convention.
		/// </summary>
		public static ulong GetULong(byte[] bytes, ref int index, ulong defaultValue = 0) {
			if (index < 0 || index + 8 > bytes.Length) {
				if (ThrowExceptions) throw new IndexOutOfRangeException();
				return defaultValue;
			}
			ArrayCopy(bytes, index, 8, word64);
			index += 8;
			return BitConverter.ToUInt64(word64, 0);
		}
		public static float GetFloat(byte[] bytes, ref int index, float defaultValue = 0) {
			if (index < 0 || index + 4 > bytes.Length) {
				if (ThrowExceptions) throw new IndexOutOfRangeException();
				return defaultValue;
			}
			ArrayCopy(bytes, index, 4, word32);
			index += 4;
			return BitConverter.ToSingle(word32, 0);
		}
		/// <summary>
		/// Reads a standard C string (ASCII, null-terminated) from the position [index] of [bytes],
		/// and increases [index] to the next byte position (divisible by 4 - 32 bit) after the string.
		/// Returns [null] if there is no C string at the position [index].
		/// </summary>
		public static string GetString(byte[] bytes, ref int index) {
			if (index < 0 || index >= bytes.Length) return null;
			int end = index;
			while (bytes[end] != 0) {
				end++;
				if (end >= bytes.Length) return null; // error
			}
			string output = Encoding.ASCII.GetString(bytes, index, end - index);
			index = end + 4 - end % 4;
			return output;
		}

		public static void WritePaddedStringToStream(Stream stream, string str) {
			byte[] data = Encoding.ASCII.GetBytes(str);
			stream.Write(data, 0, data.Length);
			int pad = 4 - data.Length % 4;
			for (int i = 0; i < pad; i++) {
				stream.WriteByte(0);
			}
		}

		public static void AddToStream(byte[] data, bool considerEndian, Stream stream) {
			if (considerEndian && BitConverter.IsLittleEndian) {
				for (int i = data.Length - 1; i >= 0; i--) {
					stream.WriteByte(data[i]);
				}
			} else {
				stream.Write(data, 0, data.Length);
			}
		}

		// Should be implemented in all outgoing objects:
		public virtual void SerializeToStream(Stream stream) {
		}
		// Should be implemented in all outgoing objects:
		public virtual byte[] GetBytes() {
			return null;
		}
	}
}
