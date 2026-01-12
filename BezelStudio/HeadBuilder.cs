using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using NewRhinoGold.Core;

namespace NewRhinoGold.BezelStudio
{
    public static class HeadBuilder
    {
        public static List<Brep> CreateHead(Curve gemCurve, Plane plane, HeadParameters p)
        {
            var result = new List<Brep>();
            if (gemCurve == null || !plane.IsValid) return result;

            var baseCrv = gemCurve.DuplicateCurve();
            if (!baseCrv.IsPlanar()) baseCrv = Curve.ProjectToPlane(baseCrv, plane);
            if (!baseCrv.IsClosed) baseCrv.MakeClosed(0.001);
            baseCrv.Domain = new Interval(0, 1);

            // --- AUFLAGE (RAILS) ---
            if (p.EnableTopRail)
            {
                var rail = CreateRailMiteredLoft(baseCrv, plane, p.TopRailPosition, p.TopRailWidth, p.TopRailThickness, p.TopRailProfileName, p.TopRailOffset, p.TopRailRotation);
                if (IsValid(rail)) result.Add(rail);
            }

            if (p.EnableBottomRail)
            {
                double totalBotOffset = -0.4 + p.BottomRailOffset;
                var rail = CreateRailMiteredLoft(baseCrv, plane, p.BottomRailPosition, p.BottomRailWidth, p.BottomRailThickness, p.BottomRailProfileName, totalBotOffset, p.BottomRailRotation);
                if (IsValid(rail)) result.Add(rail);
            }

            // --- PRONGS ---
            if (p.ProngPositions != null && p.ProngPositions.Count > 0)
            {
                foreach (double tPercent in p.ProngPositions)
                {
                    try
                    {
                        double t = tPercent;
                        while (t > 1.0) t -= 1.0; while (t < 0.0) t += 1.0;
                        var prong = CreateProng(baseCrv, t, plane, p);
                        if (IsValid(prong)) result.Add(prong);
                    }
                    catch { }
                }
            }
            return result;
        }

        private static bool IsValid(Brep b) => b != null && b.IsValid;

        // --- RAILS ---
        private static Brep CreateRailMiteredLoft(Curve railPath, Plane plane, double zPos, double width, double height, string profileName, double offset, double rotationDeg)
        {
            if (railPath == null) return null;
            if (width < 0.01) width = 0.01;
            if (height < 0.01) height = 0.01;

            Curve path = railPath.DuplicateCurve();
            bool useManualShift = false;

            if (Math.Abs(offset) > 0.001)
            {
                Curve offsetCrv = OffsetCurveRobust(path, plane, offset);
                if (offsetCrv != null) path = offsetCrv;
                else useManualShift = true;
            }

            path.Translate(plane.ZAxis * zPos);

            // CHANGE: Lade Profil aus HeadProfileLibrary mit String
            Curve sourceProfile = HeadProfileLibrary.GetCurve(profileName);
            if (sourceProfile == null) sourceProfile = new Circle(Plane.WorldXY, 1.0).ToNurbsCurve();

            BoundingBox bbox = sourceProfile.GetBoundingBox(true);
            double origW = bbox.Max.X - bbox.Min.X; if (origW < 0.001) origW = 1.0;
            double origH = bbox.Max.Y - bbox.Min.Y; if (origH < 0.001) origH = 1.0;

            var tParams = new List<double>();
            double tStart = path.Domain.Min;
            double tEnd = path.Domain.Max;
            tParams.Add(tStart);

            double tKink = tStart;
            while (true)
            {
                if (!path.GetNextDiscontinuity(Continuity.C1_continuous, tKink, tEnd, out double nextKink)) break;
                tParams.Add(nextKink);
                tKink = nextKink;
            }

            int smoothSamples = 60;
            double[] divided = path.DivideByCount(smoothSamples, true);
            if (divided != null) tParams.AddRange(divided);
            tParams.Sort();

            var loftCurves = new List<Curve>();
            double lastT = -999.0;

            foreach (double t in tParams)
            {
                if (Math.Abs(t - lastT) < 1e-5) continue;
                if (t >= tEnd - 1e-5 && loftCurves.Count > 0) continue;

                lastT = t;
                Point3d pt = path.PointAt(t);

                Vector3d tanIn = path.TangentAt(t - 1e-4);
                Vector3d tanOut = path.TangentAt(t + 1e-4);
                Vector3d tangent = (tanIn + tanOut);
                if (tangent.IsTiny(0.001)) tangent = tanIn;
                tangent.Unitize();

                Vector3d up = plane.ZAxis;
                Vector3d radial = Vector3d.CrossProduct(tangent, up);
                if (radial.Length < 0.001) radial = Vector3d.XAxis;
                radial.Unitize();

                Point3d frameOrigin = pt;
                if (useManualShift) frameOrigin += radial * offset;

                Vector3d frameY = Vector3d.CrossProduct(radial, tangent);
                Plane frame = new Plane(frameOrigin, radial, frameY);

                Curve pCrv = sourceProfile.DuplicateCurve();
                pCrv.Transform(Transform.PlaneToPlane(Plane.WorldXY, frame));
                pCrv.Transform(Transform.Scale(frame, width / origW, height / origH, 1.0));

                if (Math.Abs(rotationDeg) > 0.001)
                    pCrv.Rotate(RhinoMath.ToRadians(rotationDeg), frame.Normal, frame.Origin);

                loftCurves.Add(pCrv);
            }

            if (loftCurves.Count < 2) return null;
            var lofts = Brep.CreateFromLoft(loftCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, true);

            if (lofts != null && lofts.Length > 0)
            {
                Brep railBrep = lofts[0];
                if (!railBrep.IsValid) railBrep.Repair(0.001);
                if (!railBrep.IsSolid) railBrep = railBrep.CapPlanarHoles(0.001);
                return railBrep;
            }
            return null;
        }

