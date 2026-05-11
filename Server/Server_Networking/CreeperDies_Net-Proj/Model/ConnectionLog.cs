using System;

namespace NetworkConnections {
	public class ConnectionLog {
		/// <summary>
		/// Set to 0 to suppress all connection related logging, and higher values to allow
		/// more logging.
		/// </summary>
		public static int LogLevel = 1;

		public static void WriteLine(string text, params object[] args) {
			WriteLine(1, text, args);
		}
		public static void Write(string text, params object[] args) {
			Write(1, text, args);
		}

		public static void WriteLine(int priority, string text, params object[] args) {
			Write(priority, text + '\n', args);
		}

		public static void Write(int priority, string text, params object[] args) {
			if (LogLevel >= priority) {
				Console.WriteLine(String.Format(text, args));
			}
		}
	}
}
