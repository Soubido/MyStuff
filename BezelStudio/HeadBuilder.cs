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

            // WICHTIG: Sicherstellen, dass die Kurve geschlossen ist für Rails
            if (!baseCrv.IsClosed) baseCrv.MakeClosed(0.001);

            baseCrv.Domain = new Interval(0, 1);

            // --- AUFLAGE (RAILS) ---
            if (p.EnableTopRail)
            {
                var rail = CreateRailMiteredLoft(baseCrv, plane, p.TopRailPosition, p.TopRailWidth, p.TopRailThickness, p.TopRailProfileId, p.TopRailOffset, p.TopRailRotation);
                if (rail != null) result.Add(rail);
            }

            if (p.EnableBottomRail)
            {
                double totalBotOffset = -0.4 + p.BottomRailOffset;
                var rail = CreateRailMiteredLoft(baseCrv, plane, p.BottomRailPosition, p.BottomRailWidth, p.BottomRailThickness, p.BottomRailProfileId, totalBotOffset, p.BottomRailRotation);
                if (rail != null) result.Add(rail);
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
                        if (prong != null) result.Add(prong);
                    }
                    catch { }
                }
            }

            return result;
        }

        /// <summary>
        /// Erzeugt die Rail mittels "Mitered Loft".
        /// Falls Offset fehlschlägt, wird das Profil manuell nach außen geschoben (Robustheit von Scale, Präzision von Offset).
        /// </summary>
        private static Brep CreateRailMiteredLoft(Curve railPath, Plane plane, double zPos, double width, double height, Guid profileId, double offset, double rotationDeg)
        {
            if (railPath == null) return null;
            if (width < 0.01) width = 0.01;
            if (height < 0.01) height = 0.01;

            Curve path = railPath.DuplicateCurve();
            bool useManualShift = false;

            // 1. Versuche echten Offset (beste Qualität)
            if (Math.Abs(offset) > 0.001)
            {
                var offsets = path.Offset(plane, offset, 0.001, CurveOffsetCornerStyle.Sharp);
                if (offsets != null && offsets.Length > 0 && offsets[0].IsValid && offsets[0].IsClosed)
                {
                    path = offsets[0];
                }
                else
                {
                    // Fallback: Wenn Rhino Offset versagt, nutzen wir "Manual Shift"
                    // Wir nehmen die Originalkurve und schieben das Profil später raus.
                    useManualShift = true;
                }
            }

            // 2. Positionieren
            path.Translate(plane.ZAxis * zPos);

            // 3. Profil Laden & Messen
            var libItem = ProfileLibrary.Get(profileId);
            Curve sourceProfile = (libItem != null && libItem.BaseCurve != null)
                ? libItem.BaseCurve.DuplicateCurve()
                : new Circle(Plane.WorldXY, 1.0).ToNurbsCurve();

            BoundingBox bbox = sourceProfile.GetBoundingBox(true);
            double origW = bbox.Max.X - bbox.Min.X; if (origW < 0.001) origW = 1.0;
            double origH = bbox.Max.Y - bbox.Min.Y; if (origH < 0.001) origH = 1.0;

            // 4. Sampling (Punkte auf der Kurve finden)
            var tParams = new List<double>();

            // Ecken finden
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

            // Glatte Bereiche unterteilen (Auflösung)
            int smoothSamples = 60;
            double[] divided = path.DivideByCount(smoothSamples, true);
            if (divided != null) tParams.AddRange(divided);

            tParams.Sort();

            // Profile generieren
            var loftCurves = new List<Curve>();
            double lastT = -999.0;

            foreach (double t in tParams)
            {
                // Duplikate am Start/Ende filtern
                if (Math.Abs(t - lastT) < 1e-5) continue;
                // Sicherstellen, dass wir nicht über das Ende hinausgehen (bei Closed Loop ist Max = Min)
                if (t >= tEnd - 1e-5 && loftCurves.Count > 0) continue;

                lastT = t;

                Point3d pt = path.PointAt(t);

                // Miter-Tangente für scharfe Ecken
                Vector3d tanIn = path.TangentAt(t - 1e-4);
                Vector3d tanOut = path.TangentAt(t + 1e-4);
                Vector3d tangent = (tanIn + tanOut);
                if (tangent.IsTiny(0.001)) tangent = tanIn; // Fallback bei 180° Wende
                tangent.Unitize();

                // Frame berechnen
                Vector3d up = plane.ZAxis;
                Vector3d radial = Vector3d.CrossProduct(tangent, up);
                if (radial.Length < 0.001) radial = Vector3d.XAxis;
                radial.Unitize();

                // MANUELLER SHIFT (Ersatz für Offset/Scale)
                Point3d frameOrigin = pt;
                if (useManualShift)
                {
                    // Wir schieben den Ankerpunkt des Profils einfach nach außen
                    frameOrigin += radial * offset;
                }

                // Plane erstellen (Zwangsausrichtung an ZAxis -> Roadlike)
                Vector3d frameY = Vector3d.CrossProduct(radial, tangent);
                Plane frame = new Plane(frameOrigin, radial, frameY);

                // Profil platzieren
                Curve pCrv = sourceProfile.DuplicateCurve();
                pCrv.Transform(Transform.PlaneToPlane(Plane.WorldXY, frame));

                // Skalieren (Breite/Höhe)
                pCrv.Transform(Transform.Scale(frame, width / origW, height / origH, 1.0));

                // Rotation
                if (Math.Abs(rotationDeg) > 0.001)
                    pCrv.Rotate(RhinoMath.ToRadians(rotationDeg), frame.Normal, frame.Origin);

                loftCurves.Add(pCrv);
            }

            // Sicherstellen, dass wir genug Profile haben
            if (loftCurves.Count < 2) return null;

            // Loft erstellen (Closed = true verbindet Ende mit Start)
            var lofts = Brep.CreateFromLoft(loftCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, true);

            if (lofts != null && lofts.Length > 0)
            {
                // Da es ein Ring ist, ist er "Closed", aber wir müssen sicherstellen, dass er "Solid" ist.
                // Ein Loft-Ring aus geschlossenen Kurven ist ein Solid (Torus).
                return lofts[0];
            }

            return null;
        }

        // --- PRONGS ---
        private static Brep CreateProng(Curve rail, double t, Plane gemPlane, HeadParameters p)
        {
            Point3d ptOnCrv = rail.PointAt(t);
            Vector3d tangent = rail.TangentAt(t);

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
            return lofts?[0]?.CapPlanarHoles(0.001);
        }

        private static Curve CreateProfileCurve(Curve axis, double t, double diameter, Plane gemPlane, HeadParameters p, double rotationDeg)
        {
            Point3d origin = axis.PointAt(t);
            Vector3d tangent = axis.TangentAt(t);

            Vector3d vecToCenter = gemPlane.Origin - origin;
            Vector3d dirFlat = vecToCenter - gemPlane.ZAxis * (vecToCenter * gemPlane.ZAxis);
            dirFlat.Unitize();

            Vector3d xAxis = dirFlat - (tangent * (dirFlat * tangent));
            if (xAxis.Length < 0.001) xAxis = Vector3d.CrossProduct(tangent, gemPlane.ZAxis);
            xAxis.Unitize();
            Vector3d yAxis = Vector3d.CrossProduct(tangent, xAxis);
            yAxis.Unitize();

            Plane targetPlane = new Plane(origin, xAxis, yAxis);

            var libItem = ProfileLibrary.Get(p.ProfileId);
            Curve src = (libItem != null && libItem.BaseCurve != null) ? libItem.BaseCurve : new Circle(Plane.WorldXY, 1.0).ToNurbsCurve();

            BoundingBox bb = src.GetBoundingBox(true);
            double dim = Math.Max(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
            if (dim < 0.001) dim = 1.0;

            Curve pCrv = src.DuplicateCurve();
            pCrv.Transform(Transform.PlaneToPlane(Plane.WorldXY, targetPlane));

            // KORREKTUR: Scale immer mit Plane für korrekte Ausrichtung
            pCrv.Transform(Transform.Scale(targetPlane, diameter / dim, diameter / dim, 1.0));

            if (Math.Abs(rotationDeg) > 0.001)
                pCrv.Rotate(RhinoMath.ToRadians(rotationDeg), targetPlane.Normal, targetPlane.Origin);

            return pCrv;
        }
    }
}