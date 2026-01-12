using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public class HeadParameters
    {
        public double Height { get; set; } = 5.0;
        public double DepthBelowGem { get; set; } = 2.0;
        public double GemInside { get; set; } = 30.0;
        public int ProngCount { get; set; } = 4;

        public double TopDiameter { get; set; } = 1.0;
        public double MidDiameter { get; set; } = 1.0;
        public double BottomDiameter { get; set; } = 1.0;

        public double TopOffset { get; set; } = 0.0;
        public double MidOffset { get; set; } = 0.0;
        public double BottomOffset { get; set; } = 0.0;

        public double TopProfileRotation { get; set; } = 0.0;
        public double MidProfileRotation { get; set; } = 0.0;
        public double BottomProfileRotation { get; set; } = 0.0;

        // Top Rail
        public bool EnableTopRail { get; set; } = true;
        // CHANGE: String statt Guid
        public string TopRailProfileName { get; set; } = "Round";
        public double TopRailWidth { get; set; } = 0.8;
        public double TopRailThickness { get; set; } = 0.8;
        public double TopRailPosition { get; set; } = -0.5;
        public double TopRailOffset { get; set; } = 0.0;
        public double TopRailRotation { get; set; } = 0.0;

        // Bottom Rail
        public bool EnableBottomRail { get; set; } = true;
        // CHANGE: String statt Guid
        public string BottomRailProfileName { get; set; } = "Round";
        public double BottomRailWidth { get; set; } = 0.8;
        public double BottomRailThickness { get; set; } = 0.8;
        public double BottomRailPosition { get; set; } = -2.0;
        public double BottomRailOffset { get; set; } = 0.0;
        public double BottomRailRotation { get; set; } = 0.0;

        // Main Profile (Prongs)
        // CHANGE: String statt Guid
        public string ProfileName { get; set; } = "Round";

        public List<double> ProngPositions { get; set; } = new List<double>();
    }
}