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

            // Kurve holen
            Curve girdleOriginal = gem.BaseCurve.DuplicateCurve();
            if (!girdleOriginal.IsClosed) girdleOriginal.MakeClosed(0.001);

            // -----------------------------------------------------------------------
            // FIX: ZENTRUM KORRIGIEREN
            // Wir berechnen das geometrische Zentrum der Kurve.
            // Falls die Kurve nicht exakt auf dem Plane-Origin liegt (bei Custom Shapes),
            // verschieben wir unsere Arbeits-Ebene dorthin.
            // -----------------------------------------------------------------------
            BoundingBox bbox = girdleOriginal.GetBoundingBox(true);
            Point3d curveCenter = bbox.Center;

            // Wir erstellen eine "Working Plane", die die Orientierung des Steins hat,
            // aber exakt in der Mitte der Kurve liegt.
            // Damit fluchten Oberteil (Kurve) und Unterteil (Schaft) perfekt.
            Plane workingPlane = new Plane(curveCenter, plane.XAxis, plane.YAxis);

            // Projektion sicherstellen: Z-Höhe der BoundingBox könnte abweichen,
            // wir wollen aber auf der Ebene des Steins bleiben (Z-Achse von plane).
            // (Optional, falls die Kurve nicht planar ist)
            // workingPlane.Origin = plane.ClosestPoint(curveCenter); 


            // 2. Clearance (Offset) anwenden
            Curve profileCrv = girdleOriginal;
            if (p.Clearance > 0.001)
            {
                // Offset auf der workingPlane (Normale ist identisch zu plane)
                var offsets = girdleOriginal.Offset(workingPlane, p.Clearance, 0.001, CurveOffsetCornerStyle.Sharp);
                if (offsets != null && offsets.Length > 0)
                    profileCrv = offsets[0];
            }

            // Global Scale
            if (Math.Abs(p.GlobalScale - 100.0) > 0.1)
            {
                double s = p.GlobalScale / 100.0;
                // Skalieren um das NEUE Zentrum (workingPlane.Origin)
                profileCrv.Transform(Transform.Scale(workingPlane.Origin, s));
            }

            // 3. Höhen berechnen
            double refSize = gem.GemSize;
            double hTop = refSize * (p.TopHeight / 100.0);
            double hSeat = refSize * (p.SeatLevel / 100.0);
            double hBot = refSize * (p.BottomHeight / 100.0);

            Vector3d up = workingPlane.ZAxis;
            Vector3d down = -workingPlane.ZAxis;

            // --- PROFILE ERSTELLEN ---

            // A. Girdle Profil (Mitte)
            Curve cGirdle = profileCrv.DuplicateCurve();

            // B. Top Profil (Oben)
            Curve cTop = profileCrv.DuplicateCurve();
            if (Math.Abs(p.TopDiameterScale - 100.0) > 0.1)
                cTop.Transform(Transform.Scale(workingPlane.Origin, p.TopDiameterScale / 100.0));
            cTop.Translate(up * hTop);

            // C. Seat End Profil (Übergang zum Bohrer)
            // WICHTIG: Hier übergeben wir jetzt workingPlane statt plane!
            Curve cSeatEnd = GetBottomProfile(workingPlane, p, refSize);
            cSeatEnd.Translate(down * hSeat);

            // D. Bottom Profil (Spitze unten)
            Curve cBot = cSeatEnd.DuplicateCurve();
            cBot.Translate(down * (hBot - hSeat));

            // --- LOFT VORBEREITUNG ---
            var profiles = new List<Curve> { cTop, cGirdle, cSeatEnd, cBot };

            // Kurven ausrichten
            AlignCurves(profiles);

            // Loft erzeugen
            var lofts = Brep.CreateFromLoft(profiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            if (lofts != null && lofts.Length > 0)
            {
                // Versuchen zu schließen
                var cutterBrep = lofts[0].CapPlanarHoles(0.001);
                if (cutterBrep != null) result.Add(cutterBrep);
                else result.Add(lofts[0]);
            }

            return result;
        }

        private static void AlignCurves(List<Curve> curves)
        {
            if (curves == null || curves.Count < 2) return;

            Curve master = curves[0];
            Point3d masterStart = master.PointAtStart;

            for (int i = 1; i < curves.Count; i++)
            {
                Curve current = curves[i];

                // Richtung angleichen
                if (!Curve.DoDirectionsMatch(master, current))
                {
                    current.Reverse();
                }

                // Seam angleichen (Startpunkt auf den nächsten Punkt zum Master-Start setzen)
                if (current.ClosestPoint(masterStart, out double t))
                {
                    current.ChangeClosedCurveSeam(t);
                }
            }
        }

        private static Curve GetBottomProfile(Plane plane, CutterParameters p, double refSize)
        {
            Curve result;

            if (p.UseCustomProfile && !string.IsNullOrEmpty(p.ProfileName))
            {
                // Hier könnte man auch HeadProfileLibrary nutzen, falls vorhanden
                // Da wir in diesem Snippet die Library nicht haben, nehmen wir an es ist ein Kreis
                // oder du fügst deine Library-Logik hier wieder ein.

                // Für dieses Beispiel: Fallback auf Kreis, damit es kompiliert.
                // Wenn du HeadProfileLibrary hast, nutze den Code aus deinem Original.
                try
                {
                    // Versuche Library Zugriff (Reflektion oder direkter Aufruf)
                    // ... dein Originalcode ...
                    result = new Circle(plane, refSize / 2.0).ToNurbsCurve();
                }
                catch
                {
                    result = new Circle(plane, refSize / 2.0).ToNurbsCurve();
                }
            }
            else
            {
                // Standard Kreis
                result = new Circle(plane, refSize / 2.0).ToNurbsCurve();
            }

            double scale = p.BottomDiameterScale / 100.0;
            // Skalieren um den Ursprung der übergebenen Plane (jetzt workingPlane.Origin)
            result.Transform(Transform.Scale(plane.Origin, scale));

            if (Math.Abs(p.ProfileRotation) > 0.001)
            {
                result.Rotate(RhinoMath.ToRadians(p.ProfileRotation), plane.ZAxis, plane.Origin);
            }

            return result;
        }
    }
}