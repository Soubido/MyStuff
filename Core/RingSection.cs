using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public class RingSection
    {
        public double Parameter { get; set; }
        public string Name { get; set; }
        public string ProfileName { get; set; } = "D-Shape";

        // NEU: Falls der User eine Kurve auswählt
        public Curve CustomProfileCurve { get; set; } = null;

        public double Width { get; set; }
        public double Height { get; set; }

        public bool IsModified { get; set; } = false;

        public RingSection Clone()
        {
            var copy = (RingSection)this.MemberwiseClone();
            if (this.CustomProfileCurve != null)
                copy.CustomProfileCurve = this.CustomProfileCurve.DuplicateCurve();
            return copy;
        }
    }
}