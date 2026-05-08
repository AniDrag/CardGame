// Comment out this line for console projects:
using System;

namespace OSCTools {

	class OSCLog {
		public static bool logging = false;

		public static void WriteLine(string text, params object[] args) {
			Write(text + '\n', args);
		}

		public static void Write(string text, params object[] args) {
			if (logging)
				Console.WriteLine(String.Format(text, args));
		}

		public static void WriteDirect(string text) {
			Console.WriteLine(text);
		}
	}
}
