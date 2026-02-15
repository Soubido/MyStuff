using Rhino.Geometry;

namespace NewRhinoGold.BezelStudio
{
    /// <summary>
    /// Speichert Position und Radius bereits gesetzter Steine für die Kollisionsprüfung.
    /// </summary>
    public struct ExistingGemData
    {
        public Point3d Point;
        public double Radius;
    }
}