using System;

namespace NewRhinoGold.Core
{
    public struct BezelParameters
    {
        public double Height;
        public double ThicknessTop;
        public double ThicknessBottom;

        public double Thickness
        {
            get => ThicknessBottom;
            set => ThicknessBottom = value;
        }

        public double Offset;      // Gap zum Stein
        public double GemGap;      // Zusätzlicher Abstand Cutter
        public bool CreateCutter;

        public double SeatDepth;   // Tiefe der Auflage (Abstand Top -> Seat)
        public double SeatLedge;   // Breite der Auflage
        public double ZOffset;     // Manueller Zusatz-Versatz

        public double Chamfer;     // Konisch: Verjüngung unten
        public double Bombing;     // Wölbung der Außenwand

        public static BezelParameters Default()
        {
            return new BezelParameters
            {
                Height = 2.0,
                ThicknessTop = 0.8,
                ThicknessBottom = 0.8,
                Offset = 0.1,
                GemGap = 0.05,
                CreateCutter = true,
                SeatDepth = 0.6,
                SeatLedge = 0.4,
                ZOffset = 0.0,
                Chamfer = 0.0,
                Bombing = 0.0
            };
        }
    }
}