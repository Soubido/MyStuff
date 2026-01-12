using System;
using System.Collections.Generic;
using Rhino.Geometry;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Core
{
    public static class RingProfileLibrary
    {
        // KONFIGURATION: RingWizard nutzt diesen Ordner
        public const string FolderName = "Profiles";

        public static List<string> GetProfileNames()
        {
            var defaults = new List<string> { "Rectangular", "Comfort Fit", "D-Shape", "Knife Edge", "Concave" };

            // Lade aus Ordner "Profiles"
            var custom = ProfileLoader.GetAvailableProfiles(FolderName);

            defaults.AddRange(custom);
            return defaults;
        }

        public static Curve GetOpenProfile(string name)
        {
            switch (name)
            {
                case "Comfort Fit": return CreateComfortFitOpen();
                case "D-Shape": return CreateDShapeOpen();
                case "Knife Edge": return CreateKnifeEdgeOpen();
                case "Concave": return CreateConcaveOpen();
                case "Rectangular": return CreateRectangleOpen();
                default:
                    // Lade aus Ordner "Profiles"
                    var loaded = ProfileLoader.LoadProfile(name, FolderName);
                    return loaded ?? CreateRectangleOpen();
            }
        }

        // ... Deine restlichen Methoden (CloseAndAnchor, CreateDShapeOpen, etc.) bleiben unverändert ...

        public static Curve GetClosedProfile(string name)
        {
            Curve open = GetOpenProfile(name);
            if (open == null) open = CreateRectangleOpen();
            return CloseAndAnchor(open);
        }

        public static Curve CloseAndAnchor(Curve openCurve)
        {
            if (openCurve == null) return CreateRectangleOpen();
            var crv = openCurve.DuplicateCurve();
            if (crv.IsClosed) return crv;
            Point3d start = crv.PointAtStart; Point3d end = crv.PointAtEnd;
            var line = new Line(end, start);
            var lineCurve = line.ToNurbsCurve();
            Point3d midPoint = line.PointAt(0.5);
            Transform trans = Transform.Translation(Point3d.Origin - midPoint);
            crv.Transform(trans); lineCurve.Transform(trans);
            var joined = Curve.JoinCurves(new Curve[] { crv, lineCurve });
            if (joined != null && joined.Length > 0) { var c = joined[0]; if (!c.IsClosed) c.MakeClosed(0.001); return c; }
            return crv;
        }
        private static Curve CreateRectangleOpen() { var p = new Polyline(); p.Add(-0.5, 0, 0); p.Add(-0.5, 0.5, 0); p.Add(0.5, 0.5, 0); p.Add(0.5, 0, 0); return p.ToNurbsCurve(); }
        private static Curve CreateDShapeOpen() { return new Arc(new Point3d(-0.5, 0, 0), new Point3d(0, 0.5, 0), new Point3d(0.5, 0, 0)).ToNurbsCurve(); }
        private static Curve CreateComfortFitOpen() { return new Arc(new Point3d(-0.5, 0, 0), new Point3d(0, 0.5, 0), new Point3d(0.5, 0, 0)).ToNurbsCurve(); }
        private static Curve CreateKnifeEdgeOpen() { var p = new Polyline(); p.Add(-0.5, 0, 0); p.Add(0, 0.5, 0); p.Add(0.5, 0, 0); return p.ToNurbsCurve(); }
        private static Curve CreateConcaveOpen() { var c1 = new Line(new Point3d(-0.5, 0, 0), new Point3d(-0.5, 0.5, 0)).ToNurbsCurve(); var c2 = new Arc(new Point3d(-0.5, 0.5, 0), new Point3d(0, 0.2, 0), new Point3d(0.5, 0.5, 0)).ToNurbsCurve(); var c3 = new Line(new Point3d(0.5, 0.5, 0), new Point3d(0.5, 0, 0)).ToNurbsCurve(); var joined = Curve.JoinCurves(new Curve[] { c1, c2, c3 }); return joined != null && joined.Length > 0 ? joined[0] : CreateRectangleOpen(); }
    }
}