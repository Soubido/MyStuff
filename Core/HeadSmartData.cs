using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    [Guid("A1B2C3D4-E5F6-7890-1234-56789ABCDE01")]
    public class HeadSmartData : UserData
    {
        private const int MAJOR = 1;
        private const int MINOR = 0;

        // Referenz zum Stein
        public Guid GemId { get; set; }
        public Plane GemPlane { get; set; }

        // Parameter (Kopie von HeadParameters)
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
        
        // Rotationen
        public double TopProfileRotation { get; set; }
        public double MidProfileRotation { get; set; }
        public double BottomProfileRotation { get; set; }

        // Rails
        public bool EnableTopRail { get; set; }
        public Guid TopRailProfileId { get; set; }
        public double TopRailWidth { get; set; }
        public double TopRailThickness { get; set; }
        public double TopRailPosition { get; set; }
        public double TopRailOffset { get; set; }
        public double TopRailRotation { get; set; }

        public bool EnableBottomRail { get; set; }
        public Guid BottomRailProfileId { get; set; }
        public double BottomRailWidth { get; set; }
        public double BottomRailThickness { get; set; }
        public double BottomRailPosition { get; set; }
        public double BottomRailOffset { get; set; }
        public double BottomRailRotation { get; set; }

        public Guid ProfileId { get; set; }
        
        // Liste der Krappenpositionen
        public List<double> ProngPositions { get; set; } = new List<double>();

        public HeadSmartData() { }

        // Konstruktor basierend auf Parametern
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
            TopRailProfileId = p.TopRailProfileId;
            TopRailWidth = p.TopRailWidth;
            TopRailThickness = p.TopRailThickness;
            TopRailPosition = p.TopRailPosition;
            TopRailOffset = p.TopRailOffset;
            TopRailRotation = p.TopRailRotation;

            EnableBottomRail = p.EnableBottomRail;
            BottomRailProfileId = p.BottomRailProfileId;
            BottomRailWidth = p.BottomRailWidth;
            BottomRailThickness = p.BottomRailThickness;
            BottomRailPosition = p.BottomRailPosition;
            BottomRailOffset = p.BottomRailOffset;
            BottomRailRotation = p.BottomRailRotation;

            ProfileId = p.ProfileId;
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
            TopRailProfileId = archive.ReadGuid();
            TopRailWidth = archive.ReadDouble();
            TopRailThickness = archive.ReadDouble();
            TopRailPosition = archive.ReadDouble();
            TopRailOffset = archive.ReadDouble();
            TopRailRotation = archive.ReadDouble();

            EnableBottomRail = archive.ReadBool();
            BottomRailProfileId = archive.ReadGuid();
            BottomRailWidth = archive.ReadDouble();
            BottomRailThickness = archive.ReadDouble();
            BottomRailPosition = archive.ReadDouble();
            BottomRailOffset = archive.ReadDouble();
            BottomRailRotation = archive.ReadDouble();

            ProfileId = archive.ReadGuid();
            
            // Liste lesen
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
            archive.WriteGuid(TopRailProfileId);
            archive.WriteDouble(TopRailWidth);
            archive.WriteDouble(TopRailThickness);
            archive.WriteDouble(TopRailPosition);
            archive.WriteDouble(TopRailOffset);
            archive.WriteDouble(TopRailRotation);

            archive.WriteBool(EnableBottomRail);
            archive.WriteGuid(BottomRailProfileId);
            archive.WriteDouble(BottomRailWidth);
            archive.WriteDouble(BottomRailThickness);
            archive.WriteDouble(BottomRailPosition);
            archive.WriteDouble(BottomRailOffset);
            archive.WriteDouble(BottomRailRotation);

            archive.WriteGuid(ProfileId);
            
            // Liste schreiben
            archive.WriteDoubleArray(ProngPositions.ToArray());

            return true;
        }
    }
}