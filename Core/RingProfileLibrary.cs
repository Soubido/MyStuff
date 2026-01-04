using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public static class RingProfileLibrary
    {
        // Gibt eine GESCHLOSSENE Kurve zurück, zentriert auf 0,0
        public static Curve GetShape(int id)
        {
            // ID 0: Rechteck (Standard)
            // Interval -0.5 bis 0.5 sorgt dafür, dass 0,0 genau die Mitte ist
            if (id == 0)
            {
                var rect = new Rectangle3d(Plane.WorldXY, new Interval(-0.5, 0.5), new Interval(-0.5, 0.5));
                return rect.ToNurbsCurve();
            }
            
            // ID 1: Ellipse / Oval
            if (id == 1)
            {
                var ellipse = new Ellipse(Plane.WorldXY, 0.5, 0.5); // Radius 0.5 = Durchmesser 1.0
                return ellipse.ToNurbsCurve();
            }
            
            // ID 2: D-Shape (Bombiert) - Etwas komplexer
            if (id == 2)
            {
                // Ein Bogen oben, eine Linie unten
                var arc = new Arc(new Point3d(-0.5, 0, 0), new Point3d(0, 0.5, 0), new Point3d(0.5, 0, 0));
                var line = new Line(new Point3d(-0.5, 0, 0), new Point3d(0.5, 0, 0));
                
                var curves = new Curve[] { arc.ToNurbsCurve(), line.ToNurbsCurve() };
                var joined = Curve.JoinCurves(curves);
                
                // Wichtig: Mitte korrigieren, damit sie mittig auf 0,0 liegt
                if (joined != null && joined.Length > 0)
                {
                    var c = joined[0];
                    // Bounding Box Mitte holen und zum Ursprung verschieben
                    var bbox = c.GetBoundingBox(true);
                    c.Translate(-bbox.Center); 
                    return c;
                }
            }

            // Fallback
            return GetShape(0);
        }
    }
}