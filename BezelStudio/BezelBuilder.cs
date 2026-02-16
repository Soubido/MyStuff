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

            // --- SEAM-ANPASSUNG ---
            // Alle geschlossenen Kurven auf dieselbe Seam-Position und Richtung bringen,
            // damit Lofts sauber joinen und keine verdrehten Flächen entstehen.
            Curve[] allCurves = new[] { cOuterTop, cOuterBottom, cInnerTop, cInnerBottom, cGap, cSeatInner, cOuterMid };
            AlignCurveSeams(allCurves, baseCurve, tol);

            // --- 2. FLÄCHEN (LOFTS) ---
            List<Brep> parts = new List<Brep>();

            // 1. Außenwand
            // LoftType.Straight für konsistente Kanten mit den angrenzenden Rim-Flächen.
            // Bei Bombing: Zusätzliche Zwischenprofile erzeugen statt LoftType.Normal,
            // damit die Endkanten exakt auf den Profilen liegen.
            List<Curve> outerProfiles = new List<Curve> { cOuterTop };
            if (cOuterMid != null)
            {
                // Mit Bombing: Zwischenprofile für glatten Verlauf bei LoftType.Straight
                Curve midTop = TweenCurve(cOuterTop, cOuterMid, 0.5);
                if (midTop != null) outerProfiles.Add(midTop);
                outerProfiles.Add(cOuterMid);
                Curve midBottom = TweenCurve(cOuterMid, cOuterBottom, 0.5);
                if (midBottom != null) outerProfiles.Add(midBottom);
            }
            outerProfiles.Add(cOuterBottom);
            var outerWall = Brep.CreateFromLoft(outerProfiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
            AddIfValid(parts, outerWall);

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

            if (!finalBezel.IsSolid)
            {
                finalBezel = finalBezel.CapPlanarHoles(tol);
                // Zweiter Versuch mit höherer Toleranz, falls planare Caps nicht greifen
                if (finalBezel != null && !finalBezel.IsSolid)
                    finalBezel = finalBezel.CapPlanarHoles(tol * 10);
            }

            if (finalBezel == null || !finalBezel.IsValid) return null;

            // --- 4. CUTTER (Boolean Difference für Pavillon) ---
            // "Cutter sollte immer aktiv sein" -> Wir schneiden Platz für den Steinbauch
            try
            {
                // Cutter Oben: Startet an der SeatInner Kante
                Curve cutterTop = cSeatInner.DuplicateCurve();
                cutterTop.Translate(plane.ZAxis * 0.01); // Minimal hoch, damit boolean sauber schneidet

                // Cutter Unten: Spitze tief unten
                Curve cutterBottom = cSeatInner.DuplicateCurve();
                cutterBottom.Translate(plane.ZAxis * (zBottom - 2.0)); // unterhalb der Zarge
                cutterBottom.Scale(0.01); // Spitze

                var cutterLoft = Brep.CreateFromLoft(new[] { cutterTop, cutterBottom }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                if (cutterLoft != null && cutterLoft.Length > 0)
                {
                    Brep cutterBrep = cutterLoft[0].CapPlanarHoles(tol);
                    if (cutterBrep != null && cutterBrep.IsValid && cutterBrep.IsSolid)
                    {
                        Brep[] diff = Brep.CreateBooleanDifference(new[] { finalBezel }, new[] { cutterBrep }, tol);
                        if (diff != null && diff.Length > 0 && diff[0].IsValid)
                        {
                            finalBezel = diff[0];
                        }
                    }
                }
            }
            catch
            {
                // Boolean Difference fehlgeschlagen — Bezel ohne Cutter zurückgeben
            }

            // --- 5. Z-OFFSET ---
            if (Math.Abs(p.ZOffset) > 0.001)
            {
                finalBezel.Translate(plane.ZAxis * p.ZOffset);
            }

            // --- 6. FINALE VALIDIERUNG ---
            if (!finalBezel.IsValid) finalBezel.Repair(tol);

            return finalBezel.IsValid ? finalBezel : null;
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

        /// <summary>
        /// Robuster Offset: Bei Mehrfach-Fragmenten werden alle Segmente gejoint.
        /// Fallback-Kette: Sharp → Round → Smooth.
        /// </summary>
        private static Curve OffsetCurveRobust(Curve crv, Plane plane, double dist)
        {
            if (Math.Abs(dist) < 0.001) return crv.DuplicateCurve();

            CurveOffsetCornerStyle[] styles = {
                CurveOffsetCornerStyle.Sharp,
                CurveOffsetCornerStyle.Round,
                CurveOffsetCornerStyle.Smooth
            };

            foreach (var style in styles)
            {
                var offsets = crv.Offset(plane, dist, 0.001, style);
                if (offsets == null || offsets.Length == 0) continue;

                // Einzelnes Segment — direkt zurückgeben wenn geschlossen und valide
                if (offsets.Length == 1 && offsets[0].IsClosed && offsets[0].IsValid)
                    return offsets[0];

                // Mehrere Segmente — joinen zu einer geschlossenen Kurve
                if (offsets.Length > 1)
                {
                    Curve joined = JoinOffsetFragments(offsets);
                    if (joined != null && joined.IsClosed && joined.IsValid)
                        return joined;
                }

                // Einzelnes, aber offenes Segment — schließen versuchen
                if (offsets.Length == 1 && !offsets[0].IsClosed && offsets[0].IsValid)
                {
                    var nc = offsets[0].ToNurbsCurve();
                    if (nc != null)
                    {
                        // Gap zwischen Start und Ende prüfen
                        double gap = nc.PointAtStart.DistanceTo(nc.PointAtEnd);
                        if (gap < 0.1) // Kleiner Gap → Schließen erlaubt
                        {
                            nc.SetEndPoint(nc.PointAtStart);
                            if (nc.IsClosed && nc.IsValid) return nc;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Joint mehrere Offset-Fragmente zu einer geschlossenen Kurve.
        /// </summary>
        private static Curve JoinOffsetFragments(Curve[] fragments)
        {
            if (fragments == null || fragments.Length == 0) return null;

            var joined = Curve.JoinCurves(fragments, 0.01);
            if (joined == null || joined.Length == 0) return null;

            // Längste geschlossene Kurve suchen
            Curve best = null;
            double bestLen = 0;
            foreach (var c in joined)
            {
                if (c.IsClosed && c.IsValid && c.GetLength() > bestLen)
                {
                    best = c;
                    bestLen = c.GetLength();
                }
            }
            if (best != null) return best;

            // Keine geschlossene Kurve — längste offene Kurve schließen
            best = joined.OrderByDescending(c => c.GetLength()).First();
            if (!best.IsClosed)
            {
                var nc = best.ToNurbsCurve();
                if (nc != null)
                {
                    double gap = nc.PointAtStart.DistanceTo(nc.PointAtEnd);
                    if (gap < 0.1)
                    {
                        nc.SetEndPoint(nc.PointAtStart);
                        if (nc.IsClosed && nc.IsValid) return nc;
                    }
                }
            }

            return best.IsValid ? best : null;
        }

        /// <summary>
        /// Gleicht Seam-Positionen und Kurvenrichtungen aller geschlossenen Kurven an.
        /// Referenz ist die Basiskurve des Steins.
        /// </summary>
        private static void AlignCurveSeams(Curve[] curves, Curve reference, double tol)
        {
            if (reference == null || !reference.IsClosed) return;

            // Referenz-Seam-Punkt
            Point3d seamPt = reference.PointAtStart;

            foreach (var crv in curves)
            {
                if (crv == null || !crv.IsClosed) continue;

                // Richtung angleichen (CounterClockwise prüfen über Fläche)
                var area1 = AreaMassProperties.Compute(reference);
                var area2 = AreaMassProperties.Compute(crv);
                if (area1 != null && area2 != null)
                {
                    // Wenn Vorzeichen der Flächen unterschiedlich → Richtung umkehren
                    bool refCCW = area1.Area > 0;
                    bool crvCCW = area2.Area > 0;
                    if (refCCW != crvCCW) crv.Reverse();
                }

                // Seam zum nächsten Punkt zur Referenz-Seam-Position verschieben
                crv.ClosestPoint(seamPt, out double t);
                crv.ChangeClosedCurveSeam(t);
            }
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
