using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;
using Rhino.Geometry;

namespace NewRhinoGold.Core
{
	// Speichert die Einstellungen für EINE Position (z.B. "Oben")
	public class RingSmartSection
	{
		public int PositionIndex { get; set; } // Cast von RingPosition
		public double Width { get; set; }
		public double Height { get; set; }
		public double Rotation { get; set; }
		public double OffsetY { get; set; }
		public string ProfileName { get; set; }
		public bool IsActive { get; set; }
		public bool FlipX { get; set; }
	}

	[Guid("E5D4C3B2-A100-4BCD-9999-888877776666")]
	public class RingSmartData : UserData
	{
		private const int MAJOR = 1;
		private const int MINOR = 0;

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
			if (sections != null) Sections = new List<RingSmartSection>(sections);
		}

		public override string Description => "NewRhinoGold Smart Ring";

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
				archive.WriteString(s.ProfileName);
				archive.WriteBool(s.IsActive);
				archive.WriteBool(s.FlipX);
			}
			return true;
		}
	}
}