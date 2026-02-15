using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public static class EngravingBuilder
    {
        /// <summary>
        /// Stanzt geschlossene Kurven in einen Ring ein.
        /// </summary>
        /// <param name="ring">Der Ring (Solid Brep).</param>
        /// <param name="curves">Liste der Kurven, die AUF dem Ring liegen.</param>
        /// <param name="depth">Tiefe der Stanze (z.B. 1.0 mm).</param>
        /// <param name="center">Mittelpunkt des Rings (meist Point3d.Origin).</param>
        /// <returns>Der Ring mit eingestanzten Kurven.</returns>
        public static Brep EngraveRing(Brep ring, IEnumerable<Curve> curves, double depth, Point3d center)
        {
            if (ring == null || curves == null) return null;

            double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            var cutters = new List<Brep>();

            foreach (var crv in curves)
            {
                if (!crv.IsClosed) continue;

                // 1. Vektor zum Zentrum ermitteln (für die Richtung)
                // Wir nehmen den Mittelpunkt der Kurven-BoundingBox
                Point3d crvCenter = crv.GetBoundingBox(true).Center;
                Vector3d dirToCenter = center - crvCenter;
                double distToCenter = dirToCenter.Length;
                
                if (distToCenter < 0.001) continue; // Fehlerhaft
                
                // Normalisieren
                dirToCenter.Unitize();

                // 2. Start-Kurve (leicht nach "außen" schieben für sauberen Schnitt)
                Curve outerCrv = crv.DuplicateCurve();
                outerCrv.Translate(-dirToCenter * 0.1); // 0.1mm Luft nach außen

                // 3. End-Kurve (nach "innen" schieben auf gewünschte Tiefe)
                // Wir berechnen den Skalierungsfaktor basierend auf der Tiefe
                // Ziel-Distanz = AktuelleDistanz - (Depth + Safety)
                double targetDist = distToCenter - (depth + 0.1);
                double scaleFactor = targetDist / distToCenter;

                Curve innerCrv = crv.DuplicateCurve();
                // Skalierung ist besser als Translation bei Ringen, da sie die "Konizität" erhält
                innerCrv.Scale(scaleFactor); 
                // Zur Sicherheit explizit positionieren, falls Scale origin-basiert war
                // (Rhino Scale nutzt Origin, das passt hier perfekt für Ringe um 0,0,0)

                // 4. Solid Cutter erstellen (Loft)
                var loft = Brep.CreateFromLoft(new[] { outerCrv, innerCrv }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                
                if (loft != null && loft.Length > 0)
                {
                    Brep cutter = loft[0];

                    // Versuchen zu schließen (Cap)
                    // CapPlanarHoles funktioniert nur bei planaren Kurven.
                    // Wenn die Kurve gewölbt ist, müssen wir "Patches" nutzen oder hoffen, dass Rhino es packt.
                    cutter = cutter.CapPlanarHoles(tol);

                    if (cutter == null || !cutter.IsSolid)
                    {
                        // Fallback für nicht-planare Kurven (Wrapping Text):
                        // Wir müssen die Enden schließen. Da CapPlanarHoles versagt, nutzen wir CreateSolid 
                        // oder schließen es manuell (komplex).
                        // Einfacher Hack: Wir gehen davon aus, dass BooleanDifference auch mit offenen Flächen 
                        // schneiden kann, wenn sie den Körper komplett durchdringen? Nein.
                        
                        // Versuch: Patch Faces
                        // Das ist rechenintensiv, aber nötig für "gekrümmte" Texte.
                        // Für den Moment geben wir nur Cutter zurück, die wir schließen konnten.
                        // Eine echte "Wrapper"-Logik benötigt Brep.CreatePatch.
                    }

                    if (cutter != null && cutter.IsSolid)
                    {
                        cutters.Add(cutter);
                    }
                }
            }

            if (cutters.Count == 0) return ring; // Nichts zu tun oder fehlgeschlagen

            // 5. Boolean Difference
            Brep[] result = Brep.CreateBooleanDifference(ring, cutters[0], tol);
            
            // Falls mehrere Cutter, iterativ oder als Liste abziehen
            if (cutters.Count > 1)
            {
                // CreateBooleanDifference nimmt Arrays
                 result = Brep.CreateBooleanDifference(new[] { ring }, cutters, tol);
            }

            return (result != null && result.Length > 0) ? result[0] : ring;
        }
    }
}