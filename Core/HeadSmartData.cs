using System;
using System.Collections.Generic;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;
using System.Runtime.InteropServices;

namespace NewRhinoGold.Core
{
    [Guid("A1B2C3D4-E5F6-7890-1234-56789ABCDE01")]
    public class HeadSmartData : UserData
    {
        private const int MAJOR = 1;
        private const int MINOR = 1; // Version erhöht wegen String-Wechsel

        public Guid GemId { get; set; }
        public Plane GemPlane { get; set; }

        public double Height { get; set; }
        public double DepthBelowGem { get; set; }
        public double GemInside { get; set; }
        public int ProngCount { get; set; }

        public double TopDiameter { get; set; }
        public double MidDiameter { get; set; }
        public double BottomDiameter { get; set; }

        public double TopOffset { get; set; }
        public double MidOffset { get; set; }
        public double BottomOffset { get; set; }

        public double TopProfileRotation { get; set; }
        public double MidProfileRotation { get; set; }
        public double BottomProfileRotation { get; set; }

        // Rails
        public bool EnableTopRail { get; set; }
        public string TopRailProfileName { get; set; } // String!
        public double TopRailWidth { get; set; }
        public double TopRailThickness { get; set; }
        public double TopRailPosition { get; set; }
        public double TopRailOffset { get; set; }
        public double TopRailRotation { get; set; }

        public bool EnableBottomRail { get; set; }
        public string BottomRailProfileName { get; set; } // String!
        public double BottomRailWidth { get; set; }
        public double BottomRailThickness { get; set; }
        public double BottomRailPosition { get; set; }
        public double BottomRailOffset { get; set; }
        public double BottomRailRotation { get; set; }

        public string ProfileName { get; set; } // String!

        public List<double> ProngPositions { get; set; } = new List<double>();

        public HeadSmartData() { }

        public HeadSmartData(HeadParameters p, Guid gemId, Plane gemPlane)
        {
            GemId = gemId;
            GemPlane = gemPlane;
            Height = p.Height;
            DepthBelowGem = p.DepthBelowGem;
            GemInside = p.GemInside;
            ProngCount = p.ProngCount;
            TopDiameter = p.TopDiameter;
            MidDiameter = p.MidDiameter;
            BottomDiameter = p.BottomDiameter;
            TopOffset = p.TopOffset;
            MidOffset = p.MidOffset;
            BottomOffset = p.BottomOffset;
            TopProfileRotation = p.TopProfileRotation;
            MidProfileRotation = p.MidProfileRotation;
            BottomProfileRotation = p.BottomProfileRotation;

            EnableTopRail = p.EnableTopRail;
            TopRailProfileName = p.TopRailProfileName;
            TopRailWidth = p.TopRailWidth;
            TopRailThickness = p.TopRailThickness;
            TopRailPosition = p.TopRailPosition;
            TopRailOffset = p.TopRailOffset;
            TopRailRotation = p.TopRailRotation;

            EnableBottomRail = p.EnableBottomRail;
            BottomRailProfileName = p.BottomRailProfileName;
            BottomRailWidth = p.BottomRailWidth;
            BottomRailThickness = p.BottomRailThickness;
            BottomRailPosition = p.BottomRailPosition;
            BottomRailOffset = p.BottomRailOffset;
            BottomRailRotation = p.BottomRailRotation;

            ProfileName = p.ProfileName;
            ProngPositions = p.ProngPositions ?? new List<double>();
        }

        public override string Description => "Smart Head Data";

        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out int major, out int minor);

            GemId = archive.ReadGuid();
            GemPlane = archive.ReadPlane();
            Height = archive.ReadDouble();
            DepthBelowGem = archive.ReadDouble();
            GemInside = archive.ReadDouble();
            ProngCount = archive.ReadInt();
            TopDiameter = archive.ReadDouble();
            MidDiameter = archive.ReadDouble();
            BottomDiameter = archive.ReadDouble();
            TopOffset = archive.ReadDouble();
            MidOffset = archive.ReadDouble();
            BottomOffset = archive.ReadDouble();
            TopProfileRotation = archive.ReadDouble();
            MidProfileRotation = archive.ReadDouble();
            BottomProfileRotation = archive.ReadDouble();

            EnableTopRail = archive.ReadBool();
            // Liest String (ab v1.1) oder Guid (Legacy Fallback wäre hier komplex, wir nehmen String an)
            TopRailProfileName = archive.ReadString();
            TopRailWidth = archive.ReadDouble();
            TopRailThickness = archive.ReadDouble();
            TopRailPosition = archive.ReadDouble();
            TopRailOffset = archive.ReadDouble();
            TopRailRotation = archive.ReadDouble();

            EnableBottomRail = archive.ReadBool();
            BottomRailProfileName = archive.ReadString();
            BottomRailWidth = archive.ReadDouble();
            BottomRailThickness = archive.ReadDouble();
            BottomRailPosition = archive.ReadDouble();
            BottomRailOffset = archive.ReadDouble();
            BottomRailRotation = archive.ReadDouble();

            ProfileName = archive.ReadString();

            var positions = archive.ReadDoubleArray();
            ProngPositions = positions != null ? new List<double>(positions) : new List<double>();

            return true;
        }

        protected override bool Write(BinaryArchiveWriter archive)
        {
            archive.Write3dmChunkVersion(MAJOR, MINOR);

            archive.WriteGuid(GemId);
            archive.WritePlane(GemPlane);
            archive.WriteDouble(Height);
            archive.WriteDouble(DepthBelowGem);
            archive.WriteDouble(GemInside);
            archive.WriteInt(ProngCount);
            archive.WriteDouble(TopDiameter);
            archive.WriteDouble(MidDiameter);
            archive.WriteDouble(BottomDiameter);
            archive.WriteDouble(TopOffset);
            archive.WriteDouble(MidOffset);
            archive.WriteDouble(BottomOffset);
            archive.WriteDouble(TopProfileRotation);
            archive.WriteDouble(MidProfileRotation);
            archive.WriteDouble(BottomProfileRotation);

            archive.WriteBool(EnableTopRail);
            archive.WriteString(TopRailProfileName ?? "Round");
            archive.WriteDouble(TopRailWidth);
            archive.WriteDouble(TopRailThickness);
            archive.WriteDouble(TopRailPosition);
            archive.WriteDouble(TopRailOffset);
            archive.WriteDouble(TopRailRotation);

            archive.WriteBool(EnableBottomRail);
            archive.WriteString(BottomRailProfileName ?? "Round");
            archive.WriteDouble(BottomRailWidth);
            archive.WriteDouble(BottomRailThickness);
            archive.WriteDouble(BottomRailPosition);
            archive.WriteDouble(BottomRailOffset);
            archive.WriteDouble(BottomRailRotation);

            archive.WriteString(ProfileName ?? "Round");
            archive.WriteDoubleArray(ProngPositions.ToArray());

            return true;
        }
    }
}