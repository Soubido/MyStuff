using System;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
    [Guid("F1E2D3C4-B5A6-9780-1234-CDEF56789012")]
    public class CutterSmartData : UserData
    {
        private const int MAJOR = 1;
        private const int MINOR = 1; // Version erhöht wegen String-Wechsel

        public Guid GemId { get; set; }

        public double GlobalScale { get; set; }
        public double Clearance { get; set; }
        public double TopHeight { get; set; }
        public double TopDiameterScale { get; set; }
        public double SeatLevel { get; set; }
        public double BottomHeight { get; set; }
        public double BottomDiameterScale { get; set; }

        public bool UseCustomProfile { get; set; }
        public string ProfileName { get; set; } // CHANGE: String
        public double ProfileRotation { get; set; }

        public CutterSmartData() { }

        public CutterSmartData(CutterParameters p, Guid gemId)
        {
            GemId = gemId;
            GlobalScale = p.GlobalScale;
            Clearance = p.Clearance;
            TopHeight = p.TopHeight;
            TopDiameterScale = p.TopDiameterScale;
            SeatLevel = p.SeatLevel;
            BottomHeight = p.BottomHeight;
            BottomDiameterScale = p.BottomDiameterScale;
            UseCustomProfile = p.UseCustomProfile;
            ProfileName = p.ProfileName;
            ProfileRotation = p.ProfileRotation;
        }

        public override string Description => "Smart Cutter Data";

        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out int major, out int minor);
            GemId = archive.ReadGuid();
            GlobalScale = archive.ReadDouble();
            Clearance = archive.ReadDouble();
            TopHeight = archive.ReadDouble();
            TopDiameterScale = archive.ReadDouble();
            SeatLevel = archive.ReadDouble();
            BottomHeight = archive.ReadDouble();
            BottomDiameterScale = archive.ReadDouble();
            UseCustomProfile = archive.ReadBool();

            // Liest String (ab v1.1) oder Guid als Fallback
            if (minor >= 1) ProfileName = archive.ReadString();
            else { archive.ReadGuid(); ProfileName = "Round"; } // Legacy ignore

            ProfileRotation = archive.ReadDouble();
            return true;
        }

        protected override bool Write(BinaryArchiveWriter archive)
        {
            archive.Write3dmChunkVersion(MAJOR, MINOR);
            archive.WriteGuid(GemId);
            archive.WriteDouble(GlobalScale);
            archive.WriteDouble(Clearance);
            archive.WriteDouble(TopHeight);
            archive.WriteDouble(TopDiameterScale);
            archive.WriteDouble(SeatLevel);
            archive.WriteDouble(BottomHeight);
            archive.WriteDouble(BottomDiameterScale);
            archive.WriteBool(UseCustomProfile);

            archive.WriteString(ProfileName ?? "Round"); // CHANGE

            archive.WriteDouble(ProfileRotation);
            return true;
        }
    }
}