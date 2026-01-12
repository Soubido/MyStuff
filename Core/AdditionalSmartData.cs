using System;
using System.Runtime.InteropServices;
using Rhino.DocObjects.Custom;
using Rhino.FileIO;

namespace NewRhinoGold.Core
{
    // BEREINIGT: HeadSmartData und CutterSmartData entfernt (jetzt in eigenen Dateien).
    // ProngSmartData bleibt hier, da es von SelectionHelpers.cs benötigt wird.

    [Guid("C1D2E3F4-0000-0000-0000-000000000003")]
    public class ProngSmartData : UserData
    {
        public override string Description => "NewRhinoGold Smart Prong";

        protected override bool Read(BinaryArchiveReader archive) => true;
        protected override bool Write(BinaryArchiveWriter archive) => true;
    }
}