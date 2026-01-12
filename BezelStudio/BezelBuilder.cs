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

            // Vorbereitung: Kurve auf Plane projizieren
            Curve baseCurve = gemCurve.DuplicateCurve();
            if (!baseCurve.IsPlanar()) baseCurve = Curve.ProjectToPlane(baseCurve, plane);

            double tol = 0.001;

            // --- 1. PROFIL GENERIERUNG ---

            // A. Innenkurve (Kontakt zum Stein + Offset)
            Curve cGap = OffsetCurveRobust(baseCurve, plane, p.Offset);
            if (cGap == null) return null; // Offset fehlgeschlagen

            // B. Außenkurve Oben
            Curve cOuterTopBase = OffsetCurveRobust(cGap, plane, p.ThicknessTop);
            if (cOuterTopBase == null) cOuterTopBase = cGap.DuplicateCurve(); // Fallback

            // C. Außenkurve Unten (Tapering)
            // Chamfer verringert den Radius unten.
            Curve cOuterBottomBase = OffsetCurveRobust(cOuterTopBase, plane, -p.Chamfer);
            if (cOuterBottomBase == null) cOuterBottomBase = cOuterTopBase.DuplicateCurve(); // Fallback

            // Positionierung in Z
            // Top liegt bei +SeatDepth
            // Bottom liegt bei +SeatDepth - Height
            double zTop = p.SeatDepth;
            double zBottom = p.SeatDepth - p.Height;

            Curve cOuterTop = cOuterTopBase.DuplicateCurve();
            cOuterTop.Translate(plane.ZAxis * zTop);

            Curve cOuterBottom = cOuterBottomBase.DuplicateCurve();
            cOuterBottom.Translate(plane.ZAxis * zBottom);

            // D. Innenkurve Oben (Identisch mit Gap, aber auf Höhe zTop)
            Curve cInnerTop = cGap.DuplicateCurve();
            cInnerTop.Translate(plane.ZAxis * zTop);

            // E. Innenkurve Unten (Dicke unten abziehen)
            Curve cInnerBottom = OffsetCurveRobust(cOuterBottom, plane, -p.ThicknessBottom);
            if (cInnerBottom == null)
            {
                // Fallback: Skalieren, falls Offset fehlschlägt
                cInnerBottom = cOuterBottom.DuplicateCurve();
                cInnerBottom.Scale(0.9);
            }

            // F. Seat Inner (Auflagekante)
            // Definiert durch Gap - SeatLedge
            Curve cSeatInner = OffsetCurveRobust(cGap, plane, -p.SeatLedge);
            if (cSeatInner == null)
            {
                // Wenn Ledge zu breit für den Stein ist, machen wir ein minimales Loch
                cSeatInner = OffsetCurveRobust(cGap, plane, -0.1);
                if (cSeatInner == null) cSeatInner = cGap.DuplicateCurve();
            }

            // G. Bombing (Mittelprofil)
            Curve cOuterMid = null;
            if (Math.Abs(p.Bombing) > 0.001)
            {
                double zMid = (zTop + zBottom) / 2.0;
                Curve midBase = TweenCurve(cOuterTopBase, cOuterBottomBase, 0.5);
                if (midBase != null)
                {
                    cOuterMid = OffsetCurveRobust(midBase, plane, p.Bombing);
                    if (cOuterMid != null) cOuterMid.Translate(plane.ZAxis * zMid);
                }
            }

            // --- 2. FLÄCHEN (LOFTS) ---
            List<Brep> parts = new List<Brep>();

            // 1. Außenwand
            List<Curve> outerProfiles = new List<Curve> { cOuterTop };
            if (cOuterMid != null) outerProfiles.Add(cOuterMid);
            outerProfiles.Add(cOuterBottom);
            var outerWall = Brep.CreateFromLoft(outerProfiles, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (AddIfValid(parts, outerWall)) { }

            // 2. Rand Oben (Rim)
            var topRim = Brep.CreateFromLoft(new[] { cOuterTop, cInnerTop }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            AddIfValid(parts, topRim);

            // 3. Fassrand Innen (Bezel Wall)
            var upperInnerWall = Brep.CreateFromLoft(new[] { cInnerTop, cGap }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            AddIfValid(parts, upperInnerWall);

            // 4. Auflage (Seat) - Planar
            var seatShelf = Brep.CreateFromLoft(new[] { cGap, cSeatInner }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            AddIfValid(parts, seatShelf);

            // 5. Innenwand Unten (Under Bezel)
            var lowerInnerWall = Brep.CreateFromLoft(new[] { cSeatInner, cInnerBottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            AddIfValid(parts, lowerInnerWall);

            // 6. Rand Unten
            var bottomRim = Brep.CreateFromLoft(new[] { cOuterBottom, cInnerBottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            AddIfValid(parts, bottomRim);

            // --- 3. JOIN & CAP ---
            Brep[] joined = Brep.JoinBreps(parts, tol);
            if (joined == null || joined.Length == 0) return null;

            Brep finalBezel = joined.OrderByDescending(b => b.GetArea()).FirstOrDefault();
            if (finalBezel == null) return null;

            if (!finalBezel.IsSolid) finalBezel = finalBezel.CapPlanarHoles(tol);


            // --- 4. CUTTER (Boolean Difference für Pavillon) ---
            // "Cutter sollte immer aktiv sein" -> Wir schneiden Platz für den Steinbauch

            // Cutter Oben: Startet an der SeatInner Kante
            Curve cutterTop = cSeatInner.DuplicateCurve();
            cutterTop.Translate(plane.ZAxis * 0.01); // Minimal hoch, damit boolean sauber schneidet

            // Cutter Unten: Spitze tief unten
            Curve cutterBottom = cSeatInner.DuplicateCurve();
            cutterBottom.Translate(plane.ZAxis * (zBottom - 2.0)); // unterhalb der Zarge
            cutterBottom.Scale(0.01); // Spitze

            var cutterLoft = Brep.CreateFromLoft(new[] { cutterTop, cutterBottom }, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
            if (cutterLoft != null && cutterLoft.Length > 0)
            {
                Brep cutterBrep = cutterLoft[0].CapPlanarHoles(tol);
                if (cutterBrep != null)
                {
                    Brep[] diff = Brep.CreateBooleanDifference(new[] { finalBezel }, new[] { cutterBrep }, tol);
                    if (diff != null && diff.Length > 0)
                    {
                        finalBezel = diff[0];
                    }
                }
            }

            // --- 5. Z-OFFSET ---
            if (Math.Abs(p.ZOffset) > 0.001)
            {
                finalBezel.Translate(plane.ZAxis * p.ZOffset);
            }

            return finalBezel;
        }

        // --- HELPER ---

        private static bool AddIfValid(List<Brep> list, Brep[] breps)
        {
            if (breps != null && breps.Length > 0)
            {
                list.Add(breps[0]);
                return true;
            }
            return false;
        }

        // Robuster Offset, der bei "Null"-Ergebnis (wegen Sharp corners) Round versucht
        private static Curve OffsetCurveRobust(Curve crv, Plane plane, double dist)
        {
            if (Math.Abs(dist) < 0.001) return crv.DuplicateCurve();

            // Versuch 1: Sharp (Ideal für Schmuck)
            var offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Sharp);

            // Check ob Valid
            if (offsets != null && offsets.Length > 0) return offsets[0];

            // Versuch 2: Round (Fallback, löst das "0.7 Gap Problem" bei komplexen Formen)
            offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Round);
            if (offsets != null && offsets.Length > 0) return offsets[0];

            // Versuch 3: Smooth (Letzter Ausweg)
            offsets = crv.Offset(plane, dist, 0.001, CurveOffsetCornerStyle.Smooth);
            if (offsets != null && offsets.Length > 0) return offsets[0];

            return null;
        }

        private static Curve TweenCurve(Curve c1, Curve c2, double factor)
        {
            if (c1 == null || c2 == null) return null;
            var loft = Brep.CreateFromLoft(new[] { c1, c2 }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
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