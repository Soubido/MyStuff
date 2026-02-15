using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    // Speichert die Einstellungen für EINE Position
    public class RingSmartSection
    {
        public int PositionIndex { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public double OffsetY { get; set; }
        public string ProfileName { get; set; }
        public bool IsActive { get; set; }
        public bool FlipX { get; set; }

        // WICHTIG: Speichert die Kurvengeometrie direkt (für Custom-Profile)
        public Curve ProfileCurve { get; set; }
    }

    [Guid("E5D4C3B2-A100-4BCD-9999-888877776666")]
    public class RingSmartData : UserData
    {
        private const int MAJOR = 1;
        private const int MINOR = 1;

        public double RingSize { get; set; }
        public string MaterialId { get; set; }
        public bool MirrorX { get; set; }
        public List<RingSmartSection> Sections { get; set; } = new List<RingSmartSection>();

        public RingSmartData() { }

        public RingSmartData(double size, string matId, bool mirrorX, List<RingSmartSection> sections)
        {
            RingSize = size;
            MaterialId = matId;
            MirrorX = mirrorX;
            // WICHTIG: Deep Copy der Liste beim Erstellen, um Referenzkonflikte zu vermeiden
            if (sections != null)
            {
                Sections = new List<RingSmartSection>();
                foreach (var sec in sections)
                {
                    Sections.Add(DuplicateSection(sec));
                }
            }
        }

        public override string Description => "NewRhinoGold Smart Ring";
        public override bool ShouldWrite => true;

        // ------------------------------------------------------------------
        // KRITISCHER FIX: ON DUPLICATE
        // ------------------------------------------------------------------
        protected override void OnDuplicate(UserData source)
        {
            if (source is RingSmartData srcData)
            {
                this.RingSize = srcData.RingSize;
                this.MaterialId = srcData.MaterialId;
                this.MirrorX = srcData.MirrorX;

                this.Sections = new List<RingSmartSection>();
                foreach (var sec in srcData.Sections)
                {
                    this.Sections.Add(DuplicateSection(sec));
                }
            }
        }

        // Hilfsmethode für Deep Copy einer Section
        private RingSmartSection DuplicateSection(RingSmartSection source)
        {
            return new RingSmartSection
            {
                PositionIndex = source.PositionIndex,
                Width = source.Width,
                Height = source.Height,
                Rotation = source.Rotation,
                OffsetY = source.OffsetY,
                ProfileName = source.ProfileName,
                IsActive = source.IsActive,
                FlipX = source.FlipX,
                // WICHTIG: Geometrie muss auch dupliziert werden!
                ProfileCurve = source.ProfileCurve?.DuplicateCurve()
            };
        }

        // ------------------------------------------------------------------
        // SERIALISIERUNG (READ / WRITE)
        // ------------------------------------------------------------------
        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out int major, out int minor);
            RingSize = archive.ReadDouble();
            MaterialId = archive.ReadString();
            MirrorX = archive.ReadBool();

            int count = archive.ReadInt();
            Sections = new List<RingSmartSection>(count);
            for (int i = 0; i < count; i++)
            {
                var s = new RingSmartSection();
                s.PositionIndex = archive.ReadInt();
                s.Width = archive.ReadDouble();
                s.Height = archive.ReadDouble();
                s.Rotation = archive.ReadDouble();
                s.OffsetY = archive.ReadDouble();
                s.ProfileName = archive.ReadString();
                s.IsActive = archive.ReadBool();
                s.FlipX = archive.ReadBool();

                if (minor >= 1)
                {
                    bool hasCurve = archive.ReadBool();
                    if (hasCurve)
                    {
                        // Geometry sicher casten
                        var geom = archive.ReadGeometry();
                        if (geom is Curve c) s.ProfileCurve = c;
                    }
                }
                Sections.Add(s);
            }
            return true;
        }

        protected override bool Write(BinaryArchiveWriter archive)
        {
            archive.Write3dmChunkVersion(MAJOR, MINOR);
            archive.WriteDouble(RingSize);
            archive.WriteString(MaterialId);
            archive.WriteBool(MirrorX);

            archive.WriteInt(Sections.Count);
            foreach (var s in Sections)
            {
                archive.WriteInt(s.PositionIndex);
                archive.WriteDouble(s.Width);
                archive.WriteDouble(s.Height);
                archive.WriteDouble(s.Rotation);
                archive.WriteDouble(s.OffsetY);
                archive.WriteString(s.ProfileName ?? "D-Shape"); // Null-Check
                archive.WriteBool(s.IsActive);
                archive.WriteBool(s.FlipX);

                bool hasCurve = s.ProfileCurve != null;
                archive.WriteBool(hasCurve);
                if (hasCurve)
                {
                    archive.WriteGeometry(s.ProfileCurve);
                }
            }
            return true;
        }
    }
}