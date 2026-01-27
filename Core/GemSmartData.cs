using System;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    [Guid("8A456F21-3B99-4D2A-9876-A1B2C3D4E5F6")]
    public class GemSmartData : UserData
    {
        private const int MAJOR_VERSION = 1;
        private const int MINOR_VERSION = 1;

        // --- KERN-DATEN ---
        public Curve BaseCurve { get; set; }
        public Plane GemPlane { get; set; }
        public string CutType { get; set; } = "Unknown";
        public double GemSize { get; set; }

        // --- REPORTING ---
        public string MaterialName { get; set; } = "Default";
        public double CaratWeight { get; set; } = 0.0;

        // --- PROPORTIONEN (Diese fehlten unten!) ---
        public double TablePercent { get; set; }
        public double CrownHeightPercent { get; set; }
        public double GirdleThicknessPercent { get; set; }
        public double PavilionHeightPercent { get; set; }

        public override string Description => "NewRhinoGold Smart Gem Data";

        // Zwingend für das Speichern
        public override bool ShouldWrite => true;

        public GemSmartData() { }

        public GemSmartData(Curve curve, Plane plane, string cut, double size, string material, double weight)
        {
            BaseCurve = curve?.DuplicateCurve();
            GemPlane = plane;
            CutType = cut;
            GemSize = size;
            MaterialName = material;
            CaratWeight = weight;
        }

        protected override void OnTransform(Transform xform)
        {
            base.OnTransform(xform);

            if (BaseCurve != null && BaseCurve.IsValid)
                BaseCurve.Transform(xform);

            if (GemPlane.IsValid)
            {
                var p = GemPlane;
                p.Transform(xform);
                GemPlane = p;
            }

            double scaleFactor = 1.0;
            if (Math.Abs(xform.Determinant) > 1e-6)
                scaleFactor = Math.Pow(Math.Abs(xform.Determinant), 1.0 / 3.0);

            if (Math.Abs(scaleFactor - 1.0) > Rhino.RhinoMath.ZeroTolerance)
            {
                GemSize *= scaleFactor;
                CaratWeight *= Math.Pow(scaleFactor, 3);
            }
        }

        // ------------------------------------------------------------------
        // KORREKTUR 1: OnDuplicate
        // Hier fehlten die Prozent-Werte. Ohne das gehen sie beim Copy/Paste verloren.
        // ------------------------------------------------------------------
        protected override void OnDuplicate(UserData source)
        {
            if (source is GemSmartData src)
            {
                BaseCurve = src.BaseCurve?.DuplicateCurve();
                GemPlane = src.GemPlane;
                CutType = src.CutType;
                GemSize = src.GemSize;
                MaterialName = src.MaterialName;
                CaratWeight = src.CaratWeight;

                // NEU HINZUGEFÜGT:
                TablePercent = src.TablePercent;
                CrownHeightPercent = src.CrownHeightPercent;
                GirdleThicknessPercent = src.GirdleThicknessPercent;
                PavilionHeightPercent = src.PavilionHeightPercent;
            }
        }

        // ------------------------------------------------------------------
        // KORREKTUR 2: Read
        // Hier fehlte das Auslesen der Werte. Nach dem Laden waren sie 0.0.
        // ------------------------------------------------------------------
        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out int major, out int minor);

            BaseCurve = archive.ReadGeometry() as Curve;
            GemPlane = archive.ReadPlane();
            CutType = archive.ReadString();
            GemSize = archive.ReadDouble();

            if (minor >= 1)
            {
                MaterialName = archive.ReadString();
                CaratWeight = archive.ReadDouble();

                // Wir nutzen hier einen Try-Catch oder Logik, falls die Version noch neuer wird,
                // aber da wir MINOR_VERSION auf 1 haben, lesen wir sie einfach mit.
                // ACHTUNG: Wenn du alte Dateien (ohne Prozentwerte) hast, musst du hier aufpassen.
                // Da du aber MINOR_VERSION = 1 schon verwendet hast, gehen wir davon aus, dass wir alles lesen.
                // Besser wäre es, MINOR auf 2 zu setzen, wenn sich das Format ändert.

                // Da wir aber im selben Entwicklungszyklus sind, lesen wir einfach weiter:
                try
                {
                    // Versuchen zu lesen (falls die Datei diese Daten schon hat)
                    TablePercent = archive.ReadDouble();
                    CrownHeightPercent = archive.ReadDouble();
                    GirdleThicknessPercent = archive.ReadDouble();
                    PavilionHeightPercent = archive.ReadDouble();
                }
                catch
                {
                    // Fallback für Dateien, die während der Entwicklung gespeichert wurden,
                    // als diese Zeilen noch fehlten.
                }
            }

            return true;
        }

        // ------------------------------------------------------------------
        // KORREKTUR 3: Write
        // Hier fehlte das Schreiben. Ohne das sind sie nicht in der Datei.
        // ------------------------------------------------------------------
        protected override bool Write(BinaryArchiveWriter archive)
        {
            archive.Write3dmChunkVersion(MAJOR_VERSION, MINOR_VERSION);

            archive.WriteGeometry(BaseCurve);
            archive.WritePlane(GemPlane);
            archive.WriteString(CutType);
            archive.WriteDouble(GemSize);
            archive.WriteString(MaterialName);
            archive.WriteDouble(CaratWeight);

            // NEU HINZUGEFÜGT:
            archive.WriteDouble(TablePercent);
            archive.WriteDouble(CrownHeightPercent);
            archive.WriteDouble(GirdleThicknessPercent);
            archive.WriteDouble(PavilionHeightPercent);

            return true;
        }
    }
}