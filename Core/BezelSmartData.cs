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

        public double Height { get; set; }
        public double Thickness { get; set; }
        public double Offset { get; set; }
        public Guid ParentGemId { get; set; }
        public Plane BasePlane { get; set; }

        public BezelSmartData() { }

        public BezelSmartData(double height, double thickness, double offset, Guid parentGemId, Plane basePlane)
        {
            Height = height;
            Thickness = thickness;
            Offset = offset;
            ParentGemId = parentGemId;
            BasePlane = basePlane;
        }

        public override string Description => "NewRhinoGold Smart Bezel Data";

        protected override void OnTransform(Transform xform)
        {
            base.OnTransform(xform);

            if (BasePlane.IsValid)
            {
                var p = BasePlane;
                p.Transform(xform);
                BasePlane = p;
            }

            // Fix CS1061: Skalierungsfaktor berechnen
            double scale = 1.0;
            if (Math.Abs(xform.Determinant) > 1e-6)
            {
                scale = Math.Pow(Math.Abs(xform.Determinant), 1.0 / 3.0);
            }

            if (Math.Abs(scale - 1.0) > Rhino.RhinoMath.ZeroTolerance)
            {
                Height *= scale;
                Thickness *= scale;
                Offset *= scale;
            }
        }

        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out int major, out int minor);
            Height = archive.ReadDouble();
            Thickness = archive.ReadDouble();
            Offset = archive.ReadDouble();
            ParentGemId = archive.ReadGuid();
            BasePlane = archive.ReadPlane();
            return true;
        }

        protected override bool Write(BinaryArchiveWriter archive)
        {
            archive.Write3dmChunkVersion(MAJOR_VERSION, MINOR_VERSION);
            archive.WriteDouble(Height);
            archive.WriteDouble(Thickness);
            archive.WriteDouble(Offset);
            archive.WriteGuid(ParentGemId);
            archive.WritePlane(BasePlane);
            return true;
        }
    }
}