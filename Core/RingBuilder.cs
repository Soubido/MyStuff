using System;
using System.Collections.Generic;
using System.Linq;
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

                // A. SMART REBUILD
                // Erhält Ecken, damit später separate Flächen entstehen
                shape = SmartRebuild(shape);
                if (shape == null) continue;

                // A.1 ORIENTATION
                if (shape.ClosedCurveOrientation(Vector3d.ZAxis) == CurveOrientation.Clockwise)
                    shape.Reverse();

                // A.2 SEAM ALIGNMENT
                BoundingBox rawBox = shape.GetBoundingBox(true);
                Point3d targetSeam = new Point3d((rawBox.Min.X + rawBox.Max.X) / 2.0, rawBox.Min.Y, 0);

                double tSeam;
                if (shape.ClosestPoint(targetSeam, out tSeam))
                {
                    shape.ChangeClosedCurveSeam(tSeam);
                }

                // B. SKALIEREN
                BoundingBox bbox = shape.GetBoundingBox(true);
                double currentW = bbox.Max.X - bbox.Min.X;
                double currentH = bbox.Max.Y - bbox.Min.Y;
                if (currentW < 0.001) currentW = 1.0;
                if (currentH < 0.001) currentH = 1.0;
                shape.Transform(Transform.Scale(Plane.WorldXY, slot.Width / currentW, slot.Height / currentH, 1.0));

                // C. ALIGNMENT
                bbox = shape.GetBoundingBox(true);
                double shiftUp = -bbox.Min.Y;
                shape.Transform(Transform.Translation(0, shiftUp, 0));

                // D. ROTATION
                if (Math.Abs(slot.Rotation) > 0.001)
                {
                    double rad = slot.Rotation * (Math.PI / 180.0);
                    shape.Rotate(rad, Vector3d.ZAxis, Point3d.Origin);
                }

                // E. OFFSET Y
                if (Math.Abs(slot.OffsetY) > 0.001)
                {
                    shape.Translate(slot.OffsetY, 0, 0);
                }

                // F. MAPPING
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
            sweep.AngleToleranceRadians = 0.02;
            sweep.SweepTolerance = 0.001;

            // HINWEIS: CheckCustomCurveStyle entfernt. 
            // Die Ecken werden durch die Geometrie der 'SmartRebuild' Kurven definiert.

            Brep[] breps = null;
            try { breps = sweep.PerformSweep(rail, sweepShapes, sweepParams); } catch { }

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
                b.Standardize();

                // CRITICAL: Zerlegt "Smooth" Surfaces an den Kinks in separate Faces.
                // Das sorgt dafür, dass Innenfläche, Außenfläche und Seitenwände separate BrepFaces werden.
                b.Faces.SplitKinkyFaces(RhinoMath.DefaultAngleTolerance, true);

                b.JoinNakedEdges(docTol);

                if (solid && !b.IsSolid)
                {
                    if (isClosedLoop)
                    {
                        b.JoinNakedEdges(docTol * 10.0);
                    }
                    else
                    {
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

        private static Curve SmartRebuild(Curve input)
        {
            if (input == null) return null;

            var simplified = input.Simplify(CurveSimplifyOptions.All, RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001, RhinoMath.DefaultAngleTolerance);
            if (simplified != null) input = simplified;

            if (!input.GetNextDiscontinuity(Continuity.G1_continuous, input.Domain.Min, input.Domain.Max, out double t))
            {
                return input.Rebuild(32, 3, true);
            }

            Curve[] segments = input.DuplicateSegments();
            if (segments == null || segments.Length == 0) return input.Rebuild(32, 3, true);

            var newSegments = new List<Curve>();
            foreach (var seg in segments)
            {
                int ptCount = 4;
                double len = seg.GetLength();
                if (len > 1.0) ptCount = (int)(len * 4);
                if (ptCount < 4) ptCount = 4;
                if (ptCount > 12) ptCount = 12;

                if (!seg.IsLinear(0.001))
                {
                    var rebuiltSeg = seg.Rebuild(ptCount, 3, true);
                    if (rebuiltSeg != null) newSegments.Add(rebuiltSeg);
                    else newSegments.Add(seg);
                }
                else
                {
                    newSegments.Add(seg);
                }
            }

            Curve[] joined = Curve.JoinCurves(newSegments, 0.01, true);
            if (joined != null && joined.Length == 1)
            {
                return joined[0];
            }

            return input;
        }
    }
}