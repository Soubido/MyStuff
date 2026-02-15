using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public class RingSection
    {
        public string Name { get; set; }
        public double Parameter { get; set; } // 0.0 bis 1.0

        // Dimensionen
        public double Width { get; set; }
        public double Height { get; set; }

        // --- NEUE PARAMETER ---
        public double Rotation { get; set; } = 0; // in Grad
        public double OffsetY { get; set; } = 0;  // in mm (Verschiebung vom Rail weg)

        // Profil
        public string ProfileName { get; set; }
        public Curve CustomProfileCurve { get; set; }

        // Status
        public bool IsActive { get; set; } = true;
        public bool IsModified { get; set; } = false;
        public bool FlipX { get; set; } = false;
        public bool FlipY { get; set; } = false;
    }
}