        // --- PRONGS ---
        private static Brep CreateProng(Curve rail, double t, Plane gemPlane, HeadParameters p)
        {
            Point3d ptOnCrv = rail.PointAt(t);
            Vector3d vecToCenter = gemPlane.Origin - ptOnCrv;
            vecToCenter -= gemPlane.ZAxis * (vecToCenter * gemPlane.ZAxis);
            vecToCenter.Unitize();
            Vector3d normal = -vecToCenter;

            double GetShift(double diameter)
            {
                double radius = diameter / 2.0;
                double overlap = diameter * (p.GemInside / 100.0);
                return radius - overlap;
            }

            double zBot = -p.DepthBelowGem;
            double zTop = zBot + p.Height;

            Point3d ptMid = ptOnCrv + (normal * (GetShift(p.MidDiameter) + p.MidOffset));
            Point3d ptBot = ptOnCrv + (normal * (GetShift(p.BottomDiameter) + p.BottomOffset)) + (gemPlane.ZAxis * zBot);
            Point3d ptTop = ptOnCrv + (normal * (GetShift(p.TopDiameter) + p.TopOffset)) + (gemPlane.ZAxis * zTop);

            Curve axisCurve;
            try { axisCurve = Curve.CreateInterpolatedCurve(new[] { ptTop, ptMid, ptBot }, 3); }
            catch { axisCurve = new LineCurve(ptBot, ptTop); }

            if (axisCurve == null) return null;

            var profiles = new List<Curve>();
            profiles.Add(CreateProfileCurve(axisCurve, axisCurve.Domain.Min, p.TopDiameter, gemPlane, p, p.TopProfileRotation));

            axisCurve.ClosestPoint(ptMid, out double tMid);
            if (tMid > axisCurve.Domain.Min + 0.1 && tMid < axisCurve.Domain.Max - 0.1)
                profiles.Add(CreateProfileCurve(axisCurve, tMid, p.MidDiameter, gemPlane, p, p.MidProfileRotation));

            profiles.Add(CreateProfileCurve(axisCurve, axisCurve.Domain.Max, p.BottomDiameter, gemPlane, p, p.BottomProfileRotation));

            var lofts = Brep.CreateFromLoft(profiles, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (lofts != null && lofts.Length > 0) return lofts[0].CapPlanarHoles(0.001);
            return null;
        }

        private static Curve CreateProfileCurve(Curve axis, double t, double diameter, Plane gemPlane, HeadParameters p, double rotationDeg)
        {
            Point3d origin = axis.PointAt(t);
            Vector3d tangent = axis.TangentAt(t);

            Vector3d vecToCenter = gemPlane.Origin - origin;
            Vector3d dirFlat = vecToCenter - gemPlane.ZAxis * (vecToCenter * gemPlane.ZAxis);
            if (dirFlat.IsTiny(0.001)) dirFlat = Vector3d.XAxis;
            dirFlat.Unitize();

            Vector3d xAxis = dirFlat - (tangent * (dirFlat * tangent));
            if (xAxis.Length < 0.001) xAxis = Vector3d.CrossProduct(tangent, gemPlane.ZAxis);
            xAxis.Unitize();
            Vector3d yAxis = Vector3d.CrossProduct(tangent, xAxis);
            yAxis.Unitize();

            Plane targetPlane = new Plane(origin, xAxis, yAxis);

            // CHANGE: Nutze HeadProfileLibrary und String
            Curve src = HeadProfileLibrary.GetCurve(p.ProfileName);
            if (src == null) src = new Circle(Plane.WorldXY, 1.0).ToNurbsCurve();

            BoundingBox bb = src.GetBoundingBox(true);
            double dim = Math.Max(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
            if (dim < 0.001) dim = 1.0;

            Curve pCrv = src.DuplicateCurve();
            pCrv.Transform(Transform.PlaneToPlane(Plane.WorldXY, targetPlane));
            pCrv.Transform(Transform.Scale(targetPlane, diameter / dim, diameter / dim, 1.0));

            if (Math.Abs(rotationDeg) > 0.001)
                pCrv.Rotate(RhinoMath.ToRadians(rotationDeg), targetPlane.Normal, targetPlane.Origin);

            return pCrv;
        }

        private static Curve OffsetCurveRobust(Curve crv, Plane plane, double dist)
        {
            if (Math.Abs(dist) < 0.001) return crv.DuplicateCurve();
            var offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Sharp);
            if (CheckValidOffset(offsets)) return offsets[0];
            offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Round);
            if (CheckValidOffset(offsets)) return offsets[0];
            offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Smooth);
            if (CheckValidOffset(offsets)) return offsets[0];
            return null;
        }

        private static bool CheckValidOffset(Curve[] offsets)
        {
            if (offsets == null || offsets.Length == 0) return false;
            return offsets[0].IsValid && offsets[0].IsClosed;
        }
    }
}