using System;
using System.Text;
using System.Text.RegularExpressions;

namespace OSCTools {

	public class OSCUtil {
		public const string INT = "i";
		public const string FLOAT = "f";
		public const string STRING = "s";
		public const string BLOB = "b";
		public const string COLOR = "r";
		public const string NIL = "N";
		public const string IMPULSE = "I";
		public const string DOUBLE = "d";
		public const string LONG = "h";
		public const string TIME = "t";
		/// <summary>
		/// NOTE: This is not actually an element that appears in OSC type tags: T or F appears instead.
		/// You should use this however as input for the CreateSignatureChecker method.
		/// </summary>
		public const string BOOL = "B";


		public static Regex CreateSignatureChecker(params string[] args) {
			StringBuilder builder = new StringBuilder();
			builder.Append(','); // anchor not needed
			foreach (string arg in args) {
				string narg = arg.Replace("B", "[TF]");
				builder.Append(narg);
			}
			builder.Append('$');
			string rexpr = builder.ToString();
			OSCLog.WriteLine("Regular expression: " + rexpr);
			return new Regex(rexpr);
		}

		public static double GetCurrentTimeSeconds() {
			DateTime now = DateTime.Now;
			return DateTimeToSeconds(now);
		}
		public static ulong GetCurrentOSCTime() {
			// Note: There's a year 2038 problem here - we're already using the MSB... But that's an OSC standard problem.
			double seconds = GetCurrentTimeSeconds();
			return SecondsToOSCTime(seconds);
		}
		public static ulong SecondsToOSCTime(double seconds) {
			return (ulong)(seconds * (1 + (ulong)uint.MaxValue));
		}
		public static double OSCTimeToSeconds(ulong time) {
			return (double)time / (1 + (ulong)uint.MaxValue);
		}
		public static DateTime SecondsToDateTime(double seconds) {
			DateTime yearZero = new DateTime(1900, 1, 1); // Start of time in OSC
			return yearZero.AddSeconds(seconds);
		}
		public static double DateTimeToSeconds(DateTime date) {
			DateTime yearZero = new DateTime(1900, 1, 1); // Start of time in OSC
			TimeSpan elapsed = new TimeSpan(date.Ticks - yearZero.Ticks);
			return elapsed.TotalSeconds;
		}
	}
}
