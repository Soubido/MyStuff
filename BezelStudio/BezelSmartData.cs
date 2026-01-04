using System;
using Rhino.DocObjects.Custom;
using Rhino.Geometry;
using Rhino.FileIO;

namespace NewRhinoGold.BezelStudio
{
    // WICHTIG: Jede UserData-Klasse braucht eine eindeutige GUID!
    [System.Runtime.InteropServices.Guid("B8A5C2D1-4444-5555-6666-ABCDEF123456")]
    public class BezelSmartData : UserData
    {
        // Datenfelder, die wir speichern wollen
        public double Height { get; set; }
        public double ThicknessTop { get; set; }
        public double Offset { get; set; }
        public Guid GemId { get; set; }
        public Plane GemPlane { get; set; }

        // Leerer Konstruktor (wichtig für Rhino beim Laden)
        public BezelSmartData() 
        { 
        }

        // Unser Konstruktor zum Erstellen
        public BezelSmartData(double height, double thickTop, double offset, Guid gemId, Plane plane)
        {
            Height = height;
            ThicknessTop = thickTop;
            Offset = offset;
            GemId = gemId;
            GemPlane = plane;
        }

        public override string Description => "Smart Bezel Data";

        // Speichern (Write)
        protected override bool Write(BinaryArchiveWriter archive)
        {
            // Versionierung hilft später bei Updates
            archive.Write3dmChunkVersion(1, 0);

            archive.WriteDouble(Height);
            archive.WriteDouble(ThicknessTop);
            archive.WriteDouble(Offset);
            archive.WriteGuid(GemId);
            archive.WritePlane(GemPlane);

            return true;
        }

        // Laden (Read)
        protected override bool Read(BinaryArchiveReader archive)
        {
            archive.Read3dmChunkVersion(out var major, out var minor);

            Height = archive.ReadDouble();
            ThicknessTop = archive.ReadDouble();
            Offset = archive.ReadDouble();
            GemId = archive.ReadGuid();
            GemPlane = archive.ReadPlane();

            return true;
        }
    }
}