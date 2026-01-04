using System;

namespace NewRhinoGold.Core
{
    public class CutterParameters
    {
        // --- GLOBAL ---
        public double GlobalScale { get; set; } = 100.0; // % des Steindurchmessers
        public double Clearance { get; set; } = 0.05;    // Abstand in mm (Offset)

        // --- TOP PART (Schaft oben) ---
        public double TopHeight { get; set; } = 100.0;   // % relative zur Steinhöhe oder fix
        public double TopDiameterScale { get; set; } = 100.0; // % Skalierung des Profils oben

        // --- SEAT (Auflage) ---
        public double SeatLevel { get; set; } = 20.0;    // % Position (wo der Seat endet)
        
        // --- BOTTOM PART (Schaft unten) ---
        public double BottomHeight { get; set; } = 150.0; // % Länge nach unten
        public double BottomDiameterScale { get; set; } = 70.0; // % Verjüngung unten

        // --- SHAPE (Reiter 2) ---
        public bool UseCustomProfile { get; set; } = false;
        public Guid ProfileId { get; set; } = Guid.Empty; // ID aus ProfileLibrary
        public double ProfileRotation { get; set; } = 0.0;
    }
}