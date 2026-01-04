using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using NewRhinoGold.Core;
using NewRhinoGold.Helpers;

namespace NewRhinoGold.Commands
{
    public class ExtractGemCurve : Command
    {
        public override Guid Id => new Guid("F5E4D3C2-B1A0-4987-6543-210987FEDCBA");
        public override string EnglishName => "ExtractGemCurve";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var go = new GetObject();
            go.SetCommandPrompt("Select gems to extract curve");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh;
            go.EnablePreSelect(true, true);
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var newCurveIds = new List<Guid>();

            // CS0234 Fix: Implemented correct BeginUndoRecord/EndUndoRecord pattern
            uint sn = doc.BeginUndoRecord("Extract Gem Curves");
            try
            {
                foreach (var objRef in go.Objects())
                {
                    var obj = objRef.Object();
                    if (obj == null) continue;

                    Curve curveToExtract = null;

                    if (obj.Geometry.UserData.Find(typeof(GemSmartData)) is GemSmartData smartData && smartData.BaseCurve != null)
                    {
                        curveToExtract = smartData.BaseCurve.DuplicateCurve();
                    }

                    if (curveToExtract == null)
                    {
                        if (RhinoGoldHelper.TryGetGemData(obj, out Curve calculatedCurve, out _, out _))
                        {
                            curveToExtract = calculatedCurve;
                        }
                    }

                    if (curveToExtract != null)
                    {
                        var attr = doc.CreateDefaultAttributes();
                        Guid id = doc.Objects.AddCurve(curveToExtract, attr);
                        if (id != Guid.Empty)
                        {
                            newCurveIds.Add(id);
                        }
                    }
                }
            }
            finally
            {
                doc.EndUndoRecord(sn);
            }

            if (newCurveIds.Count > 0)
            {
                doc.Objects.UnselectAll();
                foreach (var id in newCurveIds)
                    doc.Objects.Select(id);

                doc.Views.Redraw();
                RhinoApp.WriteLine($"Extracted {newCurveIds.Count} curves.");
                return Result.Success;
            }
            else
            {
                RhinoApp.WriteLine("No curves could be extracted. Are the selected objects valid gems?");
                return Result.Failure;
            }
        }
    }
}