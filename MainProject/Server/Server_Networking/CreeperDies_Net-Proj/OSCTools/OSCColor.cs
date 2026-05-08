namespace OSCTools {
	public struct OSCColor {
		public byte R, G, B, A;
		public OSCColor(int pR, int pG, int pB, int pA) {
			R = (byte)pR;
			G = (byte)pG;
			B = (byte)pB;
			A = (byte)pA;
		}

		public override string ToString() {
			return $"R:{R} G:{G} B:{B} A:{A}";
		}
	}
}