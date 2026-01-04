using System;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    /// <summary>
    /// SmartData Backbone für Edelsteine.
    /// Speichert die parametrische Definition direkt an der Geometrie.
    /// Reagiert automatisch auf Rhino-Transformationen (Move, Rotate, Scale).
    /// </summary>
    [Guid("8A456F21-3B99-4D2A-9876-A1B2C3D4E5F6")]
    public class GemSmartData : UserData
    {
        private const int MAJOR_VERSION = 1;
        private const int MINOR_VERSION = 1; // Version erhöht für neue Felder

        // Kern-Daten
        public Curve BaseCurve { get; set; }
        public Plane GemPlane { get; set; }
        public string CutType { get; set; } = "Unknown";
        public double GemSize { get; set; }

        // NEU: Für Reporting & Stücklisten
        public string MaterialName { get; set; } = "Default";
        public double CaratWeight { get; set; } = 0.0;

        public GemSmartData()
        {
        }

        // Konstruktor erweitert
        public GemSmartData(Curve curve, Plane plane, string cut, double size, string material, double weight)
        {
            BaseCurve = curve?.DuplicateCurve();
            GemPlane = plane;
            CutType = cut;
            GemSize = size;
            MaterialName = material;
            CaratWeight = weight;
        }

        public override string Description => "NewRhinoGold Smart Gem Data";

        protected override void OnTransform(Transform xform)
        {
            base.OnTransform(xform);

            if (BaseCurve != null && BaseCurve.IsValid)
            {
                BaseCurve.Transform(xform);
            }

            if (GemPlane.IsValid)
            {
                var p = GemPlane;
                p.Transform(xform);
                GemPlane = p;
            }

            // Skalierungsfaktor berechnen (Determinante^1/3 für 3D Uniform Scale)
            double scaleFactor = 1.0;
            if (Math.Abs(xform.Determinant) > 1e-6)
            {
                scaleFactor = Math.Pow(Math.Abs(xform.Determinant), 1.0 / 3.0);
            }

            if (Math.Abs(scaleFactor - 1.0) > Rhino.RhinoMath.ZeroTolerance)
            {
                GemSize *= scaleFactor;
                // Gewicht ändert sich bei Skalierung kubisch (Volumen)
                // Wir passen es hier an, damit der Report auch nach Skalierung stimmt
                CaratWeight *= Math.Pow(scaleFactor, 3);
            }
        }

        protected override void OnDuplicate(UserData source)
        {
            if (source is GemSmartData src)
            {
                BaseCurve = src.BaseCurve?.DuplicateCurve();
                GemPlane = src.GemPlane;
                CutType = src.CutType;
                GemSize = src.GemSize;

                // Neue Felder kopieren
                MaterialName = src.MaterialName;
                CaratWeight = src.CaratWeight;
            }
        }

        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out int major, out int minor);

            BaseCurve = archive.ReadGeometry() as Curve;
            GemPlane = archive.ReadPlane();
            CutType = archive.ReadString();
            GemSize = archive.ReadDouble();

            // Versions-Check für Abwärtskompatibilität
            if (minor >= 1)
            {
                MaterialName = archive.ReadString();
                CaratWeight = archive.ReadDouble();
            }

            return true;
        }

        protected override bool Write(BinaryArchiveWriter archive)
        {
            archive.Write3dmChunkVersion(MAJOR_VERSION, MINOR_VERSION);

            archive.WriteGeometry(BaseCurve);
            archive.WritePlane(GemPlane);
            archive.WriteString(CutType);
            archive.WriteDouble(GemSize);

            // Neue Felder schreiben
            archive.WriteString(MaterialName);
            archive.WriteDouble(CaratWeight);

            return true;
        }
    }
}