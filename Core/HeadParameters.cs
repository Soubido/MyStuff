using System;
using System.Collections.Generic; // Wichtig für List<>
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public enum HeadProfileType
    {
        Round,
        Square,
        Rectangular,
        Custom
    }

    public class HeadParameters
    {
        // General
        public double Height { get; set; } = 5.0;
        public double DepthBelowGem { get; set; } = 2.0;
        public double GemInside { get; set; } = 30.0;

        // Prongs Dimensions
        public int ProngCount { get; set; } = 4;

        public double TopDiameter { get; set; } = 1.0;
        public double MidDiameter { get; set; } = 1.0;
        public double BottomDiameter { get; set; } = 1.0;

        // Prongs Shifts/Offsets
        public double TopOffset { get; set; } = 0.0;
        public double MidOffset { get; set; } = 0.0;
        public double BottomOffset { get; set; } = 0.0;

        // --- NEU: ROTATIONEN (0-360 Grad) ---
        public double TopProfileRotation { get; set; } = 0.0;
        public double MidProfileRotation { get; set; } = 0.0;
        public double BottomProfileRotation { get; set; } = 0.0;

        // --- AUFLAGE (RAILS) ---

        // Oben
        public bool EnableTopRail { get; set; } = true;
        public Guid TopRailProfileId { get; set; } = Guid.Empty;
        public double TopRailWidth { get; set; } = 0.8;
        public double TopRailThickness { get; set; } = 0.8;
        public double TopRailPosition { get; set; } = -0.5;
        public double TopRailOffset { get; set; } = 0.0; // NEU: Scale/Offset
        public double TopRailRotation { get; set; } = 0.0; // NEU

        // Unten
        public bool EnableBottomRail { get; set; } = true;
        public Guid BottomRailProfileId { get; set; } = Guid.Empty;
        public double BottomRailWidth { get; set; } = 0.8;
        public double BottomRailThickness { get; set; } = 0.8;
        public double BottomRailPosition { get; set; } = -2.0;
        public double BottomRailOffset { get; set; } = 0.0; // NEU: Scale/Offset
        public double BottomRailRotation { get; set; } = 0.0; // NEU

        // Profiles
        public Guid ProfileId { get; set; } = Guid.Empty;
        public HeadProfileType RailsProfile { get; set; } = HeadProfileType.Round;
        public Guid TopRailCustomProfileId { get; set; } = Guid.Empty;
        public Guid BottomRailCustomProfileId { get; set; } = Guid.Empty;

        // Speichert die Position jedes Prongs auf der Kurve (0.0 bis 1.0)
        public List<double> ProngPositions { get; set; } = new List<double>();




    }
}