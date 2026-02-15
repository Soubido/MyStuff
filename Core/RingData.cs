using Rhino.Geometry;
using NewRhinoGold.Helpers; // Wichtig für den Loader

namespace NewRhinoGold.Core
{
    public class RingProfileSlot
    {
        public double AngleRad { get; set; }      // Position auf dem Kreis in Bogenmaß (0..2PI)
        public double Width { get; set; }         // Breite in mm
        public double Height { get; set; }        // Höhe/Stärke in mm
        public string ProfileName { get; set; }   // Name des Profils (für UI/Logik)
        public Curve BaseCurve { get; set; }      // Die tatsächliche Profilkurve (unskaliert)

        // Optionale Parameter für fortgeschrittene Steuerung
        public double Rotation { get; set; } = 0;
        public double OffsetY { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public RingProfileSlot(double angleRad, Curve baseCurve, double width, double height)
        {
            AngleRad = angleRad;
            BaseCurve = baseCurve;
            Width = width;
            Height = height;
        }
    }
}