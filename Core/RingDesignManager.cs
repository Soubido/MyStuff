using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    public enum RingPosition
    {
        Bottom = 0, BottomRight = 1, Right = 2, TopRight = 3,
        Top = 4, TopLeft = 5, Left = 6, BottomLeft = 7
    }

    public class RingDesignManager
    {
        private Dictionary<RingPosition, RingSection> _sections;
        public bool MirrorX { get; set; } = true;

        public RingDesignManager()
        {
            _sections = new Dictionary<RingPosition, RingSection>();

            // Standardwerte (Start mit Bottom Werten f¸r alles)
            // Wir setzen IsModified erst auf true, wenn DU es ‰nderst.
            // Beim Start ist alles "Standard".

            double defaultW = 3.0;
            double defaultH = 1.5;

            // Parameter (Winkel) festlegen
            InitSec(RingPosition.Bottom, 0.000, defaultW, defaultH);
            InitSec(RingPosition.BottomRight, 0.125, defaultW, defaultH);
            InitSec(RingPosition.Right, 0.250, defaultW, defaultH);
            InitSec(RingPosition.TopRight, 0.375, defaultW, defaultH);
            InitSec(RingPosition.Top, 0.500, defaultW, defaultH); // Top
            InitSec(RingPosition.TopLeft, 0.625, defaultW, defaultH);
            InitSec(RingPosition.Left, 0.750, defaultW, defaultH);
            InitSec(RingPosition.BottomLeft, 0.875, defaultW, defaultH);
        }

        private void InitSec(RingPosition pos, double param, double w, double h)
        {
            _sections[pos] = new RingSection
            {
                Parameter = param,
                Name = pos.ToString(),
                Width = w,
                Height = h,
                IsModified = false // Noch nicht vom User angefasst
            };
        }

        public RingSection GetSection(RingPosition pos) => _sections[pos];

        // Die Haupt-Update Methode
        public void UpdateSection(RingPosition pos, double w, double h, object profileSource)
        {
            if (!_sections.ContainsKey(pos)) return;

            // 1. Das angeklickte Element updaten & markieren
            UpdateSingleSection(pos, w, h, profileSource, true);

            // 2. PROPAGATION (Die Auswirkung auf andere)

            // Fall A: Bottom wurde ge‰ndert -> Wirkt auf ALLE, die noch nicht modifiziert sind.
            if (pos == RingPosition.Bottom)
            {
                foreach (var key in _sections.Keys.ToList())
                {
                    if (key == RingPosition.Bottom) continue;

                    var sec = _sections[key];
                    // Nur ¸berschreiben, wenn der User diesen Teil noch NICHT angefasst hat
                    if (!sec.IsModified)
                    {
                        UpdateSingleSection(key, w, h, profileSource, false); // false = bleibt "unmodifiziert" (abh‰ngig)
                    }
                }
            }

            // Fall B: Top wurde ge‰ndert -> Wirkt auf alle auﬂer Bottom (und modifizierte).
            else if (pos == RingPosition.Top)
            {
                foreach (var key in _sections.Keys.ToList())
                {
                    if (key == RingPosition.Top) continue;
                    if (key == RingPosition.Bottom) continue; // Bottom wird von Top nicht ver‰ndert!

                    var sec = _sections[key];
                    if (!sec.IsModified)
                    {
                        UpdateSingleSection(key, w, h, profileSource, false);
                    }
                }
            }

            // Symmetrie behandeln (nach der Propagation, damit Spiegelung korrekt sitzt)
            if (MirrorX)
            {
                var mPos = GetMirrorPosition(pos);
                if (mPos != pos)
                {
                    // Spiegelung erzwingen
                    UpdateSingleSection(mPos, w, h, profileSource, true); // Spiegelung gilt als modifiziert
                }
            }
        }

        private void UpdateSingleSection(RingPosition pos, double w, double h, object profileSource, bool markModified)
        {
            var sec = _sections[pos];
            sec.Width = w;
            sec.Height = h;

            if (markModified) sec.IsModified = true;

            if (profileSource is string name)
            {
                sec.ProfileName = name;
                sec.CustomProfileCurve = null;
            }
            else if (profileSource is Curve crv)
            {
                sec.ProfileName = "Custom";
                sec.CustomProfileCurve = crv.DuplicateCurve();
            }
        }

        private RingPosition GetMirrorPosition(RingPosition pos)
        {
            // Simple Mirror Logic
            if (pos == RingPosition.Right) return RingPosition.Left;
            if (pos == RingPosition.Left) return RingPosition.Right;
            if (pos == RingPosition.TopRight) return RingPosition.TopLeft;
            if (pos == RingPosition.TopLeft) return RingPosition.TopRight;
            if (pos == RingPosition.BottomRight) return RingPosition.BottomLeft;
            if (pos == RingPosition.BottomLeft) return RingPosition.BottomRight;
            return pos;
        }

        public RingProfileSlot[] GetProfileSlots()
        {
            var slots = new List<RingProfileSlot>();

            foreach (var kvp in _sections)
            {
                var sec = kvp.Value;
                double angle = sec.Parameter * 2.0 * Math.PI;
                Curve baseCurve;

                if (sec.CustomProfileCurve != null)
                    baseCurve = RingProfileLibrary.CloseAndAnchor(sec.CustomProfileCurve);
                else
                    baseCurve = RingProfileLibrary.GetClosedProfile(sec.ProfileName);

                slots.Add(new RingProfileSlot(angle, baseCurve, sec.Width, sec.Height));
            }

            // Loop schlieﬂen (Slot bei 360∞) - RingBuilder filtert den evtl raus, aber wir liefern ihn.
            var btm = slots.First(s => Math.Abs(s.AngleRad) < 0.001);
            slots.Add(new RingProfileSlot(2.0 * Math.PI, btm.BaseCurve, btm.Width, btm.Height));

            return slots.OrderBy(s => s.AngleRad).ToArray();
        }
    }
}