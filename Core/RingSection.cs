using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public class RingSection
    {
        public double Parameter { get; set; } // Position auf der Schiene (0.0 bis 1.0)
        public double Width { get; set; }
        public double Height { get; set; }
        
        // Die Form des Profils (z.B. Rechteck, Oval)
        public Curve ProfileCurve { get; set; } 
        
        // Name zur Identifikation in der UI (z.B. "Top", "Side")
        public string Name { get; set; } 

        // Konstruktor für Standardwerte
        public RingSection()
        {
            Width = 3.0;
            Height = 1.5;
        }
    }
}