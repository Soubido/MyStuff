using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public enum RingPosition
    {
        BottomStart = 0,
        Right = 1,
        Top = 2,
        Left = 3,
        BottomEnd = 4
    }

    public class RingDesignManager
    {
        private Dictionary<RingPosition, RingSection> _sections;
        public bool MirrorX { get; set; } = true; 

        public RingDesignManager()
        {
            _sections = new Dictionary<RingPosition, RingSection>();
            
            // Standard-Kurve (ein einfaches Rechteck als Platzhalter)
            var defCurve = new Rectangle3d(Plane.WorldXY, new Interval(-0.5, 0.5), new Interval(0, 1)).ToNurbsCurve();

            InitSection(RingPosition.BottomStart, 0.0, "Bottom", defCurve);
            InitSection(RingPosition.Right, 0.25, "Side", defCurve);
            InitSection(RingPosition.Top, 0.5, "Top", defCurve);
            InitSection(RingPosition.Left, 0.75, "Side", defCurve);
            InitSection(RingPosition.BottomEnd, 1.0, "Bottom", defCurve);
        }

        private void InitSection(RingPosition pos, double t, string name, Curve c)
        {
            _sections[pos] = new RingSection { Parameter = t, Name = name, ProfileCurve = c.DuplicateCurve() };
        }

        public RingSection GetSection(RingPosition pos) => _sections[pos];

        public void UpdateSection(RingPosition pos, double w, double h)
        {
            var sec = _sections[pos];
            sec.Width = w;
            sec.Height = h;

            if (MirrorX) ApplySymmetry(pos, w, h);
            
            // Naht schließen
            if (pos == RingPosition.BottomStart) Sync(RingPosition.BottomEnd, w, h);
            if (pos == RingPosition.BottomEnd) Sync(RingPosition.BottomStart, w, h);
        }

        private void ApplySymmetry(RingPosition pos, double w, double h)
        {
            if (pos == RingPosition.Right) Sync(RingPosition.Left, w, h);
            if (pos == RingPosition.Left) Sync(RingPosition.Right, w, h);
        }

        private void Sync(RingPosition targetPos, double w, double h)
        {
            _sections[targetPos].Width = w;
            _sections[targetPos].Height = h;
            // Profilkurve müsste hier auch kopiert werden, wenn wir verschiedene Formen erlauben
        }

        public List<RingSection> GetSectionsForBuilder() => _sections.Values.ToList();

        public RingProfileSlot[] GetProfileSlots()
        {
            var slotList = new System.Collections.Generic.List<RingProfileSlot>();

            foreach (var entry in _sections)
            {
                RingPosition pos = entry.Key;
                RingSection sec = entry.Value;

                // 1. Winkel berechnen (basierend auf der Position)
                double angle = GetAngleFromPosition(pos);

                // 2. Profilkurve aus der neuen Library holen
                // Falls kein Name gesetzt ist, nehmen wir einen Standard (z.B. "Comfort Fit")
                string profileName = string.IsNullOrEmpty(sec.ProfileName) ? "Comfort Fit" : sec.ProfileName;

                Rhino.Geometry.Curve profileCurve = RingProfileLibrary.GetProfileCurve(profileName, sec.Width, sec.Height);

                // 3. Slot erstellen und zur Liste hinzufügen
                // HINWEIS: Dies setzt voraus, dass RingProfileSlot einen Konstruktor (double angle, Curve curve) hat.
                slotList.Add(new RingProfileSlot(angle, profileCurve));
            }

            return slotList.ToArray();
        }

        // Kleine Hilfsmethode, um die Position in Bogenmaß (Radians) umzurechnen
        private double GetAngleFromPosition(RingPosition pos)
        {
            switch (pos)
            {
                case RingPosition.Right: return 0.0;
                case RingPosition.Top: return Math.PI * 0.5; // 90 Grad
                case RingPosition.Left: return Math.PI;      // 180 Grad
                case RingPosition.BottomStart: return Math.PI * 1.5; // 270 Grad
                case RingPosition.BottomEnd: return Math.PI * 1.5;
                default: return 0.0;
            }
        }
    }
}