using System;
using Rhino.Geometry;

namespace NewRhinoGold.BezelStudio
{
    /// <summary>
    /// Eine temporäre Datenstruktur, um Informationen über platzierte Steine
    /// vom interaktiven Tool (GetPoint) zurück an den Dialog zu senden.
    /// </summary>
    public class TempGem
    {
        public Point3d Position { get; set; }
        public Vector3d Normal { get; set; }
        
        // Die visuelle Repräsentation (bereits transformiert)
        public Brep Geometry { get; set; }
        
        // Der grüne/rote Abstandsring (nur für Vorschau)
        public Curve Bumper { get; set; }
        
        // Status
        public bool IsValid { get; set; } = true;
        public bool IsCollision { get; set; } = false;
    }
}