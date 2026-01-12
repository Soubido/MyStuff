using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public static class RingBuilder
    {
        public static Brep[] BuildRing(double radiusMM, RingProfileSlot[] slots, bool isClosedLoop, bool solid)
        {
            if (radiusMM < 1.0) radiusMM = 8.0;
            if (slots == null || slots.Length < 1) return null;

            // 1. RAIL (Schiene)
            var plane = new Plane(Point3d.Origin, Vector3d.XAxis, Vector3d.ZAxis);
            var circle = new Circle(plane, radiusMM);
            // Wir rotieren den Startpunkt nach unten (-90°), damit der Seam unten liegt (üblicher für Ringe)
            circle.Rotate(-Math.PI / 2.0, plane.Normal, plane.Origin);
            Curve rail = circle.ToNurbsCurve();
            rail.Domain = new Interval(0, 1.0);

            // 2. PROFILE PROCESSING
            var sweepShapes = new List<Curve>();
            var sweepParams = new List<double>();
            Vector3d sideways = Vector3d.YAxis;

            foreach (var slot in slots)
            {
                if (slot.BaseCurve == null) continue;

                double t = slot.AngleRad / (2.0 * Math.PI);
                if (t < 0.0) t = 0.0; if (t > 1.0) t = 1.0;

                Curve shape = slot.BaseCurve.DuplicateCurve();

                // A. SKALIEREN
                BoundingBox bbox = shape.GetBoundingBox(true);
                double currentW = bbox.Max.X - bbox.Min.X;
                double currentH = bbox.Max.Y - bbox.Min.Y;
                if (currentW < 0.001) currentW = 1.0;
                if (currentH < 0.001) currentH = 1.0;
                shape.Transform(Transform.Scale(Plane.WorldXY, slot.Width / currentW, slot.Height / currentH, 1.0));

                // B. ALIGNMENT (Auf Rail setzen)
                bbox = shape.GetBoundingBox(true);
                double shiftUp = -bbox.Min.Y;
                shape.Transform(Transform.Translation(0, shiftUp, 0));

                // C. ROTATION
                if (Math.Abs(slot.Rotation) > 0.001)
                {
                    double rad = slot.Rotation * (Math.PI / 180.0);
                    shape.Rotate(rad, Vector3d.ZAxis, Point3d.Origin);
                }

                // D. OFFSET Y (Sideways Shift)
                if (Math.Abs(slot.OffsetY) > 0.001)
                {
                    shape.Translate(slot.OffsetY, 0, 0);
                }

                // E. MAPPING
                Point3d railPoint = rail.PointAt(t);
                Vector3d radial = railPoint - Point3d.Origin;
                radial.Unitize();
                var targetPlane = new Plane(railPoint, sideways, radial);
                var orient = Transform.PlaneToPlane(Plane.WorldXY, targetPlane);
                shape.Transform(orient);

                sweepShapes.Add(shape);
                sweepParams.Add(t);
            }

            // 3. SWEEP
            var sweep = new SweepOneRail();
            sweep.ClosedSweep = isClosedLoop;
            // Toleranzen etwas lockern für Sweep, damit er leichter schließt
            sweep.AngleToleranceRadians = 0.02;
            sweep.SweepTolerance = 0.01;

            Brep[] breps = null;
            try { breps = sweep.PerformSweep(rail, sweepShapes, sweepParams); } catch { }

            // Fallback: Wenn ClosedSweep fehlschlägt, versuche es offen
            if ((breps == null || breps.Length == 0) && isClosedLoop)
            {
                sweep.ClosedSweep = false;
                breps = sweep.PerformSweep(rail, sweepShapes, sweepParams);
            }

            if (breps == null || breps.Length == 0) return null;

            // 4. SOLID & VALIDITY CHECK
            var result = new List<Brep>();
            double docTol = RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;

            foreach (var b in breps)
            {
                // Standardize richtet Knot-Vectors, Faces und Edges aus
                b.Standardize();

                // Versuch 1: Einfaches Joinen von Naked Edges (für die Naht)
                b.JoinNakedEdges(docTol);

                if (solid && !b.IsSolid)
                {
                    if (isClosedLoop)
                    {
                        // Bei Ring: Naht schließen.
                        // Manchmal ist die Lücke etwas größer als die Toleranz.
                        b.JoinNakedEdges(docTol * 10.0);

                        // Wenn immer noch nicht solid, ist es kaputt oder verdreht
                        if (!b.IsSolid)
                        {
                            // Letzte Rettung: Brep.Join (falls Sweep mehrere Teile ausgespuckt hat, was er bei single rail selten tut)
                            // Aber hier haben wir nur ein Brep b.
                            // Wir belassen es dabei und lassen den Validator im Wizard entscheiden.
                        }
                    }
                    else
                    {
                        // Bei offener Schiene: Deckflächen (Caps) drauf
                        var capped = b.CapPlanarHoles(docTol);
                        if (capped != null)
                        {
                            result.Add(capped);
                            continue;
                        }
                    }
                }
                result.Add(b);
            }
            return result.ToArray();
        }
    }
}