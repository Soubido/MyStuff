using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using NewRhinoGold.Core;

namespace NewRhinoGold.BezelStudio
{
    public static class BezelBuilder
    {
        public static Brep CreateBezel(Curve gemCurve, Plane plane, BezelParameters p)
        {
            if (gemCurve == null || !gemCurve.IsValid) return null;

            double tol = 0.001;

            // --- 1. LOKALISIERUNG (TRANSFORM TO WORLD ZERO) ---
            // Wir definieren eine Mapping-Transformation von der Stein-Plane zur Welt-XY-Plane.
            // Dadurch wird der Stein mathematisch "gerade hingelegt" auf 0,0,0.
            Transform xformToWorld = Transform.PlaneToPlane(plane, Plane.WorldXY);
            Transform xformToLoc = Transform.PlaneToPlane(Plane.WorldXY, plane);

            // Kurve in den Ursprung transformieren
            Curve crvLocal = gemCurve.DuplicateCurve();
            crvLocal.Transform(xformToWorld);

            // Plane ist jetzt WorldXY
            Plane planeLocal = Plane.WorldXY;

            // Sicherstellen, dass die Kurve flach ist (Project to WorldXY)
            if (!crvLocal.IsPlanar()) crvLocal = Curve.ProjectToPlane(crvLocal, planeLocal);
            if (!crvLocal.IsClosed) crvLocal.MakeClosed(tol);

            // --- 2. RICHTUNGS-NORMALISIERUNG ---
            // Im Welt-Ursprung MUSS die Kurve Counter-Clockwise sein, 
            // damit Offset positiv nach außen geht.
            CurveOrientation orientation = crvLocal.ClosedCurveOrientation(planeLocal);
            if (orientation == CurveOrientation.Clockwise)
            {
                crvLocal.Reverse();
            }

            // Seam optimieren (weg von Ecken für saubere Lofts)
            SimplifySeam(ref crvLocal);

            // --- 3. PROFIL GENERIERUNG (LOKAL) ---

            // A. Innenkurve (Gap)
            Curve cGap = OffsetCurveRobust(crvLocal, planeLocal, p.Offset);
            if (cGap == null) return null;

            // B. Außenkurve Oben
            Curve cOuterTopBase = OffsetCurveRobust(cGap, planeLocal, p.ThicknessTop);
            if (cOuterTopBase == null) cOuterTopBase = cGap.DuplicateCurve();

            // C. Außenkurve Unten (Chamfer/Tapering)
            Curve cOuterBottomBase = OffsetCurveRobust(cOuterTopBase, planeLocal, -p.Chamfer);
            if (cOuterBottomBase == null) cOuterBottomBase = cOuterTopBase.DuplicateCurve();

            // D. Seat Inner (Auflage)
            Curve cSeatInnerBase = OffsetCurveRobust(cGap, planeLocal, -p.SeatLedge);
            if (cSeatInnerBase == null)
            {
                cSeatInnerBase = OffsetCurveRobust(cGap, planeLocal, -0.1);
                if (cSeatInnerBase == null) cSeatInnerBase = cGap.DuplicateCurve();
            }

            // E. Innenkurve Unten
            Curve cInnerBottomBase = OffsetCurveRobust(cOuterBottomBase, planeLocal, -p.ThicknessBottom);
            if (cInnerBottomBase == null)
            {
                cInnerBottomBase = cOuterBottomBase.DuplicateCurve();
                // Skalierung ist jetzt trivial, da wir im Ursprung sind
                cInnerBottomBase.Transform(Transform.Scale(Point3d.Origin, 0.9));
            }

            // F. Bombing (Wölbung)
            Curve cOuterMidBase = null;
            if (Math.Abs(p.Bombing) > 0.001)
            {
                Curve midTween = TweenCurve(cOuterTopBase, cOuterBottomBase, 0.5);
                if (midTween != null)
                {
                    cOuterMidBase = OffsetCurveRobust(midTween, planeLocal, p.Bombing);
                }
            }

            // --- 4. Z-AUFBAU (LOKAL) ---

            double zTop = p.SeatDepth;
            double zBottom = p.SeatDepth - p.Height;
            double zMid = (zTop + zBottom) / 2.0;
            Vector3d zAxis = planeLocal.ZAxis; // Ist jetzt einfach (0,0,1)

            // Kurven positionieren
            Curve cOuterTop = cOuterTopBase.DuplicateCurve();
            cOuterTop.Translate(zAxis * zTop);

            Curve cOuterBottom = cOuterBottomBase.DuplicateCurve();
            cOuterBottom.Translate(zAxis * zBottom);

            Curve cInnerTop = cGap.DuplicateCurve();
            cInnerTop.Translate(zAxis * zTop);

            Curve cGapCrv = cGap.DuplicateCurve(); // Z=0

            Curve cSeatInner = cSeatInnerBase.DuplicateCurve(); // Z=0

            Curve cInnerBottom = cInnerBottomBase.DuplicateCurve();
            cInnerBottom.Translate(zAxis * zBottom);

            Curve cOuterMid = null;
            if (cOuterMidBase != null)
            {
                cOuterMid = cOuterMidBase.DuplicateCurve();
                cOuterMid.Translate(zAxis * zMid);
            }

            // --- 5. LOFTS ERSTELLEN ---
            List<Brep> parts = new List<Brep>();

            // 1. Outer Wall
            var outerProfiles = new List<Curve> { cOuterTop };
            if (cOuterMid != null) outerProfiles.Add(cOuterMid);
            outerProfiles.Add(cOuterBottom);
            AlignCurves(outerProfiles);
            AddIfValid(parts, Brep.CreateFromLoft(outerProfiles, Point3d.Unset, Point3d.Unset, LoftType.Normal, false));

            // 2. Top Rim
            var rimProfiles = new List<Curve> { cOuterTop, cInnerTop };
            AlignCurves(rimProfiles);
            AddIfValid(parts, Brep.CreateFromLoft(rimProfiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false));

            // 3. Inner Wall (Top)
            var innerWallProfiles = new List<Curve> { cInnerTop, cGapCrv };
            AlignCurves(innerWallProfiles);
            AddIfValid(parts, Brep.CreateFromLoft(innerWallProfiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false));

            // 4. Seat Shelf
            var seatProfiles = new List<Curve> { cGapCrv, cSeatInner };
            AlignCurves(seatProfiles);
            AddIfValid(parts, Brep.CreateFromLoft(seatProfiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false));

            // 5. Under Bezel Inner Wall
            var underBezelProfiles = new List<Curve> { cSeatInner, cInnerBottom };
            AlignCurves(underBezelProfiles);
            AddIfValid(parts, Brep.CreateFromLoft(underBezelProfiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false));

            // 6. Bottom Rim
            var bottomRimProfiles = new List<Curve> { cOuterBottom, cInnerBottom };
            AlignCurves(bottomRimProfiles);
            AddIfValid(parts, Brep.CreateFromLoft(bottomRimProfiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false));

            // --- 6. JOIN & CAP (LOKAL) ---
            Brep finalBezel = null;
            Brep[] joined = Brep.JoinBreps(parts, tol);

            if (joined != null && joined.Length > 0)
            {
                finalBezel = joined.OrderByDescending(b => b.GetArea()).FirstOrDefault();
                if (finalBezel != null && !finalBezel.IsSolid)
                {
                    finalBezel = finalBezel.CapPlanarHoles(tol);
                }
            }
            if (finalBezel == null) return null;

            // --- 7. CUTTER (LOKAL) ---

            Curve cutterTop = cSeatInner.DuplicateCurve();
            cutterTop.Translate(zAxis * 0.01);

            Curve cutterBottom = cSeatInner.DuplicateCurve();
            cutterBottom.Translate(zAxis * (zBottom - 2.0));
            cutterBottom.Transform(Transform.Scale(Point3d.Origin, 0.01)); // Origin ist hier sicher (0,0,0)

            var cutterProfiles = new List<Curve> { cutterTop, cutterBottom };
            AlignCurves(cutterProfiles);

            var cutterLoft = Brep.CreateFromLoft(cutterProfiles, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (cutterLoft != null && cutterLoft.Length > 0)
            {
                Brep cutterBrep = cutterLoft[0].CapPlanarHoles(tol);
                if (cutterBrep != null)
                {
                    if (cutterBrep.SolidOrientation == BrepSolidOrientation.Inward) cutterBrep.Flip();
                    Brep[] diff = Brep.CreateBooleanDifference(new[] { finalBezel }, new[] { cutterBrep }, tol);
                    if (diff != null && diff.Length > 0) finalBezel = diff[0];
                }
            }

            // --- 8. Z-OFFSET (LOKAL) ---
            if (Math.Abs(p.ZOffset) > 0.001)
            {
                finalBezel.Translate(zAxis * p.ZOffset);
            }

            // --- 9. RÜCKTRANSFORMATION (GLOBAL) ---
            // Das fertige Objekt wird nun an die echte Position im Raum geschickt.
            finalBezel.Transform(xformToLoc);

            return finalBezel;
        }

        // --- HELPER ---

        private static void SimplifySeam(ref Curve crv)
        {
            if (crv == null) return;
            if (crv.TangentAtStart.EpsilonEquals(crv.TangentAtEnd, 0.1)) return;
            double tMid = (crv.Domain.Min + crv.Domain.Max) / 2.0;
            crv.ChangeClosedCurveSeam(tMid);
        }

        private static void AlignCurves(List<Curve> curves)
        {
            if (curves == null || curves.Count < 2) return;
            for (int i = 1; i < curves.Count; i++)
            {
                if (!Curve.DoDirectionsMatch(curves[0], curves[i])) curves[i].Reverse();
            }
            double t0 = curves[0].Domain.Min;
            Point3d startPt = curves[0].PointAtStart;
            for (int i = 1; i < curves.Count; i++)
            {
                if (curves[i].ClosestPoint(startPt, out double t)) curves[i].ChangeClosedCurveSeam(t);
            }
        }

        private static bool AddIfValid(List<Brep> list, Brep[] breps)
        {
            if (breps != null && breps.Length > 0 && breps[0] != null)
            {
                list.Add(breps[0]);
                return true;
            }
            return false;
        }

        private static Curve OffsetCurveRobust(Curve crv, Plane plane, double dist)
        {
            if (Math.Abs(dist) < 0.001) return crv.DuplicateCurve();
            var offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Sharp);
            if (IsValidOffset(offsets)) return offsets[0];
            offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Round);
            if (IsValidOffset(offsets)) return offsets[0];
            offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Smooth);
            if (IsValidOffset(offsets)) return offsets[0];
            return null;
        }

        private static bool IsValidOffset(Curve[] offsets)
        {
            return offsets != null && offsets.Length > 0 && offsets[0] != null && offsets[0].IsValid && offsets[0].IsClosed;
        }

        private static Curve TweenCurve(Curve c1, Curve c2, double factor)
        {
            if (c1 == null || c2 == null) return null;
            var list = new List<Curve> { c1, c2 };
            AlignCurves(list);
            var loft = Brep.CreateFromLoft(list, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (loft != null && loft.Length > 0)
            {
                var face = loft[0].Faces[0];
                face.SetDomain(1, new Interval(0, 1));
                return face.IsoCurve(1, factor).ToNurbsCurve();
            }
            return c1.DuplicateCurve();
        }
    }
}