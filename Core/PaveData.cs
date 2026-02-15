using System;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    // Die Art des Musters
    public enum PavePattern
    {
        GridStack,
        HexOffset,
        RandomPacking
    }

    // Definition eines Steins (Was wird gesetzt?)
    public class PaveStoneDefinition
    {
        public string Name { get; set; } = "Gem";
        public double Diameter { get; set; } = 1.0;
        public string Shape { get; set; } = "Round";
        public string Material { get; set; } = "Diamond";
        public double Probability { get; set; } = 1.0;

        public override string ToString() => $"{Material} {Shape} Ø{Diameter}mm";
    }

    // Ein konkreter gesetzter Stein (Instanz)
    public class PaveInstance
    {
        public Point3d Position { get; set; }
        public Vector3d Normal { get; set; }
        public PaveStoneDefinition Definition { get; set; }

        // WICHTIG: U und V sind jetzt fester Bestandteil der Klasse
        public double U { get; set; }
        public double V { get; set; }

        public Plane GetPlane()
        {
            return new Plane(Position, Normal);
        }
    }
}