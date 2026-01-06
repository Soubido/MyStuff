using System;
using System.Collections.Generic;
using Rhino.Geometry;
using System.Linq;
using Rhino;

namespace NewRhinoGold.Core
{
    public static class RingBuilder
    {
        public static Brep[] BuildRing(double radiusMM, RingProfileSlot[] slots, bool solid)
        {
            if (radiusMM < 1.0) radiusMM = 8.0;
            // Mindestens 1 Slot
            if (slots == null || slots.Length < 1) return null;

            // 1. RAIL (Finger-Innenseite)
            var plane = new Plane(Point3d.Origin, Vector3d.XAxis, Vector3d.ZAxis);
            var circle = new Circle(plane, radiusMM);

            // Start unten bei 6 Uhr (-90°)
            circle.Rotate(-Math.PI / 2.0, plane.Normal, plane.Origin);

            Curve rail = circle.ToNurbsCurve();
            rail.Domain = new Interval(0, 1.0);

            // 2. PROFILE VORBEREITEN
            var sweepShapes = new List<Curve>();
            var sweepParams = new List<double>();

            foreach (var slot in slots)
            {
                if (slot.BaseCurve == null) continue;

                Curve shape = slot.BaseCurve.DuplicateCurve();

                // A. Skalieren
                BoundingBox bbox = shape.GetBoundingBox(true);
                double currentW = bbox.Max.X - bbox.Min.X;
                double currentH = bbox.Max.Y - bbox.Min.Y;
                if (currentW < 0.001) currentW = 1;
                if (currentH < 0.001) currentH = 1;

                double factorX = slot.Width / currentW;
                double factorY = slot.Height / currentH;

                // Skalieren um WorldXY Origin
                shape.Transform(Transform.Scale(Plane.WorldXY, factorX, factorY, 1.0));

                // B. NACH AUSSEN BAUEN (Alignment)
                // Profil so verschieben, dass MinY (unten) auf 0 liegt.
                bbox = shape.GetBoundingBox(true);
                double shiftUp = -bbox.Min.Y;
                shape.Transform(Transform.Translation(0, shiftUp, 0));

                // C. Manuelle Frame-Berechnung (WICHTIG!)
                // Wir berechnen exakt, wie das Profil auf dem Ring sitzen muss.

                double t = slot.AngleRad / (2.0 * Math.PI);
                if (t > 1.0) t = 1.0;

                Point3d railPoint = rail.PointAt(t);

                // Vektoren berechnen:
                // 1. Tangente (Richtung der Schiene)
                Vector3d tangent = rail.TangentAt(t);

                // 2. Radial (Dicke des Rings, nach Aussen)
                // Da Zentrum (0,0,0), ist Vektor = Point - Origin.
                Vector3d radial = railPoint - Point3d.Origin;
                radial.Unitize();

                // 3. Breite (Seitwärts, entlang Welt-Y für diesen Ring in XZ)
                Vector3d sideways = Vector3d.YAxis;

                // Ziel-Frame erstellen:
                // Origin = Punkt auf Rail
                // X-Achse (Profil-Breite) = Sideways (Welt-Y)
                // Y-Achse (Profil-Höhe) = Radial (Nach Aussen)
                // Z-Achse (Profil-Normale) = Tangent (Entlang Rail)

                var targetPlane = new Plane(railPoint, sideways, radial);

                // Mapping: WorldXY -> TargetPlane
                // World X (Profilbreite) landet auf Target X (Sideways)
                // World Y (Profilhöhe) landet auf Target Y (Radial/Outwards)
                var orient = Transform.PlaneToPlane(Plane.WorldXY, targetPlane);
                shape.Transform(orient);

                sweepShapes.Add(shape);
                sweepParams.Add(t);
            }

            // 3. SWEEP
            var sweep = new SweepOneRail();
            // ClosedSweep = false, weil wir Anfang und Ende (t=0 und t=1) manuell übergeben haben.
            // Das verhindert Probleme an der Nahtstelle.
            sweep.ClosedSweep = false;

            // Kein "RoadlikeTop" nötig, da wir die Frames manuell perfekt ausgerichtet haben.
            // Freeform lässt die Interpolation natürlich fließen.
            sweep.AngleToleranceRadians = 0.01;
            sweep.SweepTolerance = 0.001;

            Brep[] breps = null;
            try
            {
                // Versuch mit Parametern
                breps = sweep.PerformSweep(rail, sweepShapes, sweepParams);
            }
            catch
            {
                // Fallback
            }

            if (breps == null || breps.Length == 0)
            {
                RhinoApp.WriteLine("FEHLER: Sweep fehlgeschlagen.");
                return null;
            }

            // 4. CAPPING
            if (solid)
            {
                var result = new List<Brep>();
                foreach (var b in breps)
                {
                    var capped = b.CapPlanarHoles(0.001);
                    result.Add(capped ?? b);
                }
                return result.ToArray();
            }

            return breps;
        }
    }
}