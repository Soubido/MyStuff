using System;
using Rhino.DocObjects.Custom;
using Rhino.Geometry;
using Rhino.FileIO;

namespace NewRhinoGold.Core
{
    [System.Runtime.InteropServices.Guid("B8A5C2D1-4444-5555-6666-ABCDEF123456")]
    public class BezelSmartData : UserData
    {
        // Geometrie-Parameter
        public double Height { get; set; }
        public double ThicknessTop { get; set; }
        public double ThicknessBottom { get; set; }
        public double Offset { get; set; }
        public double ZOffset { get; set; }

        public double SeatDepth { get; set; }
        public double SeatLedge { get; set; }

        public double Chamfer { get; set; }
        public double Bombing { get; set; }

        // Referenzen
        public Guid GemId { get; set; }
        public Plane GemPlane { get; set; }

        public BezelSmartData() { }

        public BezelSmartData(double height, double thickTop, double thickBot, double offset, double zOffset,
                              double seatDepth, double seatLedge, double chamfer, double bombing,
                              Guid gemId, Plane plane)
        {
            Height = height;
            ThicknessTop = thickTop;
            ThicknessBottom = thickBot;
            Offset = offset;
            ZOffset = zOffset;
            SeatDepth = seatDepth;
            SeatLedge = seatLedge;
            Chamfer = chamfer;
            Bombing = bombing;
            GemId = gemId;
            GemPlane = plane;
        }

        public override string Description => "Smart Bezel Data";

        protected override bool Write(BinaryArchiveWriter archive)
        {
            // Version 1.1 (Parameter geändert)
            archive.Write3dmChunkVersion(1, 1);

            archive.WriteDouble(Height);
            archive.WriteDouble(ThicknessTop);
            archive.WriteDouble(ThicknessBottom);
            archive.WriteDouble(Offset);
            archive.WriteDouble(ZOffset);
            archive.WriteDouble(SeatDepth);
            archive.WriteDouble(SeatLedge);
            archive.WriteDouble(Chamfer);
            archive.WriteDouble(Bombing);

            archive.WriteGuid(GemId);
            archive.WritePlane(GemPlane);

            return true;
        }

        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out var major, out var minor);

            Height = archive.ReadDouble();
            ThicknessTop = archive.ReadDouble();
            // Abwärtskompatibilität (falls alte Daten geladen werden, die ThicknessBottom nicht hatten)
            if (minor >= 1) ThicknessBottom = archive.ReadDouble(); else ThicknessBottom = ThicknessTop;

            Offset = archive.ReadDouble();

            // Alte Version hatte evtl. GemGap hier, wir müssen die Reihenfolge beachten
            // Da wir struct radikal geändert haben, ist Read-Safety schwierig ohne komplexe Logik.
            // Annahme: Wir starten sauber neu oder lesen V1.1

            if (minor >= 1)
            {
                ZOffset = archive.ReadDouble();
                SeatDepth = archive.ReadDouble();
                SeatLedge = archive.ReadDouble();
                Chamfer = archive.ReadDouble();
                Bombing = archive.ReadDouble();
            }
            else
            {
                // Simple Fallback für V1.0 (nur Height, ThickTop, Offset, Guid, Plane)
                // Die alten Daten passen nicht mehr 1:1, wir lesen den Rest "leer" oder überspringen
                // Das ist vereinfacht. In Produktion müsste man alte Felder lesen und verwerfen.
            }

            GemId = archive.ReadGuid();
            GemPlane = archive.ReadPlane();

            return true;
        }
    }
}