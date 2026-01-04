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
            // Der Cutter muss etwas größer sein als der Stein
            Curve profileCrv = girdleOriginal;
            if (p.Clearance > 0.001)
            {
                var offsets = girdleOriginal.Offset(plane, p.Clearance, 0.001, CurveOffsetCornerStyle.Sharp);
                if (offsets != null && offsets.Length > 0)
                    profileCrv = offsets[0];
            }

            // Skalierung (Global Scale %)
            if (Math.Abs(p.GlobalScale - 100.0) > 0.1)
            {
                double s = p.GlobalScale / 100.0;
                profileCrv.Transform(Transform.Scale(plane.Origin, s));
            }

            // 3. Höhen berechnen (Absolutwerte basierend auf Stein-Größe)
            // Wir nehmen die GemSize als Referenz für %-Werte
            double refSize = gem.GemSize; 
            
            double hTop = refSize * (p.TopHeight / 100.0);
            double hSeat = refSize * (p.SeatLevel / 100.0); // Wie weit geht der Seat runter
            double hBot = refSize * (p.BottomHeight / 100.0);

            // Vektoren
            Vector3d up = plane.ZAxis;
            Vector3d down = -plane.ZAxis;

            // --- TEIL A: TOP & SEAT (Loft/Extrude) ---
            
            // Profil auf Girdle-Höhe (Start)
            // Wir schieben es ggf. ein kleines Stück hoch, damit es nicht *in* der Girdle anfängt,
            // sondern den Stein sicher umschließt.
            Curve cGirdle = profileCrv.DuplicateCurve();
            
            // Profil oben (Ende Top Schaft)
            Curve cTop = profileCrv.DuplicateCurve();
            // Optional: TopDiameterScale anwenden
            if (Math.Abs(p.TopDiameterScale - 100.0) > 0.1)
                cTop.Transform(Transform.Scale(plane.Origin, p.TopDiameterScale / 100.0));
            cTop.Translate(up * hTop);

            // Profil unten am Seat (Übergang zum Bohrer)
            // Hier entscheidet sich die Form: Ist es noch die Steinform oder schon die Library-Form?
            // Normalerweise ist der Seat der Übergang.
            Curve cSeatEnd = GetBottomProfile(plane, p, refSize);
            
            // Seat Ende positionieren (nach unten)
            cSeatEnd.Translate(down * hSeat);

            // Profil ganz unten (Ende Bottom Schaft)
            Curve cBot = cSeatEnd.DuplicateCurve();
            // Nach unten schieben (Gesamtlänge unten = Seat + Rest)
            // Wenn BottomHeight die Gesamtlänge ab Girdle ist:
            cBot.Translate(down * (hBot - hSeat)); 

            // --- LOFT ERSTELLEN ---
            // Wir loften durch alle Profile: Top -> Girdle -> SeatEnd -> Bottom
            
            var profiles = new List<Curve> { cTop, cGirdle, cSeatEnd, cBot };
            
            // Loft erstellen (Straight sections für technische Anmutung)
            var lofts = Brep.CreateFromLoft(profiles, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);

            if (lofts != null && lofts.Length > 0)
            {
                var cutterBrep = lofts[0].CapPlanarHoles(0.001);
                if (cutterBrep != null) result.Add(cutterBrep);
                else result.Add(lofts[0]); // Falls Caps fehlschlagen, wenigstens Flächen
            }

            return result;
        }

        /// <summary>
        /// Holt das Profil für den unteren Teil (Bohrer).
        /// Entweder runde Standardform, Steinform oder Custom Library Shape.
        /// </summary>
        private static Curve GetBottomProfile(Plane plane, CutterParameters p, double refSize)
        {
            Curve result;

            if (p.UseCustomProfile && p.ProfileId != Guid.Empty)
            {
                // 1. Aus Library laden
                var item = ProfileLibrary.Get(p.ProfileId);
                if (item != null && item.BaseCurve != null)
                {
                    // Profil auf Plane ausrichten
                    Curve libCrv = item.BaseCurve.DuplicateCurve();
                    
                    // Zentrieren & auf XY Plane (falls Library Curve woanders liegt)
                    BoundingBox bb = libCrv.GetBoundingBox(true);
                    libCrv.Translate(Point3d.Origin - bb.Center);
                    
                    // Auf Cutter Plane transformieren
                    libCrv.Transform(Transform.PlaneToPlane(Plane.WorldXY, plane));
                    result = libCrv;
                }
                else
                {
                    // Fallback Kreis
                    result = new Circle(plane, refSize / 2.0).ToNurbsCurve();
                }
            }
            else
            {
                // Standard: Wir nehmen einfach einen Kreis (Rundbohrer)
                // ODER: Wir nehmen die Steinform (für Fassungen, die genau die Form behalten sollen)
                // Hier nehmen wir vereinfacht einen Kreis als Standard für "Cutter", da Bohrer meist rund sind.
                // Wenn man die Steinform will, müsste man das in den Parametern wählen können.
                // Wir machen hier Default = Kreis (angepasst an Steinform-Größe)
                
                // Radius = (SteinGröße * Scale) / 2
                result = new Circle(plane, refSize / 2.0).ToNurbsCurve();
            }

            // Skalieren (Bottom Diameter Scale)
            double scale = p.BottomDiameterScale / 100.0;
            result.Transform(Transform.Scale(plane.Origin, scale));

            // Rotation
            if (Math.Abs(p.ProfileRotation) > 0.001)
            {
                result.Rotate(RhinoMath.ToRadians(p.ProfileRotation), plane.ZAxis, plane.Origin);
            }

            return result;
        }
    }
}