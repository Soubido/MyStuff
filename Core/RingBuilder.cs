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
            if (slots == null || slots.Length < 2) return null;

            // 1. RAIL (Front Ansicht = WorldXZ)
            // FIX: Manuelle Konstruktion der XZ-Ebene (Origin, X-Axis, Z-Axis)
            var plane = new Plane(Point3d.Origin, Vector3d.XAxis, Vector3d.ZAxis);

            var circle = new Circle(plane, radiusMM);

            // Start bei 6 Uhr (unten). 
            circle.Rotate(-Math.PI / 2.0, plane.Normal, plane.Origin);

            Curve rail = circle.ToNurbsCurve();
            rail.Domain = new Interval(0, 1.0);

            // 2. PROFILE VORBEREITEN
            var sweepShapes = new List<Curve>();
            var sweepParams = new List<double>();

            // Filter: Letztes Profil bei 360° (t=1.0) rauswerfen für Closed Sweep
            var validSlots = slots.Where(s => (s.AngleRad / (2.0 * Math.PI)) < 0.99).ToArray();

            foreach (var slot in validSlots)
            {
                if (slot.BaseCurve == null) continue;

                Curve shape = slot.BaseCurve.DuplicateCurve();

                // Skalieren
                BoundingBox bbox = shape.GetBoundingBox(true);
                double currentW = bbox.Max.X - bbox.Min.X;
                double currentH = bbox.Max.Y - bbox.Min.Y;

                if (currentW < 0.001) currentW = 1;
                if (currentH < 0.001) currentH = 1;

                double factorX = slot.Width / currentW;
                double factorY = slot.Height / currentH;

                // Scale (Basis ist WorldXY für das Profil)
                shape.Transform(Transform.Scale(Plane.WorldXY, factorX, factorY, 1.0));

                // Position berechnen
                double t = slot.AngleRad / (2.0 * Math.PI);

                Plane railFrame;
                rail.PerpendicularFrameAt(t, out railFrame);

                // Orientieren: Das Profil liegt flach auf WorldXY.
                // Es muss senkrecht auf den RailFrame transformiert werden.
                var orient = Transform.PlaneToPlane(Plane.WorldXY, railFrame);
                shape.Transform(orient);

                sweepShapes.Add(shape);
                sweepParams.Add(t);
            }

            // 3. SWEEP
            var sweep = new SweepOneRail();
            sweep.ClosedSweep = true;    // Ring schließen
            sweep.SetToRoadlikeTop();    // Oben bleibt Oben (wichtig für Profile)
            sweep.SweepTolerance = 0.001;

            var breps = sweep.PerformSweep(rail, sweepShapes, sweepParams);

            // Fallback
            if (breps == null || breps.Length == 0)
            {
                // Debug Extrusions (damit du siehst wo die Profile landen)
                var debugBreps = new List<Brep>();
                foreach (var s in sweepShapes)
                {
                    var extrusion = Surface.CreateExtrusion(s, Vector3d.YAxis * 0.1);
                    if (extrusion != null) debugBreps.Add(extrusion.ToBrep());
                }
                return debugBreps.ToArray();
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