using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using NewRhinoGold.Core;

namespace NewRhinoGold.BezelStudio
{
    public static class CutterBuilder
    {
        public static List<Brep> CreateCutter(GemSmartData gem, CutterParameters p)
        {
            var result = new List<Brep>();
            if (gem == null || gem.BaseCurve == null) return result;

            // 1. Basisdaten holen
            Plane plane = gem.GemPlane;
            if (!plane.IsValid) plane = Plane.WorldXY;

            // Kurve holen und sicherstellen, dass sie geschlossen ist
            Curve girdleOriginal = gem.BaseCurve.DuplicateCurve();
            if (!girdleOriginal.IsClosed) girdleOriginal.MakeClosed(0.001);

            // 2. Clearance (Offset) anwenden
            Curve profileCrv = girdleOriginal;
            if (p.Clearance > 0.001)
            {
                var offsets = girdleOriginal.Offset(plane, p.Clearance, 0.001, CurveOffsetCornerStyle.Sharp);
                if (offsets != null && offsets.Length > 0)
                    profileCrv = offsets[0];
            }

            // Global Scale
            if (Math.Abs(p.GlobalScale - 100.0) > 0.1)
            {
                double s = p.GlobalScale / 100.0;
                profileCrv.Transform(Transform.Scale(plane.Origin, s));
            }

            // 3. Höhen berechnen
            double refSize = gem.GemSize;
            double hTop = refSize * (p.TopHeight / 100.0);
            double hSeat = refSize * (p.SeatLevel / 100.0);
            double hBot = refSize * (p.BottomHeight / 100.0);

            Vector3d up = plane.ZAxis;
            Vector3d down = -plane.ZAxis;

            // --- PROFILE ERSTELLEN ---

            // A. Girdle Profil (Mitte)
            Curve cGirdle = profileCrv.DuplicateCurve();

            // B. Top Profil (Oben)
            Curve cTop = profileCrv.DuplicateCurve();
            if (Math.Abs(p.TopDiameterScale - 100.0) > 0.1)
                cTop.Transform(Transform.Scale(plane.Origin, p.TopDiameterScale / 100.0));
            cTop.Translate(up * hTop);

            // C. Seat End Profil (Übergang zum Bohrer)
            Curve cSeatEnd = GetBottomProfile(plane, p, refSize);
            cSeatEnd.Translate(down * hSeat);

            // D. Bottom Profil (Spitze unten)
            Curve cBot = cSeatEnd.DuplicateCurve();
            cBot.Translate(down * (hBot - hSeat));

            // --- LOFT VORBEREITUNG ---
            var profiles = new List<Curve> { cTop, cGirdle, cSeatEnd, cBot };

            // WICHTIG: Kurven ausrichten (Seam Adjustment), um Verdrehungen zu verhindern
            AlignCurves(profiles);

            // Loft erzeugen (Straight für saubere Kanten)
            var lofts = Brep.CreateFromLoft(profiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            if (lofts != null && lofts.Length > 0)
            {
                var cutterBrep = lofts[0].CapPlanarHoles(0.001);
                if (cutterBrep != null) result.Add(cutterBrep);
                else result.Add(lofts[0]);
            }

            return result;
        }

        /// <summary>
        /// Richtet alle Kurven in der Liste so aus, dass Startpunkte und Richtung
        /// zur ersten Kurve passen. Verhindert verdrehte Lofts.
        /// </summary>
        private static void AlignCurves(List<Curve> curves)
        {
            if (curves == null || curves.Count < 2) return;

            // Wir nutzen die erste Kurve (Top) als Referenz
            Curve master = curves[0];
            Point3d masterStart = master.PointAtStart;
            Vector3d masterDir = master.TangentAtStart;

            // Plane für Orientierungs-Check (Clockwise vs Counter-Clockwise)
            // Wir nehmen an, Kurven sind planar oder fast planar
            Plane refPlane;
            master.TryGetPlane(out refPlane);

            for (int i = 1; i < curves.Count; i++)
            {
                Curve current = curves[i];

                // 1. Richtung angleichen (Flip wenn nötig)
                if (Curve.DoDirectionsMatch(master, current))
                {
                    // Alles gut
                }
                else
                {
                    current.Reverse();
                }

                // 2. Startpunkt (Seam) angleichen
                // Finde den Punkt auf 'current', der dem Startpunkt von 'master' am nächsten ist
                if (current.ClosestPoint(masterStart, out double t))
                {
                    // Setze den Seam neu
                    current.ChangeClosedCurveSeam(t);
                }
            }
        }

        private static Curve GetBottomProfile(Plane plane, CutterParameters p, double refSize)
        {
            Curve result;

            if (p.UseCustomProfile && !string.IsNullOrEmpty(p.ProfileName))
            {
                Curve libCrv = HeadProfileLibrary.GetCurve(p.ProfileName);

                if (libCrv != null)
                {
                    libCrv = libCrv.DuplicateCurve();
                    BoundingBox bb = libCrv.GetBoundingBox(true);
                    libCrv.Translate(Point3d.Origin - bb.Center);
                    libCrv.Transform(Transform.PlaneToPlane(Plane.WorldXY, plane));
                    result = libCrv;
                }
                else
                {
                    result = new Circle(plane, refSize / 2.0).ToNurbsCurve();
                }
            }
            else
            {
                result = new Circle(plane, refSize / 2.0).ToNurbsCurve();
            }

            double scale = p.BottomDiameterScale / 100.0;
            result.Transform(Transform.Scale(plane.Origin, scale));

            if (Math.Abs(p.ProfileRotation) > 0.001)
            {
                result.Rotate(RhinoMath.ToRadians(p.ProfileRotation), plane.ZAxis, plane.Origin);
            }

            return result;
        }
    }
}