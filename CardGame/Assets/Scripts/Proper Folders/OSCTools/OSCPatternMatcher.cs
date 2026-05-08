using System;
using System.Text;
using System.Text.RegularExpressions;

namespace OSCTools {

	public class OSCPatternMatcher {
		static Regex forbidden = null;
		static Regex special = null;
		static Regex forbiddenAddress = null;

		// OSC Pattern matching rules:
		//  ? matches any single char
		//  * matches any sequence of zero or more chars
		//  [abcd] matches any char in the enclosed set
		//  [a-d] matches any char in the range
		//  [!abcd] matches any char not in the enclosed char set (or range)
		//  {foo,bar} matches any of the words in the set

		// NOTE:
		// These characters must be escaped in a C# ReGex:
		//    \ * + ? | { [ ( ) ^ $ . #    [white space]
		// (Source: https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.escape?view=net-9.0 )
		// These characters are not allowed in OSC addresses:
		//    # / , * ? [ ] { }  [white space] [non-printable ASCII]
		// These characters have special meaning in OSC pattern strings:
		//        , * ? [ ] { }
		static void InitBasicRegexes() {
			string specialOSC_RE = @"[\*\{\}\[\]\?]";
			// NOTE: the printable ASCII characters, excluding space, are !..~. So the expression below matches anything
			//  containing # , / [whitespace] or non-printable ASCII.
			string forbiddenOSC_RE = @"[#\/\s]|[^!-~]";
			string forbiddenOSCA_RE = @"[#,\/\s\*\?\[\]\{\}]|[^!-~]";
			forbidden = new Regex(forbiddenOSC_RE);
			special = new Regex(specialOSC_RE);
			forbiddenAddress = new Regex(forbiddenOSCA_RE);
		}
		/// <summary>
		/// Returns true iff [word] contains special OSC pattern matching characters.
		/// That is:  * ? [ ] { }
		/// (Note: doesn't check for illegal characters such as # - use IsValidOSCString for that.)
		/// </summary>
		public static bool IsSpecialPattern(string pattern) {
			if (special == null) InitBasicRegexes();
			return special.IsMatch(pattern);
		}
		/// <summary>
		/// Returns true iff [word] does not contain any of the forbidden characters 
		/// (special pattern matching characters are allowed). That is:
		///  	# , /  [white space] [non-printable ASCII] 
		/// </summary>
		public static bool IsValidOSCString(string word) {
			if (forbidden == null) InitBasicRegexes();
			return !forbidden.IsMatch(word);
		}
		/// <summary>
		/// Returns true iff [word] is a valid OSC address.
		/// That means it does not contain any of
		///  	# , / * ? [ ] { }  [white space] [non-printable ASCII]
		/// </summary>
		public static bool IsValidAddress(string word) {
			if (forbiddenAddress == null) InitBasicRegexes();
			return !forbiddenAddress.IsMatch(word);
		}
	}
}
