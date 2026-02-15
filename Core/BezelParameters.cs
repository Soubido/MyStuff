using System;

namespace NewRhinoGold.Core
{
    public struct BezelParameters
    {
        // Dimensionen
        public double Height;
        public double ThicknessTop;
        public double ThicknessBottom;

        // Offsets
        public double Offset;      // Der "Gem Gap" (Abstand Stein <-> Metall)
        public double ZOffset;     // Vertikale Verschiebung

        // Seat (Auflage)
        public double SeatDepth;   // Wie tief sitzt der Stein
        public double SeatLedge;   // Auflagefläche

        // Features
        public double Chamfer;     // Tapering
        public double Bombing;     // Wölbung

        // Helper Property
        public double Thickness
        {
            get => ThicknessBottom;
            set => ThicknessBottom = value;
        }

        public static BezelParameters Default()
        {
            return new BezelParameters
            {
                Height = 3.0,
                ThicknessTop = 0.8,
                ThicknessBottom = 0.8,
                Offset = 0.1,         // Standard 0.1mm Luft
                ZOffset = 0.0,
                SeatDepth = 0.6,
                SeatLedge = 0.4,
                Chamfer = 0.0,
                Bombing = 0.0
            };
        }
    }
}