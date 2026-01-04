using System;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;

namespace NewRhinoGold.Core
{
    // --- Architecture Placeholders ---
    // Diese Klassen stellen sicher, dass wir auch für Heads, Cutters und Prongs
    // die gleiche typ-sichere Selektionslogik verwenden können wie für Gems und Bezels.

    [Guid("C1D2E3F4-0000-0000-0000-000000000001")]
    public class HeadSmartData : UserData
    {
        public override string Description => "NewRhinoGold Smart Head";
        
        // Minimal-Implementierung für Selektion
        protected override bool Read(BinaryArchiveReader archive) => true;
        protected override bool Write(BinaryArchiveWriter archive) => true;
    }

    [Guid("C1D2E3F4-0000-0000-0000-000000000002")]
    public class CutterSmartData : UserData
    {
        public override string Description => "NewRhinoGold Smart Cutter";
        
        protected override bool Read(BinaryArchiveReader archive) => true;
        protected override bool Write(BinaryArchiveWriter archive) => true;
    }

    [Guid("C1D2E3F4-0000-0000-0000-000000000003")]
    public class ProngSmartData : UserData
    {
        public override string Description => "NewRhinoGold Smart Prong";
        
        protected override bool Read(BinaryArchiveReader archive) => true;
        protected override bool Write(BinaryArchiveWriter archive) => true;
    }
}