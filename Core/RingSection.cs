using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public class RingSection
    {
        public double Parameter { get; set; }
        public string Name { get; set; }

        // WICHTIG: Standardwert setzen, damit er nie null ist!
        public string ProfileName { get; set; } = "D-Shape";

        public Curve CustomProfileCurve { get; set; } = null;
        public double Width { get; set; }
        public double Height { get; set; }

        public bool IsModified { get; set; } = false;
        public bool IsActive { get; set; } = false;

        public RingSection Clone()
        {
            var copy = (RingSection)this.MemberwiseClone();
            if (this.CustomProfileCurve != null)
                copy.CustomProfileCurve = this.CustomProfileCurve.DuplicateCurve();
            return copy;
        }
    }
}