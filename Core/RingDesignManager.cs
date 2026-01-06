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

            double defaultW = 3.0;
            double defaultH = 1.5;

            InitSec(RingPosition.Bottom, 0.000, defaultW, defaultH);
            InitSec(RingPosition.BottomRight, 0.125, defaultW, defaultH);
            InitSec(RingPosition.Right, 0.250, defaultW, defaultH);
            InitSec(RingPosition.TopRight, 0.375, defaultW, defaultH);
            InitSec(RingPosition.Top, 0.500, defaultW, defaultH);
            InitSec(RingPosition.TopLeft, 0.625, defaultW, defaultH);
            InitSec(RingPosition.Left, 0.750, defaultW, defaultH);
            InitSec(RingPosition.BottomLeft, 0.875, defaultW, defaultH);

            // Standard: Bottom und Top aktiv
            _sections[RingPosition.Bottom].IsActive = true;
            _sections[RingPosition.Top].IsActive = true;
        }

        private void InitSec(RingPosition pos, double param, double w, double h)
        {
            _sections[pos] = new RingSection
            {
                Parameter = param,
                Name = pos.ToString(),
                ProfileName = "D-Shape", // Explizit setzen!
                Width = w,
                Height = h,
                IsModified = false,
                IsActive = false
            };
        }

        public RingSection GetSection(RingPosition pos) => _sections[pos];

        public void UpdateSection(RingPosition pos, double w, double h, object profileSource)
        {
            if (!_sections.ContainsKey(pos)) return;

            _sections[pos].IsActive = true;
            UpdateSingleSection(pos, w, h, profileSource, true);

            if (MirrorX)
            {
                var mPos = GetMirrorPosition(pos);
                if (mPos != pos)
                {
                    _sections[mPos].IsActive = true;
                    UpdateSingleSection(mPos, w, h, profileSource, true);
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
            var keys = new[] { RingPosition.Bottom, RingPosition.Right, RingPosition.Top, RingPosition.Left };

            foreach (var key in keys)
            {
                var sec = _sections[key];
                Curve profileCurve;
                double w, h;

                // Sicherheitshalber Fallback für ProfileName
                string pName = sec.ProfileName ?? "D-Shape";

                if (sec.IsActive)
                {
                    w = sec.Width;
                    h = sec.Height;
                    if (sec.CustomProfileCurve != null)
                        profileCurve = RingProfileLibrary.CloseAndAnchor(sec.CustomProfileCurve);
                    else
                        profileCurve = RingProfileLibrary.GetClosedProfile(pName);
                }
                else
                {
                    // Interpolation (Bottom + Top) / 2
                    var sBot = _sections[RingPosition.Bottom];
                    var sTop = _sections[RingPosition.Top];
                    w = (sBot.Width + sTop.Width) / 2.0;
                    h = (sBot.Height + sTop.Height) / 2.0;

                    // Profil vom Bottom übernehmen
                    if (sBot.CustomProfileCurve != null)
                        profileCurve = RingProfileLibrary.CloseAndAnchor(sBot.CustomProfileCurve);
                    else
                        profileCurve = RingProfileLibrary.GetClosedProfile(sBot.ProfileName ?? "D-Shape");
                }

                double angle = sec.Parameter * 2.0 * Math.PI;
                slots.Add(new RingProfileSlot(angle, profileCurve, w, h));
            }

            // Loop schließen
            var first = slots[0];
            slots.Add(new RingProfileSlot(first.AngleRad + 2.0 * Math.PI, first.BaseCurve, first.Width, first.Height));

            return slots.ToArray();
        }
    }
}