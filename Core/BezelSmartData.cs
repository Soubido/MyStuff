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

        // -------------------------------------------------------------
        // KORREKTUR 1: ZWINGEND ERFORDERLICH ZUM SPEICHERN
        // -------------------------------------------------------------
        public override bool ShouldWrite => true;

        // -------------------------------------------------------------
        // KORREKTUR 2: DEEP COPY FÜR COPY/PASTE SUPPORT
        // -------------------------------------------------------------
        protected override void OnDuplicate(UserData source)
        {
            if (source is BezelSmartData src)
            {
                this.Height = src.Height;
                this.ThicknessTop = src.ThicknessTop;
                this.ThicknessBottom = src.ThicknessBottom;
                this.Offset = src.Offset;
                this.ZOffset = src.ZOffset;
                this.SeatDepth = src.SeatDepth;
                this.SeatLedge = src.SeatLedge;
                this.Chamfer = src.Chamfer;
                this.Bombing = src.Bombing;
                this.GemId = src.GemId;
                this.GemPlane = src.GemPlane;
            }
        }

        protected override bool Write(BinaryArchiveWriter archive)
        {
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

            if (minor >= 1) ThicknessBottom = archive.ReadDouble(); else ThicknessBottom = ThicknessTop;

            Offset = archive.ReadDouble();

            if (minor >= 1)
            {
                ZOffset = archive.ReadDouble();
                SeatDepth = archive.ReadDouble();
                SeatLedge = archive.ReadDouble();
                Chamfer = archive.ReadDouble();
                Bombing = archive.ReadDouble();
            }

            GemId = archive.ReadGuid();
            GemPlane = archive.ReadPlane();

            return true;
        }
    }
}