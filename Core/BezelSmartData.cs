using System;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    [Guid("9B567A22-4C11-5E3B-1234-B2C3D4E5F6A7")]
    public class BezelSmartData : UserData
    {
        private const int MAJOR_VERSION = 1;
        private const int MINOR_VERSION = 0;

        // Eigenschaften an Dialog angepasst (Thickness -> ThicknessTop, ParentGemId -> GemId, etc.)
        public double Height { get; set; }
        public double ThicknessTop { get; set; }
        public double Offset { get; set; }
        public Guid GemId { get; set; }
        public Plane GemPlane { get; set; }

        public BezelSmartData() { }

        // Konstruktor passend zum Aufruf in BezelStudioDlg
        public BezelSmartData(double height, double thicknessTop, double offset, Guid gemId, Plane gemPlane)
        {
            Height = height;
            ThicknessTop = thicknessTop;
            Offset = offset;
            GemId = gemId;
            GemPlane = gemPlane;
        }

        public override string Description => "NewRhinoGold Smart Bezel Data";

        protected override void OnTransform(Transform xform)
        {
            base.OnTransform(xform);

            if (GemPlane.IsValid)
            {
                var p = GemPlane;
                p.Transform(xform);
                GemPlane = p;
            }

            double scale = 1.0;
            if (Math.Abs(xform.Determinant) > 1e-6)
            {
                scale = Math.Pow(Math.Abs(xform.Determinant), 1.0 / 3.0);
            }

            if (Math.Abs(scale - 1.0) > Rhino.RhinoMath.ZeroTolerance)
            {
                Height *= scale;
                ThicknessTop *= scale;
                Offset *= scale;
            }
        }

        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out int major, out int minor);
            Height = archive.ReadDouble();
            ThicknessTop = archive.ReadDouble();
            Offset = archive.ReadDouble();
            GemId = archive.ReadGuid();
            GemPlane = archive.ReadPlane();
            return true;
        }

        protected override bool Write(BinaryArchiveWriter archive)
        {
            archive.Write3dmChunkVersion(MAJOR_VERSION, MINOR_VERSION);
            archive.WriteDouble(Height);
            archive.WriteDouble(ThicknessTop);
            archive.WriteDouble(Offset);
            archive.WriteGuid(GemId);
            archive.WritePlane(GemPlane);
            return true;
        }
    }
}