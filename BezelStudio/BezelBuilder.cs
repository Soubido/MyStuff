using System;
using System.Collections.Generic;
using Rhino.Geometry;
using NewRhinoGold.Core;

namespace NewRhinoGold.BezelStudio
{
    public static class BezelBuilder
    {
        public static Brep CreateBezel(Curve gemCurve, Plane plane, BezelParameters p)
        {
            if (gemCurve == null || !gemCurve.IsValid) return null;

            // 1. Basis-Kurven vorbereiten (Rondiste auf Plane)
            Curve baseCurve = gemCurve.DuplicateCurve();
            if (!baseCurve.IsPlanar()) baseCurve = Curve.ProjectToPlane(baseCurve, plane);

            // --- A. Außenkörper (Main Body) ---

            // Profil Oben (Basis + Gap)
            Curve profileTopBase = OffsetCurve(baseCurve, plane, p.Offset);
            if (profileTopBase == null) return null;

            // Profil Unten (Basis + Gap - Chamfer/Tapering)
            // Chamfer zieht die Basis unten zusammen -> Konische Form
            Curve profileBottomBase = OffsetCurve(baseCurve, plane, p.Offset - p.Chamfer);
            if (profileBottomBase == null) profileBottomBase = profileTopBase.DuplicateCurve(); // Fallback wenn Taper zu stark

            // Außenwand-Kurven (ProfileBase + Thickness)
            Curve outerTop = OffsetCurve(profileTopBase, plane, p.ThicknessTop);
            Curve outerBottom = OffsetCurve(profileBottomBase, plane, p.ThicknessBottom);

            // Positionierung (temporär Top bei 0, wir schieben am Ende alles hoch)
            outerBottom.Translate(plane.ZAxis * -p.Height);

            Brep outerWallBrep = null;

            if (p.Bombing > 0.001)
            {
                // Bombierung: Mittelprofil auf halber Höhe
                // Wir interpolieren die Basis zwischen Top und Bottom
                // Wir interpolieren die Dicke und addieren Bombing

                // Vereinfacht: Wir nehmen den Durchschnitt der Outer-Kurven und offsetten um Bombing
                // TweenCurves wäre ideal, aber Offset funktioniert für simple Formen auch

                // Strategie: Tween Curve zwischen OuterTop und OuterBottom (projiziert auf Plane)
                Curve midBase = TweenCurve(outerTop, outerBottom, 0.5);
                if (midBase != null)
                {
                    Curve outerMid = OffsetCurve(midBase, plane, p.Bombing);
                    if (outerMid == null) outerMid = midBase;

                    outerMid.Translate(plane.ZAxis * (-p.Height / 2.0));

                    var lofts = Brep.CreateFromLoft(new[] { outerTop, outerMid, outerBottom }, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    if (lofts != null && lofts.Length > 0) outerWallBrep = lofts[0];
                }
            }

            // Fallback Gerade (wenn Bombing 0 oder fehlgeschlagen)
            if (outerWallBrep == null)
            {
                var lofts = Brep.CreateFromLoft(new[] { outerTop, outerBottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (lofts != null && lofts.Length > 0) outerWallBrep = lofts[0];
            }

            if (outerWallBrep == null) return null;
            outerWallBrep = outerWallBrep.CapPlanarHoles(0.001);

            // Wenn kein Cutter gewünscht ist: Resultat vorbereiten
            Brep result = outerWallBrep;

            if (p.CreateCutter)
            {
                // --- B. Cutter (Innenleben) ---

                // Cutter Basis (inkl. GemGap Toleranz)
                Curve cutterTopBase = OffsetCurve(baseCurve, plane, p.Offset + p.GemGap);
                if (cutterTopBase == null) cutterTopBase = profileTopBase.DuplicateCurve();

                // Cutter 1: Oben bis SeatDepth (Stein-Sitz)
                // Wir extrudieren von Z=0 bis Z=-SeatDepth
                var cutter1Srf = Surface.CreateExtrusion(cutterTopBase, plane.ZAxis * -(p.SeatDepth + 0.01));
                var cutter1 = cutter1Srf.ToBrep().CapPlanarHoles(0.001);
                cutter1.Translate(plane.ZAxis * 0.005); // Clean boolean overlap top

                // Cutter 2: SeatDepth bis Unten (Durchbruch)
                // Basis für Cutter 2 muss zur unteren Form passen (Tapering berücksichtigen!)
                // Wir interpolieren die Cutter-Basis an der Seat-Position

                // Einfachheitshalber: Wir nehmen CutterTopBase und ziehen SeatLedge ab. 
                // Wenn starkes Tapering da ist, könnte das Loch unten aus der Wand ragen.
                // Besser: Wir berechnen die Cutter-Kurve unten basierend auf profileBottomBase

                Curve cutterBottomBase = OffsetCurve(profileBottomBase, plane, p.GemGap - p.SeatLedge);
                if (cutterBottomBase == null) cutterBottomBase = OffsetCurve(cutterTopBase, plane, -p.SeatLedge);

                // Loft Cutter 2 (von Seat bis Bottom)
                Curve c2Top = OffsetCurve(cutterTopBase, plane, -p.SeatLedge);
                if (c2Top == null) c2Top = cutterBottomBase; // Fallback

                c2Top.Translate(plane.ZAxis * -p.SeatDepth);

                Curve c2Bottom = cutterBottomBase.DuplicateCurve();
                c2Bottom.Translate(plane.ZAxis * -(p.Height + 0.5)); // Durchstossen

                var cutter2Lofts = Brep.CreateFromLoft(new[] { c2Top, c2Bottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

                if (cutter2Lofts != null && cutter2Lofts.Length > 0)
                {
                    var cutter2 = cutter2Lofts[0].CapPlanarHoles(0.001);

                    var boolResult = Brep.CreateBooleanDifference(new[] { result }, new[] { cutter1, cutter2 }, 0.001);
                    if (boolResult != null && boolResult.Length > 0) result = boolResult[0];
                }
                else
                {
                    // Fallback nur Cutter 1
                    var boolResult = Brep.CreateBooleanDifference(new[] { result }, new[] { cutter1 }, 0.001);
                    if (boolResult != null && boolResult.Length > 0) result = boolResult[0];
                }
            }

            // --- Final Alignment: Seat auf Z=0 ---
            // Aktuell: Top=0, Seat=-SeatDepth.
            // Ziel: Top=+SeatDepth, Seat=0.
            // Wir schieben alles um +SeatDepth nach oben.

            double alignShift = p.SeatDepth;

            // Plus manueller ZOffset
            alignShift += p.ZOffset;

            if (Math.Abs(alignShift) > 0.001)
            {
                result.Translate(plane.ZAxis * alignShift);
            }

            return result;
        }

        private static Curve OffsetCurve(Curve crv, Plane plane, double dist)
        {
            if (Math.Abs(dist) < 0.001) return crv.DuplicateCurve();
            var offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Sharp);
            return (offsets != null && offsets.Length > 0) ? offsets[0] : null;
        }

        private static Curve TweenCurve(Curve c1, Curve c2, double factor)
        {
            // Simple tweening by rebuilding and tweening control points 
            // (Robust genug für offset-basierte Rondisten)
            if (c1 == null || c2 == null) return null;

            Curve nc1 = c1.ToNurbsCurve();
            Curve nc2 = c2.ToNurbsCurve();

            // Ensure same domain and direction roughly
            nc1.Domain = new Interval(0, 1);
            nc2.Domain = new Interval(0, 1);

            // Very simplified geometric mean (BoundingBox Center scale) or Loft center
            // Für echte Geometrie ist Loft und IsoCurve extraction am besten
            var loft = Brep.CreateFromLoft(new[] { nc1, nc2 }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            if (loft != null && loft.Length > 0)
            {
                // Extrahiere Isocurve in der Mitte (V Richtung 0.5)
                var face = loft[0].Faces[0];
                // Reparameterize face to ensure 0.5 is middle
                face.SetDomain(1, new Interval(0, 1));
                return face.IsoCurve(1, factor).ToNurbsCurve();
            }
            return c1.DuplicateCurve();
        }
    }
}