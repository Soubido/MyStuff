using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public struct GemParameters
    {
        public double H1; // Crown Height
        public double H2; // Girdle Thickness
        public double H3; // Pavilion Depth
        public double Table; // Table Width %
    }

    /// <summary>
    /// Zentrale Logik zur Erzeugung eines Stein-Volumens aus einer Kurve.
    /// </summary>
    public static class GemBuilder
    {
        public static Brep CreateGem(Curve baseCurve, GemParameters p, double targetSize)
        {
            if (baseCurve == null) return null;

            // 1. Skalierung auf Zielgröße (X-Achse Referenz)
            var bb = baseCurve.GetBoundingBox(true);
            double currentWidth = bb.Max.X - bb.Min.X;
            if (currentWidth < 0.001) return null;

            var c2 = baseCurve.DuplicateCurve();
            // Zentrieren
            c2.Translate((Vector3d)(Point3d.Origin - bb.Center));
            
            // Skalieren
            double scale = targetSize / currentWidth;
            c2.Transform(Transform.Scale(Point3d.Origin, scale));

            // Parameter berechnen (mm oder %)
            // Wir nehmen an, die Parameter p kommen bereits als absolute Werte oder müssen interpretiert werden.
            // Für diesen Builder erwarten wir absolute Werte relativ zur Skalierung.
            
            // Wenn p als % gedacht war, müssen sie hier umgerechnet werden.
            // Wir nehmen an, p.H1 etc. sind bereits skalierte MM-Werte für diesen Kontext.

            // Girdle Curves
            Curve girdleTop = c2.DuplicateCurve();
            Curve girdleBottom = c2.DuplicateCurve();
            girdleBottom.Translate(0, 0, -p.H2);

            // Table Curve
            // Table % bezieht sich auf width
            double tableScale = p.Table / 100.0; 
            var crownCurve = c2.DuplicateCurve();
            crownCurve.Transform(Transform.Scale(Point3d.Origin, tableScale));
            crownCurve.Translate(0, 0, p.H1);

            // Planar machen (Safety)
            if (!crownCurve.IsPlanar()) 
                crownCurve = Curve.ProjectToPlane(crownCurve, new Plane(new Point3d(0,0,p.H1), Vector3d.ZAxis));

            // Apex
            Point3d apex = Point3d.Origin;
            apex.Z = -p.H2 - p.H3;

            // Lofts
            var breps = new List<Brep>();
            
            // Crown
            var crownLoft = Brep.CreateFromLoft(new[] { crownCurve, girdleTop }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (crownLoft != null) breps.AddRange(crownLoft);

            // Girdle
            if (p.H2 > 0.001)
            {
                var girdleLoft = Brep.CreateFromLoft(new[] { girdleTop, girdleBottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (girdleLoft != null) breps.AddRange(girdleLoft);
            }

            // Pavilion
            // Helper Circle für Apex
            var apexCrv = new Circle(new Plane(apex, Vector3d.ZAxis), 0.001).ToNurbsCurve();
            var pavLoft = Brep.CreateFromLoft(new[] { girdleBottom, apexCrv }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (pavLoft != null) breps.AddRange(pavLoft);

            var finalBrep = Brep.JoinBreps(breps, 0.001);
            if (finalBrep != null && finalBrep.Length > 0)
            {
                return finalBrep[0].CapPlanarHoles(0.001);
            }

            return null;
        }
    }
}