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

            // Initialisierung aller 8 Sektoren
            // Reihenfolge ist für die Dictionary-Keys egal, wichtig ist die Init-Methode
            InitSec(RingPosition.Bottom, 0.000, defaultW, defaultH);
            InitSec(RingPosition.BottomRight, 0.125, defaultW, defaultH);
            InitSec(RingPosition.Right, 0.250, defaultW, defaultH);
            InitSec(RingPosition.TopRight, 0.375, defaultW, defaultH);
            InitSec(RingPosition.Top, 0.500, defaultW, defaultH);
            InitSec(RingPosition.TopLeft, 0.625, defaultW, defaultH);
            InitSec(RingPosition.Left, 0.750, defaultW, defaultH);
            InitSec(RingPosition.BottomLeft, 0.875, defaultW, defaultH);

            // --- STANDARD: NUR BOTTOM AKTIV ---
            foreach (var k in _sections.Keys) _sections[k].IsActive = false;

            // Bottom ist der "Master", mit dem man beginnt
            _sections[RingPosition.Bottom].IsActive = true;
        }

        private void InitSec(RingPosition pos, double param, double w, double h)
        {
            _sections[pos] = new RingSection
            {
                Name = pos.ToString(),
                Parameter = param,
                ProfileName = "D-Shape",
                Width = w,
                Height = h,
                Rotation = 0,
                OffsetY = 0,
                IsActive = false,
                FlipX = false
            };
        }

        public RingSection GetSection(RingPosition pos) => _sections[pos];

        // --- UPDATES ---

        // Neue Signatur mit Rotation und Offset
        public void UpdateSection(RingPosition pos, double w, double h, double rot, double offY, object profileSource)
        {
            if (!_sections.ContainsKey(pos)) return;

            // Automatische Aktivierung bei Änderung
            _sections[pos].IsActive = true;
            UpdateSingle(pos, w, h, rot, offY, profileSource);

            if (MirrorX)
            {
                var mPos = GetMirrorPosition(pos);
                if (mPos != pos)
                {
                    _sections[mPos].IsActive = true;
                    // Beim Spiegeln: Rotation oft invertieren? 
                    // Konvention: Rotation +10° rechts könnte -10° links sein.
                    // Fürs Erste spiegeln wir 1:1, es sei denn Sie wünschen Invertierung.
                    // Bei OffsetY bleibt es gleich.
                    UpdateSingle(mPos, w, h, rot, offY, profileSource);
                }
            }
        }

        private void UpdateSingle(RingPosition pos, double w, double h, double rot, double offY, object source)
        {
            var sec = _sections[pos];
            sec.Width = w;
            sec.Height = h;
            sec.Rotation = rot;
            sec.OffsetY = offY;
            sec.IsModified = true;

            if (source is string name)
            {
                sec.ProfileName = name;
                sec.CustomProfileCurve = null;
            }
            else if (source is Curve c)
            {
                sec.ProfileName = "Custom";
                sec.CustomProfileCurve = c.DuplicateCurve();
            }
        }

        public void ToggleActive(RingPosition pos)
        {
            if (!_sections.ContainsKey(pos)) return;
            bool newState = !_sections[pos].IsActive;
            _sections[pos].IsActive = newState;
            if (MirrorX)
            {
                var mPos = GetMirrorPosition(pos);
                if (mPos != pos) _sections[mPos].IsActive = newState;
            }
        }

        public void ToggleFlipProfile(RingPosition pos)
        {
            if (!_sections.ContainsKey(pos)) return;
            bool newState = !_sections[pos].FlipX;
            _sections[pos].FlipX = newState;
            if (MirrorX)
            {
                var mPos = GetMirrorPosition(pos);
                if (mPos != pos) _sections[mPos].FlipX = newState;
            }
        }

        private RingPosition GetMirrorPosition(RingPosition pos)
        {
            switch (pos)
            {
                case RingPosition.Right: return RingPosition.Left;
                case RingPosition.Left: return RingPosition.Right;
                case RingPosition.TopRight: return RingPosition.TopLeft;
                case RingPosition.TopLeft: return RingPosition.TopRight;
                case RingPosition.BottomRight: return RingPosition.BottomLeft;
                case RingPosition.BottomLeft: return RingPosition.BottomRight;
                default: return pos;
            }
        }

        // --- BUILDER DATA ---

        public (RingProfileSlot[] Slots, bool IsClosedLoop) GetBuildData()
        {
            var activeSlots = new List<RingProfileSlot>();
            var sortedKeys = _sections.Keys.OrderBy(k => _sections[k].Parameter).ToList();

            foreach (var key in sortedKeys)
            {
                var sec = _sections[key];
                if (!sec.IsActive) continue;

                Curve rawProfile;
                if (sec.CustomProfileCurve != null)
                    rawProfile = sec.CustomProfileCurve;
                else
                    rawProfile = RingProfileLibrary.GetClosedProfile(sec.ProfileName ?? "D-Shape");

                if (rawProfile == null) continue;

                if (sec.FlipX) rawProfile.Reverse();

                double angle = sec.Parameter * 2.0 * Math.PI;

                // Erstelle Slot mit den neuen Parametern
                var slot = new RingProfileSlot(angle, rawProfile, sec.Width, sec.Height);
                slot.Rotation = sec.Rotation;
                slot.OffsetY = sec.OffsetY;

                activeSlots.Add(slot);
            }

            // Fallback auf Bottom, wenn alles aus
            if (activeSlots.Count == 0)
            {
                var bSec = _sections[RingPosition.Bottom];
                var p = RingProfileLibrary.GetClosedProfile(bSec.ProfileName ?? "D-Shape");
                var slot = new RingProfileSlot(0, p, bSec.Width, bSec.Height);
                // Auch hier Fallback-Werte übernehmen
                slot.Rotation = bSec.Rotation;
                slot.OffsetY = bSec.OffsetY;
                activeSlots.Add(slot);
            }

            return (activeSlots.ToArray(), true); // Immer geschlossener Loop
        }
    }
}