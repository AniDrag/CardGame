// Comment out this line for console projects:
#define UNITY
#if UNITY
using UnityEngine;
#endif
using System;

namespace OSCTools {

	class OSCLog {
		public static bool logging = false;

		public static void WriteLine(string text, params object[] args) {
			Write(text + '\n', args);
		}

		public static void Write(string text, params object[] args) {
			if (logging)
#if UNITY
				Debug.Log(String.Format(text, args));
#else
				Console.WriteLine(String.Format(text, args));
# endif
		}

		public static void WriteDirect(string text) {
#if UNITY
			Debug.Log(text);
#else
			Console.WriteLine(text);
#endif
		}
	}
}
