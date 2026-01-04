using Rhino.Geometry;
using NewRhinoGold.Helpers; // Wichtig für den Loader

namespace NewRhinoGold.Core
{
	public class RingProfileSlot
	{
		public bool IsActive { get; set; } = false;
		public int Index { get; set; } // 0..35

		// Dimensionen
		public double Width { get; set; } = 4.0;
		public double Height { get; set; } = 2.0;

		// Ausrichtung
		public double PositionOffset { get; set; } = 0.0;
		public double Rotation { get; set; } = 0.0;
		public double RotationV { get; set; } = 0.0;

		// Flags
		public bool InvertDirection { get; set; } = false;

		// NEU: Name der Datei (z.B. "001", "Heart", "Rect")
		public string ProfileName { get; set; } = "001";

		public RingProfileSlot(int index)
		{
			Index = index;
			// Standardmäßig laden wir das erste verfügbare oder "Default"
			var profiles = ProfileLoader.GetAvailableProfiles();
			if (profiles.Count > 0) ProfileName = profiles[0];
		}

		// Hilfsmethode, um die echte Geometrie zu holen
		public Curve GetGeometry()
		{
			return ProfileLoader.LoadProfile(ProfileName);
		}
	}
}