using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript;
using System.Text;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using System;
//using Tex2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;
//using SixLabors.ImageSharp.PixelFormats;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorer.Misc.ExperimentsTools;
//using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using Microsoft.Win32;
using System.IO;
using System.Numerics;
using static LegendaryExplorerCore.Unreal.PSA;
using LegendaryExplorerCore.Save;
using DocumentFormat.OpenXml.Bibliography;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    static internal class PackageEditorExperimentsSquid
    {
        public static void MakeCustomMorphTargetSet(PackageEditorWindow pew)
        {
            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return; }

            if (!(pew.SelectedItem.Entry.ClassName == "MorphTargetSet" || pew.SelectedItem.Entry.ClassName == "SkeletalMesh"))
            {
                ShowError("Selected item is not a MorphTargetSet or SkeletalMesh");
                return;
            }

            var SelectedExport = (ExportEntry)pew.SelectedItem.Entry;
            ExportEntry morphTargetSet = null;
            ExportEntry headMesh;

            if (SelectedExport.ClassName == "MorphTargetSet")
            {
                morphTargetSet = SelectedExport;
                headMesh = (ExportEntry)morphTargetSet.GetProperty<ObjectProperty>("BaseSkelMesh").ResolveToEntry(pew.Pcc);
            }
            else
            {
                headMesh = SelectedExport;
            }

            EnsureParentClassExists(pew);
            var newClass = CreateCustomMorphTargetSet(pew, morphTargetSet, headMesh);
            pew.GoToNumber(newClass.UIndex);
        }

        private static ExportEntry CreateCustomMorphTargetSet(PackageEditorWindow pew, ExportEntry morphTargetSet, ExportEntry headMesh)
        {
            var sb = new StringBuilder();

            var className = morphTargetSet == null ? headMesh.ObjectName : morphTargetSet.ObjectName;

            sb.AppendLine($"Class {className} extends CustomMorphTargetSet config(game);");
            sb.AppendLine("defaultproperties {");
            sb.AppendLine(HandleSkeletalMesh(pew, headMesh));
            if (morphTargetSet != null)
            {
                sb.AppendLine(HandleVanillaMorphTargetSet(pew, morphTargetSet));
            }
            sb.AppendLine("}");

            return MakeNewClass(pew, null, sb.ToString(), className);
        }

        private static string HandleVanillaMorphTargetSet(PackageEditorWindow pew, ExportEntry morphTargetSet)
        {
            var sb = new StringBuilder();

            var targets = morphTargetSet.GetProperty<ArrayProperty<ObjectProperty>>("Targets");

            sb.AppendLine("\tBaseMorphTargets = (");
            for (int k = 0; k < targets.Count; k++)
            {
                var target = targets[k];
                var expEntryTarget = (ExportEntry)target.ResolveToEntry(pew.Pcc);
                // get the binary data from the export
                var targetBinary = expEntryTarget.GetBinaryData<MorphTarget>();

                // add the bone offsets from this target
                sb.AppendLine($"\t\t{{TargetName = '{expEntryTarget.ObjectNameString}',");

                sb.Append("\t\t\tBoneOffsets=(");
                for (int i = 0; i < targetBinary.BoneOffsets.Length; i++)
                {
                    var boneOffset = targetBinary.BoneOffsets[i];
                    sb.Append($"{{Bone = '{boneOffset.Bone}',Offset = {{X = {boneOffset.Offset.X:F8}, Y = {boneOffset.Offset.Y:F8}, Z = {boneOffset.Offset.Z:F8}}}}}{(i < targetBinary.BoneOffsets.Length - 1 ? "," : "")}");
                }
                sb.AppendLine("),");

                sb.Append("\t\t\tLodModels = (");
                for (int i = 0; i < targetBinary.MorphLODModels.Length; i++)
                {
                    var lodModel = targetBinary.MorphLODModels[i];
                    sb.Append($"{{NumBaseMeshVertices={lodModel.NumBaseMeshVerts},vertices = (");

                    for (int j = 0; j < lodModel.Vertices.Length; j++)
                    {
                        var vert = lodModel.Vertices[j];
                        sb.Append($"{{sourceIndex = {vert.SourceIdx},PositionDelta = {{X = {vert.PositionDelta.X:F8}, Y = {vert.PositionDelta.Y:F8}, Z = {vert.PositionDelta.Z:F8}}}}}{(j < lodModel.Vertices.Length - 1 ? "," : "")}");
                    }
                    sb.Append($")}}{(i < targetBinary.MorphLODModels.Length - 1 ? "," : "")}");
                }
                sb.Append(")");

                sb.AppendLine().AppendLine($"\t\t}}{(k < targets.Count - 1 ? "," : "")}");
            }

            // close targets
            sb.AppendLine("\t)");

            return sb.ToString();
        }

        private static ExportEntry GetOrCreatePackageFolder(PackageEditorWindow pew, string packageName)
        {
            var folder = pew.Pcc.FindExport(packageName);

            if (folder == null)
            {
                IEntry packageClass = pew.Pcc.GetEntryOrAddImport("Core.Package", "Class", "Core");
                folder = new ExportEntry(pew.Pcc, 0, packageName)
                {
                    Class = packageClass
                };
                pew.Pcc.AddExport(folder);
                folder = pew.Pcc.FindExport(packageName);
            }

            return folder;
        }

        private static ExportEntry CreateBioMorphFace(PackageEditorWindow pew, string objectName)
        {
            IEntry BioMorphFaceClass = pew.Pcc.GetEntryOrAddImport("SFXGame.BioMorphFace", "Class", "Core");
            var morphFace = new ExportEntry(pew.Pcc, 0, objectName)
            {
                Class = BioMorphFaceClass
            };
            pew.Pcc.AddExport(morphFace);
            morphFace = pew.Pcc.FindExport(objectName);

            return morphFace;
        }

        private static ExportEntry EnsureParentClassExists(PackageEditorWindow pew)
        {
            const string ParentClassText = @"Class CustomMorphTargetSet
    config(game);

// Types
struct BoneOffset 
{
    var Name Bone;
    var Vector Offset;
};
struct CustomMorphTarget 
{
    struct VertexOffset 
    {
        var int sourceIndex;
        var Vector PositionDelta;
    };
    struct LodModel 
    {
        var int NumBaseMeshVertices;
        var array<VertexOffset> vertices;
        
        structdefaultproperties
        {
            vertices = ()
        }
    };
    var array<BoneOffset> BoneOffsets;
    var array<LodModel> LodModels;
    var Name TargetName;
    
    structdefaultproperties
    {
        BoneOffsets = ()
        LodModels = ()
    }
};
struct MeshVertices 
{
    var array<Vector> vertices;
    
    structdefaultproperties
    {
        vertices = ()
    }
};

// Variables
var array<CustomMorphTarget> BaseMorphTargets;
var config array<CustomMorphTarget> CustomMorphTargets;
var array<BoneOffset> OriginalMeshBoneOffsets;
var array<MeshVertices> OriginalMeshLodModels;

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}";
            const string ParentClassPackage = "MeshTools";
            const string ParentClassName = "CustomMorphTargetSet";

            var parentClass = pew.Pcc.FindExport($"{ParentClassPackage}.{ParentClassName}");

            if (parentClass != null)
            {
                return parentClass;
            }

            var parentFolder = GetOrCreatePackageFolder(pew, ParentClassPackage);

            return MakeNewClass(pew, parentFolder, ParentClassText, ParentClassName);
        }

        private static ExportEntry MakeNewClass(PackageEditorWindow pew, IEntry parent, string classText, string className)
        {
            var usop = new UnrealScriptOptionsPackage();
            var fileLib = new FileLib(pew.Pcc);
            if (!fileLib.Initialize(usop))
            {
                var dlg = new ListDialog(fileLib.InitializationLog.AllErrors.Select(msg => msg.ToString()), "Script Error", "Could not build script database for this file!", pew);
                dlg.Show();
                throw new Exception("fileLib failed to initialize");
            }
            (_, MessageLog log) = UnrealScriptCompiler.CompileClass(pew.Pcc, classText, fileLib, usop, parent: parent);
            if (log.HasErrors)
            {
                var dlg = new ListDialog(log.AllErrors.Select(msg => msg.ToString()), "Script Error", "Could not create class!", pew);
                dlg.Show();
                throw new Exception("class failed to compile");
            }

            string fullPath = parent is null ? className : $"{parent.InstancedFullPath}.{className}";
            return (ExportEntry)pew.Pcc.FindEntry(fullPath);
        }

        private static string HandleSkeletalMesh(PackageEditorWindow pew, ExportEntry headMesh)
        {
            var meshBinary = headMesh.GetBinaryData<SkeletalMesh>();
            var morphHeadBinary = new LegendaryExplorerCore.Unreal.BinaryConverters.BioMorphFace();
            var morphHeadProps = new PropertyCollection();
            var morphHeadSkeleton = new ArrayProperty<StructProperty>("m_aFinalSkeleton");

            morphHeadProps.Add(new ObjectProperty(headMesh, "m_oBaseHead"));

            var MorphHeadExcludeBones = new List<string> { "God", "Root", "LowerBack", "Chest", "Chest1", "Chest2", "Neck", "Neck1", "Head", };
            // m_aFinalSkeleton, m_oBaseHead

            var sb = new StringBuilder();

            // add the original mesh bone offsets (ref skeleton)
            sb.AppendLine("\tOriginalMeshBoneOffsets = (");
            for (int i = 0; i < meshBinary.RefSkeleton.Length; i++)
            {
                var refBone = meshBinary.RefSkeleton[i];
                sb.AppendLine($"\t\t{{Bone = '{refBone.Name}',Offset = {{X = {refBone.Position.X:F8}, Y = {refBone.Position.Y:F8}, Z = {refBone.Position.Z:F8}}}}}{(i < meshBinary.RefSkeleton.Length - 1 ? "," : "")}");

                if (!MorphHeadExcludeBones.Contains(refBone.Name))
                {
                    morphHeadSkeleton.Add(new StructProperty("OffsetBonePos",
                        false,
                        new NameProperty(refBone.Name, "nName"),
                        new StructProperty("Vector", true,
                            new FloatProperty(refBone.Position.X, "X"),
                            new FloatProperty(refBone.Position.Y, "Y"),
                            new FloatProperty(refBone.Position.Z, "Z")
                            )
                        { Name = "vPos" }));
                }
            }
            sb.AppendLine("\t)");

            morphHeadProps.Add(morphHeadSkeleton);

            // add the original mesh vertices
            sb.AppendLine("\tOriginalMeshLodModels = (");
            morphHeadBinary.LODs = new System.Numerics.Vector3[meshBinary.LODModels.Length][];
            for (int i = 0; i < meshBinary.LODModels.Length; i++)
            {
                var lodModel = meshBinary.LODModels[i];
                morphHeadBinary.LODs[i] = new System.Numerics.Vector3[lodModel.VertexBufferGPUSkin.VertexData.Length];
                var morphLod = morphHeadBinary.LODs[i];

                sb.Append("\t\t{vertices = (");
                for (int j = 0; j < lodModel.VertexBufferGPUSkin.VertexData.Length; j++)
                {
                    var vert = lodModel.VertexBufferGPUSkin.VertexData[j];
                    sb.Append($"{{X = {vert.Position.X:F8},Y = {vert.Position.Y:F8}, Z = {vert.Position.Z:F8}}}{(j < lodModel.VertexBufferGPUSkin.VertexData.Length - 1 ? "," : "")}");
                    morphLod[j] = new System.Numerics.Vector3(vert.Position.X, vert.Position.Y, vert.Position.Z);
                }
                sb.AppendLine($")}}{(i < meshBinary.LODModels.Length - 1 ? "," : "")}");
            }
            sb.AppendLine("\t)");

            var morphHead = CreateBioMorphFace(pew, headMesh.ObjectName + "_MorphHead");

            morphHead.WritePropertiesAndBinary(morphHeadProps, morphHeadBinary);

            return sb.ToString();
        }

        private static bool GetSelectedMeshBinary(PackageEditorWindow pew, out ExportEntry meshExport, out SkeletalMesh binary)
        {
            meshExport = null;
            binary = null;

            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return false; }

            if (pew.SelectedItem.Entry.ClassName != "SkeletalMesh")
            {
                ShowError("Selected item is not a SkeletalMesh");
                return false;
            }

            meshExport = (ExportEntry)pew.SelectedItem.Entry;
            binary = meshExport.GetBinaryData<SkeletalMesh>();

            return true;
        }

        public static void GetMeshMaterials(PackageEditorWindow pew)
        {
            List<string> mats = [];
            // get the export and binary of the Skeletal Mesh that is currently selected, if any
            if (GetSelectedMeshBinary(pew, out _, out var meshBinary))
            {

                foreach (var uIndex in meshBinary.Materials)
                {
                    var entry = pew.Pcc.GetEntry(uIndex);
                    mats.Add($"\"{entry.MemoryFullPath}\"");
                }

                var result = string.Join(",", mats);
                Clipboard.SetText(result);
            }
        }

        public static void MakeHeterochromiaMesh(PackageEditorWindow pew)
        {
            // get the export and binary of the Skeletal Mesh that is currently selected, if any
            if (GetSelectedMeshBinary(pew, out var headMesh, out var meshBinary))
            {
                // ask the user to pick which material is the eye material
                var chosenMaterialIndex = ChooseMaterial(pew, meshBinary, "Which material is the eye material?");
                if (chosenMaterialIndex == -1)
                {
                    return;
                }
                // add a new material slot to split the right eye into
                var newMaterialIndex = AddMaterialSlot(meshBinary);

                // from there, find the section we need to modify
                foreach (var lod in meshBinary.LODModels)
                {
                    SplitMaterial(lod, chosenMaterialIndex, newMaterialIndex, IsRightEyeTriangle);
                }

                headMesh.WriteBinary(meshBinary);
            }
        }

        //public static void SplitTeethFromScalp(PackageEditorWindow pew)
        //{
        //    // get the export and binary of the Skeletal Mesh that is currently selected, if any
        //    if (GetSelectedMeshBinary(pew, out var headMesh, out var meshBinary))
        //    {
        //        // ask the user to pick which material they want to split (usually scalp material)
        //        var chosenMaterialIndex = ChooseMaterial(pew, meshBinary, "Which material contains the mouthbox?");
        //        if (chosenMaterialIndex == -1)
        //        {
        //            return;
        //        }
        //        // add a new material slot to split the mouthbox into
        //        var newMaterialIndex = AddMaterialSlot(meshBinary);

        //        // now, for an annoying part: look up the teeth mask texture from that material
        //        var scalpMICUIndex = meshBinary.Materials[chosenMaterialIndex];

        //        if (!pew.Pcc.TryGetUExport(scalpMICUIndex, out var scalpMIC))
        //        {
        //            ShowError("invalid material export; try porting it in instead of using an import");
        //            return;
        //        }

        //        var teethMaskTexExport = pew.Pcc.FindExport("BRT.HMM_BRT_MED_Spwr_Stack");

        //        //var textureParams = scalpMIC.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues");
        //        //if (textureParams == null)
        //        //{
        //        //    ShowError("could not find teeth mask");
        //        //    return;
        //        //}

        //        //var teethMaskParam = textureParams.FirstOrDefault(x => x.GetProp<NameProperty>("ParameterName")?.Value.ToString() == "HED_Teeth_Diff");
        //        //if (teethMaskParam == null)
        //        //{
        //        //    ShowError("could not find teeth mask");
        //        //    return;
        //        //}

        //        //if (!pew.Pcc.TryGetUExport(teethMaskParam.GetProp<ObjectProperty>("ParameterValue").Value, out var teethMaskTexExport))
        //        //{
        //        //    ShowError("could not find teeth mask");
        //        //    return;
        //        //}

        //        var teethMaskImg = ToIsImage(new Tex2D(teethMaskTexExport));

        //        bool IsMouthBoxTriangle(StaticLODModel lod, int triangleIndex)
        //        {
        //            var (v1, v2, v3) = GetTriangle(lod, triangleIndex);

        //            var uvCentroidX = (GetVertex(lod, v1).UV.X + GetVertex(lod, v2).UV.X + GetVertex(lod, v3).UV.X) / 3;
        //            var uvCentroidY = (GetVertex(lod, v1).UV.Y+ GetVertex(lod, v2).UV.Y + GetVertex(lod, v3).UV.Y) / 3;

        //            var centroidPixel = GetPixel(teethMaskImg, uvCentroidX, uvCentroidY);

        //            return centroidPixel.B > 0;
        //        }

        //        // from there, find the section we need to modify
        //        SplitMaterial(meshBinary.LODModels.First(), chosenMaterialIndex, newMaterialIndex, IsMouthBoxTriangle);

        //        headMesh.WriteBinary(meshBinary);
        //    }
        //}

        //private static Bgra32 GetPixel(SixLabors.ImageSharp.Image<Bgra32> img, float x, float y)
        //{
        //    x = x % 1;
        //    y = y % 1;
        //    return img[(int)(img.Width * x), (int)(img.Height * y)];
        //}

        //private static SixLabors.ImageSharp.Image<Bgra32> ToIsImage(Tex2D tex)
        //{
        //    var rawPng = tex.GetPNG(tex.GetTopMip());
        //    return SixLabors.ImageSharp.Image.Load<Bgra32>(rawPng);
        //}

        //public static byte[] GetPNG(LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo info, Tex2D tex)
        //{
        //    PixelFormat format = Image.getPixelFormatType(tex.TextureFormat);
        //    return ConvertToPng(Tex2D.GetTextureData(info, tex.Export.Game), info.width, info.height, format)
        //        .ToArray();
        //}

        //public static MemoryStream ConvertToPng(byte[] src, int w, int h, PixelFormat format)
        //{
        //    byte[] tmpData = Image.convertRawToARGB(src, ref w, ref h, format);
        //    var ms = new MemoryStream();
        //    var im = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(tmpData, w, h);
        //    //im.SaveAsPng(ms);
        //    ms.Position = 0;
        //    return ms;
        //}

        private static void SplitMaterial(StaticLODModel lod, int originalMaterialIndex, int newMaterialIndex, Func<StaticLODModel, int, bool> isTriangleNewMaterial)
        {
            SkelMeshSection targetSection = null;
            int targetSectionIndex = -1;
            for (int i = 0; i < lod.Sections.Length; i++)
            {
                var section = lod.Sections[i];
                if (section.MaterialIndex == originalMaterialIndex)
                {
                    targetSectionIndex = i;
                    targetSection = section;
                    break;
                }
            }

            if (targetSection == null)
            {
                return;
            }

            var newSections = new List<SkelMeshSection>();

            for (int i = 0; i < targetSectionIndex; i++)
            {
                newSections.Add(lod.Sections[i]);
            }

            bool isNewMaterial = isTriangleNewMaterial(lod, (int)targetSection.BaseIndex);
            int currentTriangleCount = 0;
            int currentBaseIndex = (int)targetSection.BaseIndex;
            for (int i = (int)targetSection.BaseIndex; i < (int)targetSection.BaseIndex + targetSection.NumTriangles * 3; i += 3)
            {
                if (isTriangleNewMaterial(lod, i) == isNewMaterial)
                {
                    currentTriangleCount++;
                    continue;
                }

                newSections.Add(new SkelMeshSection()
                {
                    BaseIndex = (uint)currentBaseIndex,
                    ChunkIndex = targetSection.ChunkIndex,
                    MaterialIndex = (ushort)(isNewMaterial ? newMaterialIndex : originalMaterialIndex),
                    NumTriangles = currentTriangleCount,
                    TriangleSorting = targetSection.TriangleSorting
                });

                isNewMaterial = !isNewMaterial;
                currentBaseIndex = i;
                currentTriangleCount = 1;
            }

            newSections.Add(new SkelMeshSection()
            {
                BaseIndex = (uint)currentBaseIndex,
                ChunkIndex = targetSection.ChunkIndex,
                MaterialIndex = (ushort)(isNewMaterial ? newMaterialIndex : originalMaterialIndex),
                NumTriangles = currentTriangleCount,
                TriangleSorting = targetSection.TriangleSorting
            });

            for (int i = targetSectionIndex + 1; i < lod.Sections.Length; i++)
            {
                newSections.Add(lod.Sections[i]);
            }

            lod.Sections = [.. newSections];
        }

        private static (int, int, int) GetTriangle(StaticLODModel lod, int triangleIndex)
        {
            return (lod.IndexBuffer[triangleIndex], lod.IndexBuffer[triangleIndex + 1], lod.IndexBuffer[triangleIndex + 2]);
        }

        private static GPUSkinVertex GetVertex(StaticLODModel lod, int vertIndex)
        {
            return lod.VertexBufferGPUSkin.VertexData[vertIndex];
        }

        private static bool IsRightEyeTriangle(StaticLODModel lod, int triangleIndex)
        {
            var (v1, v2, v3) = GetTriangle(lod, triangleIndex);

            var numRightVerts = 0;
            if (IsRightEyeVertex(lod, v1))
            {
                numRightVerts++;
            }
            if (IsRightEyeVertex(lod, v2))
            {
                numRightVerts++;
            }
            if (IsRightEyeVertex(lod, v3))
            {
                numRightVerts++;
            }
            // if any of the three vertices is on the right side of the mesh, count this triangle as being on the right side
            return numRightVerts >= 1;
        }

        private static bool IsRightEyeVertex(StaticLODModel lod, int vertIndex)
        {
            // consider values very close to 0 as being 0 to avoid triangles that just barely cross onto the right side as being on the right side
            return GetVertex(lod, vertIndex).Position.Y > 0.0001;
        }

        private static int ChooseMaterial(PackageEditorWindow pew, SkeletalMesh meshBinary, string prompt)
        {
            var materialChoices = meshBinary.Materials.Select<int, IEntry>(x => x switch {
                < 0 => pew.Pcc.GetImport(x),
                0 => null,
                > 0 => pew.Pcc.GetUExport(x)
            }).ToList();

            var mat = EntrySelector.GetEntry<IEntry>(pew, pew.Pcc, prompt,
                    exp => materialChoices.Contains(exp));

            if (mat == null)
            {
                return -1;
            }
            return materialChoices.IndexOf(mat);
        }

        private static int AddMaterialSlot(SkeletalMesh meshBinary)
        {
            var tempMaterials = meshBinary.Materials;
            meshBinary.Materials = new int[meshBinary.Materials.Length + 1];
            for (int i = 0; i < tempMaterials.Length; i++)
            {
                meshBinary.Materials[i] = tempMaterials[i];
            }

            return meshBinary.Materials.Length - 1;
        }

        private static void ShowError(string errMsg)
        {
            MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
        }

        public static void BioMorphFaceToMesh(PackageEditorWindow pew)
        {
            // make sure something is selected, a package is open ,and the right thing is selected
            if (!GetSelectedItem(pew, "BioMorphFace", out var bmf) || bmf.GetProperty<ObjectProperty>("m_oBaseHead") == null)
            {
                ShowError("You must select a BioMorphFace with a base head mesh for this command to work");
                return;
            }

            var baseHeadMesh = pew.Pcc.GetEntry(bmf.GetProperty<ObjectProperty>("m_oBaseHead").Value) as ExportEntry;

            // clone the base head tree
            var newHeadEntry = EntryCloner.CloneTree(baseHeadMesh, false);
            newHeadEntry.Parent = bmf.Parent;
            newHeadEntry.ObjectNameString = $"{bmf.ObjectNameString}_MDL";

            var newHeadBinary = newHeadEntry.GetBinaryData<SkeletalMesh>();

            // create new materials
            for (int i = 0; i < newHeadBinary.Materials.Length; i++)
            {
                var oldMatIndex = newHeadBinary.Materials[i];
                var newMat = ExportCreator.CreateExport(pew.Pcc, $"{bmf.ObjectNameString}_MAT_1{NumToLetter(i)}", "MaterialInstanceConstant", bmf.Parent, null, false);
                newMat.WriteProperty(new ObjectProperty(pew.Pcc.GetEntry(oldMatIndex), "Parent"));
                newHeadBinary.Materials[i] = newMat.UIndex;
                // copy the relevant material configs from the thing
                if (pew.Pcc.GetEntry(bmf.GetProperty<ObjectProperty>("m_oMaterialOverrides").Value) is ExportEntry bmo)
                {
                    BmoToMic(bmo, newMat);
                }
            }

            // next, copy the vertices from the bioMorphFace binary to the mesh binary
            var bmfBinary = bmf.GetBinaryData<LegendaryExplorerCore.Unreal.BinaryConverters.BioMorphFace>();

            for (int lodIndex = 0; lodIndex < bmfBinary.LODs.Length && lodIndex < newHeadBinary.LODModels.Length; lodIndex++)
            {
                var lod = bmfBinary.LODs[lodIndex];
                var meshData = newHeadBinary.LODModels[lodIndex];

                for (int i = 0; i < lod.Length && i < meshData.VertexBufferGPUSkin.VertexData.Length; i++)
                {
                    var bmfVert = lod[i];

                    meshData.VertexBufferGPUSkin.VertexData[i].Position.X = bmfVert.X;
                    meshData.VertexBufferGPUSkin.VertexData[i].Position.Y = bmfVert.Y;
                    meshData.VertexBufferGPUSkin.VertexData[i].Position.Z = bmfVert.Z;
                }
            }

            newHeadEntry.WriteBinary(newHeadBinary);

            // make a new BioMorphFace with the same material overrides and skeleton adjstments, but remove the binary data with the vertex positions
            // so you can use this with an edited mesh and it won't deform it
            var newBmf = EntryCloner.CloneTree(bmf);
            var newBmfBinary = newBmf.GetBinaryData<BioMorphFace>();
            newBmfBinary.LODs = [];
            newBmf.WriteBinary(newBmfBinary);
            // TODO remove the morph features (useless), point the base head to the new mesh
        }

        public static void BioMorphFaceToPskAndPsa(PackageEditorWindow pew)
        {
            // get the selected bmf and ensure it has a base head mesh
            if (!GetSelectedItem(pew, "BioMorphFace", out var bmf) || bmf.GetProperty<ObjectProperty>("m_oBaseHead") == null)
            {
                ShowError("You must select a BioMorphFace with a base head mesh for this command to work");
                return;
            }

            var folderDialog = new OpenFolderDialog()
            {
                Multiselect = false,
                Title = "Choose a folder for the output"
            };

            if (folderDialog.ShowDialog() == true)
            {
                var folder = folderDialog.FolderName;

                var baseHeadMesh = pew.Pcc.GetEntry(bmf.GetProperty<ObjectProperty>("m_oBaseHead").Value) as ExportEntry;
                var baseMeshBin = baseHeadMesh.GetBinaryData<SkeletalMesh>();

                // make most of the psk from the base head mesh
                var psk = PSK.CreateFromSkeletalMesh(baseHeadMesh.GetBinaryData<SkeletalMesh>());

                var bmfBin = bmf.GetBinaryData<BioMorphFace>();

                for (var i = 0; i < psk.Points.Count && i < bmfBin.LODs[0].Length; i++)
                {
                    // modify each point in the psk with the points from the bmf
                    var bmfPoint = bmfBin.LODs[0][i];
                    psk.Points[i] = bmfPoint with { Y = -bmfPoint.Y };
                }

                psk.ToFile(Path.Combine(folder, bmf.ObjectName + ".psk"));


                // now, output the psa file and config file
                var config = new StringBuilder();
                config.AppendLine("[RemoveTracks]");
                var psa = new PSA
                {
                    Bones = [],
                    Infos = [],
                    Keys = []
                };

                var bmfSkeleton = bmf.GetProperty<ArrayProperty<StructProperty>>("m_aFinalSkeleton");

                // add the ref skeleton into the thing
                foreach (var bone in baseMeshBin.RefSkeleton)
                {
                    psa.Bones.Add(new PSABone
                    {
                        Name = bone.Name,
                        ParentIndex = bone.ParentIndex,
                    });
                }

                psa.Infos.Add(new PSAAnimInfo
                {
                    Name = "BioMorphFaceFinalSkeleton",
                    Group = "None",
                    TotalBones = baseMeshBin.RefSkeleton.Length,
                    KeyQuotum = baseMeshBin.RefSkeleton.Length, // this would be multiplied by the number of frames, but there is just one frame
                    TrackTime = 1,
                    AnimRate = 1,
                    FirstRawFrame = 0,
                    NumRawFrames = 1
                });

                for (int i = 0; i < baseMeshBin.RefSkeleton.Length; i++)
                {
                    var refBone = baseMeshBin.RefSkeleton[i];

                    // is this bone offset by this BMF?
                    var offset = bmfSkeleton.FirstOrDefault(x => x.GetProp<NameProperty>("nName").Value == refBone.Name);
                    var rotQuat = new Quaternion(0, 0, 0, 1);
                    var posVec = new Vector3(0, 0, 0);
                    if (offset != null)
                    {
                        var pos = offset.GetProp<StructProperty>("vPos");
                        posVec = new Vector3(pos.GetProp<FloatProperty>("X"), -pos.GetProp<FloatProperty>("Y"), pos.GetProp<FloatProperty>("Z"));
                        // do not output rotation when you import this one
                        config.AppendLine($"BioMorphFaceFinalSkeleton.{i}=rot");
                    }
                    else
                    {
                        // do not output anything when you import this one
                        config.AppendLine($"BioMorphFaceFinalSkeleton.{i}=all");
                    }

                    psa.Keys.Add(new PSAAnimKeys
                    {
                        Position = posVec,
                        Rotation = rotQuat,
                        Time = 30
                    });
                }

                psa.ToFile(Path.Combine(folder, bmf.ObjectName + ".psa"));

                // also output a config file next to this to tell it to skip rotations for every sequence and every bone, and skip everythig for bones that aren't part of the pose
                File.WriteAllText(Path.Combine(folder, bmf.ObjectName + ".config"), config.ToString());
            }
        }

        private static char NumToLetter(int input)
        {
            return (char)('a' + (char)input);
        }

        private static void BmoToMic(ExportEntry source, ExportEntry targetExport)
        {
            var parentMat = SharedMethods.ResolveEntryToExport(targetExport.FileRef.GetEntry(targetExport.GetProperty<ObjectProperty>("Parent").Value), new PackageCache());

            ArrayProperty<StructProperty>? parentTextures = parentMat?.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues");
            //ArrayProperty<StructProperty> parentVectors = parentMat.GetProperty<ArrayProperty<StructProperty>>("VectorParameterValues");
            //ArrayProperty<StructProperty> parentScalars = parentMat.GetProperty<ArrayProperty<StructProperty>>("ScalarParameterValues");

            ArrayProperty<StructProperty> sourceTextures = source.GetProperty<ArrayProperty<StructProperty>>("m_aTextureOverrides");
            ArrayProperty<StructProperty> sourceVectors = source.GetProperty<ArrayProperty<StructProperty>>("m_aColorOverrides");
            ArrayProperty<StructProperty> sourceScalars = source.GetProperty<ArrayProperty<StructProperty>>("m_aScalarOverrides");

            ArrayProperty<StructProperty> targetTextures = new("TextureParameterValues");
            ArrayProperty<StructProperty> targetVectors = new("VectorParameterValues");
            ArrayProperty<StructProperty> targetScalars = new("ScalarParameterValues");

            if (sourceTextures != null)
            {
                foreach (StructProperty sourceTex in sourceTextures)
                {
                    var sourceParamName = sourceTex.GetProp<NameProperty>("nName").Value;
                    // make sure the texture exists on the base material so LEX is happier displaying it
                    if (parentTextures == null || parentTextures.Any(x => x.GetProp<NameProperty>("ParameterName").Value == sourceParamName))
                    {
                        PropertyCollection props =
                        [
                            //GenerateExpressionGUID()
                            new NameProperty(sourceParamName, "ParameterName"),
                            new ObjectProperty(sourceTex.GetProp<ObjectProperty>("m_pTexture").Value, "ParameterValue"),
                        ];
                        targetTextures.Add(new StructProperty("TextureParameterValue", props));
                    }
                }
            }

            if (sourceVectors != null)
            {
                foreach (StructProperty sourceVect in sourceVectors)
                {

                    PropertyCollection color =
                    [
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("R"),
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("G"),
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("B"),
                        sourceVect.GetProp<StructProperty>("cValue").GetProp<FloatProperty>("A"),
                    ];
                    StructProperty ParameterValue = new("LinearColor", color, "ParameterValue", true);
                    PropertyCollection props =
                    [
                        //GenerateExpressionGUID();
                        ParameterValue,
                        new NameProperty(sourceVect.GetProp<NameProperty>("nName").Value, "ParameterName"),
                    ];
                    targetVectors.Add(new StructProperty("VectorParameterValue", props));
                }
            }

            if (sourceScalars != null)
            {
                foreach (StructProperty sourceScal in sourceScalars)
                {
                    PropertyCollection props =
                    [
                        //props.Add(GenerateExpressionGUID());
                        new NameProperty(sourceScal.GetProp<NameProperty>("nName").Value, "ParameterName"),
                        new FloatProperty(sourceScal.GetProp<FloatProperty>("sValue").Value, "ParameterValue"),
                    ];
                    targetScalars.Add(new StructProperty("ScalarParameterValue", props));
                }
            }

            if (sourceTextures != null) { targetExport.WriteProperty(targetTextures); }
            if (sourceVectors != null) { targetExport.WriteProperty(targetVectors); }
            if (sourceScalars != null) { targetExport.WriteProperty(targetScalars); }
        }

        public static void ReplaceMeshDataFromPsk(PackageEditorWindow pew)
        {
            if (GetSelectedMeshBinary(pew, out var meshExport, out var meshBin))
            {
                var d = new OpenFileDialog
                {
                    Filter = "PSK|*.psk",
                    Title = "Select a psk file"
                };
                if (d.ShowDialog() == true)
                {
                    var psk = PSK.FromFile(d.FileName);

                    if (psk.Points.Count != meshBin.LODModels[0].NumVertices)
                    {
                        ShowError("the number of vertices in the mesh (LOD 0) and the psk must match.");
                        return;
                    }

                    if (psk.Points.Count != psk.Wedges.Count)
                    {
                        ShowError("Can't import this psk; number of points and wedges differ.");
                        return;
                    }

                    for (int i = 0; i < psk.Points.Count; i++)
                    {
                        // replace the position data with that of the psk
                        meshBin.LODModels[0].VertexBufferGPUSkin.VertexData[i].Position = psk.Points[i];
                        // gotta invert that Y for whatever reason, like it got inverted on export
                        meshBin.LODModels[0].VertexBufferGPUSkin.VertexData[i].Position.Y *= -1;
                        // TODO also replace the UVs and material slot in the future?
                    }

                    meshExport.WriteBinary(meshBin);
                }
            }
        }

        public static void ReplaceBMFDataFromPsk(PackageEditorWindow pew)
        {
            if (GetSelectedItem(pew, "BioMorphFace", out var bmfExport))
            {
                var d = new OpenFileDialog
                {
                    Filter = "PSK|*.psk",
                    Title = "Select a psk file"
                };
                if (d.ShowDialog() == true)
                {
                    var psk = PSK.FromFile(d.FileName);

                    var bmfBin = bmfExport.GetBinaryData<BioMorphFace>();

                    Vector3[] vertexPos = new Vector3[psk.Points.Count];

                    for (int i = 0; i < psk.Points.Count; i++)
                    {
                        vertexPos[i] = psk.Points[i] with { Y = -psk.Points[i].Y };
                    }

                    bmfBin.LODs = [[.. vertexPos]];

                    bmfExport.WriteBinary(bmfBin);
                }
            }
            else
            {
                ShowError("you must select a BioMorphFace to use this experiment");
                return;
            }
        }

        public static void MeshToPsk(PackageEditorWindow pew)
        {
            if (!GetSelectedItem(pew, "SkeletalMesh", out var skelMeshExport))
            {
                ShowError("You must select a SkeletalMesh to use this experiment");
                return;
            }
            var d = new SaveFileDialog { Filter = "PSK|*.psk" };
            if (d.ShowDialog() == true)
            {
                PSK.CreateFromSkeletalMesh(skelMeshExport.GetBinaryData<SkeletalMesh>()).ToFile(d.FileName);
            }
        }

        public static void ImportRonToBmf(PackageEditorWindow pew)
        {
            var d = new OpenFileDialog
            {
                Filter = "RON|*.ron",
                Title = "Select a ron headmorph file"
            };
            if (d.ShowDialog() == true)
            {
                // create a new BioMorphFace
                var bmf = ExportCreator.CreateExport(pew.Pcc, Path.GetFileNameWithoutExtension(d.FileName), "BioMorphFace");

                if (GetSelectedItem(pew, "SkeletalMesh", out var baseHead))
                {
                    // if they have selected the base head when they run this, set that as the base head
                    bmf.WriteProperty(new ObjectProperty(baseHead, "m_oBaseHead"));

                    // TODO selector dialog instead?
                }

                var headMorph = HeadMorph.FromRonFile(d.FileName);

                // add a reference to the hair mesh, if it is in this file
                if (headMorph.HairMesh != null && headMorph.HairMesh != "None")
                {
                    var hairMeshEntry = pew.Pcc.FindEntry(headMorph.HairMesh);
                    if (hairMeshEntry != null)
                    {
                        bmf.WriteProperty(new ObjectProperty(hairMeshEntry, "m_oHairMesh"));
                    }
                }

                // create the BioMaterialOverride object and property
                var matOverrideEntry = ExportCreator.CreateExport(pew.Pcc, "BioMaterialOverride", "BioMaterialOverride", bmf);
                bmf.WriteProperty(new ObjectProperty(matOverrideEntry, "m_oMaterialOverrides"));

                // write the scalars into the material overrides
                var scalarParams = headMorph.ScalarParameters.Select(x => new StructProperty("ScalarParameter", false, new NameProperty(x.Key, "nName"), new FloatProperty(x.Value, "sValue")));
                matOverrideEntry.WriteProperty(new ArrayProperty<StructProperty>(scalarParams, "m_aScalarOverrides"));

                // write the scalars into the material overrides
                var vectorParams = headMorph.VectorParameters.Select(x => new StructProperty("ColorParameter", false, new NameProperty(x.Key, "nName"), LinearColorToStructProperty(x.Value, "cValue")));
                matOverrideEntry.WriteProperty(new ArrayProperty<StructProperty>(vectorParams, "m_aColorOverrides"));

                // write the textures into the material overrides
                List<StructProperty> textureParams = [];
                foreach (var tex in headMorph.TextureParameters)
                {
                    // if the referenced texture is in this file, add it to the material overrides
                    var textureEntry = pew.Pcc.FindEntry(tex.Value, "Texture2D");
                    if (textureEntry != null)
                    {
                        textureParams.Add(new StructProperty("TextureParameter", [new NameProperty(tex.Key, "nName"), new ObjectProperty(textureEntry, "m_pTexture")]));
                    }
                }
                matOverrideEntry.WriteProperty(new ArrayProperty<StructProperty>(textureParams, "m_aTextureOverrides"));

                // add the accessories if they are in this file
                List<ObjectProperty> otherMeshes = [];
                foreach (var accessory in headMorph.AccessoryMeshes)
                {
                    var meshEntry = pew.Pcc.FindEntry(accessory, "SkeletalMesh");
                    if (meshEntry != null)
                    {
                        otherMeshes.Add(new ObjectProperty(meshEntry));
                    }
                }
                if (otherMeshes.Any())
                {
                    bmf.WriteProperty(new ArrayProperty<ObjectProperty>(otherMeshes, "m_oOtherMeshes"));
                }

                // morph features
                var morphFeatures = headMorph.MorphFeatures.Select(x => new StructProperty("MorphFeature", false, new NameProperty(x.Key, "sFeatureName"), new FloatProperty(x.Value, "Offset")));
                bmf.WriteProperty(new ArrayProperty<StructProperty>(morphFeatures, "m_aMorphFeatures"));

                // bone offsets
                var boneOffsets = headMorph.OffsetBones.Select(x => new StructProperty("OffsetBonePos", false, new NameProperty(x.Key, "nName"), Vector3ToStructProperty(x.Value, "vPos")));
                bmf.WriteProperty(new ArrayProperty<StructProperty>(boneOffsets, "m_aFinalSkeleton"));

                // vertices
                var bmfBin = new BioMorphFace();
                List<Vector3[]> LODs = [];
                if (headMorph.Lod0Vertices != null && headMorph.Lod0Vertices.Any())
                {
                    LODs.Add([.. headMorph.Lod0Vertices]);
                }
                if (headMorph.Lod1Vertices != null && headMorph.Lod1Vertices.Any())
                {
                    LODs.Add([.. headMorph.Lod1Vertices]);
                }
                if (headMorph.Lod2Vertices != null && headMorph.Lod2Vertices.Any())
                {
                    LODs.Add([.. headMorph.Lod2Vertices]);
                }
                bmfBin.LODs = [.. LODs];

                bmf.WriteBinary(bmfBin);
            }
        }

        private static StructProperty LinearColorToStructProperty(LinearColor color, NameReference? name = null)
        {
            return new StructProperty("LinearColor",
                [
                    new FloatProperty(color.R, "R"),
                    new FloatProperty(color.G, "G"),
                    new FloatProperty(color.B, "B"),
                    new FloatProperty(color.A, "A")
                ], name, true);
        }

        private static StructProperty Vector3ToStructProperty(Vector3 vect, NameReference? name = null)
        {
            return new StructProperty("Vector",
                [
                    new FloatProperty(vect.X, "X"),
                    new FloatProperty(vect.Y, "Y"),
                    new FloatProperty(vect.Z, "Z"),
                ], name, true);
        }

        private static Vector3 StructPropertyToVector3(StructProperty prop)
        {
            return new Vector3(
                prop.GetProp<FloatProperty>("X").Value,
                prop.GetProp<FloatProperty>("Y").Value,
                prop.GetProp<FloatProperty>("Z").Value);
        }

        private static LinearColor StructPropertyToLinearColor(StructProperty prop)
        {
            return new LinearColor(
                prop.GetProp<FloatProperty>("R").Value,
                prop.GetProp<FloatProperty>("G").Value,
                prop.GetProp<FloatProperty>("B").Value,
                prop.GetProp<FloatProperty>("A").Value);
        }

        public static void UpdateRonFromPsk(PackageEditorWindow pew)
        {
            // select a ron file to update
            var ronDialog = new OpenFileDialog
            {
                Filter = "RON|*.ron",
                Title = "Select a ron file"
            };
            if (ronDialog.ShowDialog() == true)
            {
                var pskDialog = new OpenFileDialog
                {
                    Filter = "PSK|*.psk",
                    Title = "Select a psk file"
                };
                if (pskDialog.ShowDialog() == true)
                {
                    var headMorph = HeadMorph.FromRonFile(ronDialog.FileName);
                    var psk = PSK.FromFile(pskDialog.FileName);

                    headMorph.Lod0Vertices = psk.Points;

                    headMorph.ToRonFile(ronDialog.FileName);
                }
            }
        }

        public static void ExportBmfToRon(PackageEditorWindow pew)
        {
            if (!GetSelectedItem(pew, "BioMorphFace", out var bmf))
            {
                ShowError("you must select a BioMorphFace export for this experiment");
                return;
            }

            var headMorph = new HeadMorph()
            {
                AccessoryMeshes = [],
                Lod0Vertices = [],
                Lod1Vertices = [],
                Lod2Vertices = [],
                Lod3Vertices = [],
                MorphFeatures = [],
                OffsetBones = [],
                ScalarParameters = [],
                TextureParameters = [],
                VectorParameters = []
            };

            var props = bmf.GetProperties();

            // morph features
            var morphs = props.GetProp<ArrayProperty<StructProperty>>("m_aMorphFeatures");
            foreach (var morph in morphs)
            {
                headMorph.MorphFeatures.Add(morph.GetProp<NameProperty>("sFeatureName").Value, morph.GetProp<FloatProperty>("Offset").Value);
            }

            // final skeleton
            var finalSkeleton = props.GetProp<ArrayProperty<StructProperty>>("m_aFinalSkeleton");
            foreach (var offsetBone in finalSkeleton)
            {
                headMorph.OffsetBones.Add(offsetBone.GetProp<NameProperty>("nName").Value, StructPropertyToVector3(offsetBone.GetProp<StructProperty>("vPos")));
            }

            // other meshes
            var otherMeshes = props.GetProp<ArrayProperty<ObjectProperty>>("m_oOtherMeshes");
            if (otherMeshes != null)
            {
                foreach (var otherMesh in otherMeshes)
                {
                    var entry = pew.Pcc.GetEntry(otherMesh.Value);
                    if (entry != null)
                    {
                        headMorph.AccessoryMeshes.Add(entry.MemoryFullPath);
                    }
                }
            }

            // LODs
            var bmfBin = bmf.GetBinaryData<BioMorphFace>();
            if (bmfBin.LODs.Length > 0)
            {
                headMorph.Lod0Vertices = [.. bmfBin.LODs[0]];
            }
            if (bmfBin.LODs.Length > 1)
            {
                headMorph.Lod1Vertices = [.. bmfBin.LODs[1]];
            }
            if (bmfBin.LODs.Length > 2)
            {
                headMorph.Lod2Vertices = [.. bmfBin.LODs[2]];
            }
            if (bmfBin.LODs.Length > 3)
            {
                headMorph.Lod3Vertices = [.. bmfBin.LODs[3]];
            }

            var materialsProp = props.GetProp<ObjectProperty>("m_oMaterialOverrides");
            if (materialsProp != null)
            {
                var matOverrides = (ExportEntry)pew.Pcc.GetEntry(materialsProp.Value);

                if (matOverrides != null)
                {
                    var matProps = matOverrides.GetProperties();

                    // textures
                    var textureProps = matProps.GetProp<ArrayProperty<StructProperty>>("m_aTextureOverrides");
                    foreach (var tex in textureProps)
                    {
                        headMorph.TextureParameters.Add(
                            tex.GetProp<NameProperty>("nName").Value,
                            pew.Pcc.GetEntry(tex.GetProp<ObjectProperty>("m_pTexture").Value).MemoryFullPath);
                    }

                    // vectors
                    var vectorProps = matProps.GetProp<ArrayProperty<StructProperty>>("m_aColorOverrides");
                    foreach (var vector in vectorProps)
                    {
                        headMorph.VectorParameters.Add(
                            vector.GetProp<NameProperty>("nName").Value,
                            StructPropertyToLinearColor(vector.GetProp<StructProperty>("cValue")));
                    }

                    // scalars
                    var scalarProps = matProps.GetProp<ArrayProperty<StructProperty>>("m_aScalarOverrides");
                    foreach (var scalar in scalarProps)
                    {
                        headMorph.ScalarParameters.Add(
                            scalar.GetProp<NameProperty>("nName").Value,
                            scalar.GetProp<FloatProperty>("sValue").Value);
                    }
                }
            }

            var d = new SaveFileDialog { Filter = "RON|*.ron" };
            if (d.ShowDialog() == true)
            {
                headMorph.ToRonFile(d.FileName);
            }
        }

        public static void ImportPskAsMorphTarget(PackageEditorWindow pew)
        {
            if (!GetSelectedItem(pew, "MorphTargetSet", out var morphTargetSet))
            {
                ShowError("you must select a morphTargetSet export for this experiment");
                return;
            }

            var baseMesh = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(morphTargetSet.GetProperty<ObjectProperty>("BaseSkelMesh").Value), new PackageCache());

            if (baseMesh == null || baseMesh.ClassName != "SkeletalMesh")
            {
                ShowError("selected MorphTargetSet must have a base mesh");
                return;
            }

            var baseMeshBinary = baseMesh.GetBinaryData<SkeletalMesh>();

            var d = new OpenFileDialog
            {
                Filter = "PSK|*.psk",
                Title = "Select a psk file"
            };
            if (d.ShowDialog() == true)
            {
                var psk = PSK.FromFile(d.FileName);

                if (psk.Points.Count != baseMeshBinary.LODModels[0].NumVertices)
                {
                    ShowError("the number of vertices in the base mesh (LOD 0) and the psk must match.");
                    return;
                }

                if (psk.Points.Count != psk.Wedges.Count)
                {
                    ShowError("Can't use this psk; number of points and wedges differ.");
                    return;
                }

                // now make a new MorphTarget with the name of the psk
                var newMorphTarget = ExportCreator.CreateExport(pew.Pcc, Path.GetFileNameWithoutExtension(d.FileName), "MorphTarget", morphTargetSet, indexed: false);
                var newMorphTargetBin = new MorphTarget
                {
                    MorphLODModels = [new MorphTarget.MorphLODModel()]
                };
                newMorphTargetBin.MorphLODModels[0].NumBaseMeshVerts = psk.Points.Count;

                List<MorphTarget.MorphVertex> vertDeltas = [];

                for (int i = 0; i < psk.Points.Count; i++)
                {
                    // gotta flip the y part of the position
                    psk.Points[i] = new Vector3(psk.Points[i].X, psk.Points[i].Y * -1, psk.Points[i].Z);

                    if (!ApproximatelyEqual(baseMeshBinary.LODModels[0].VertexBufferGPUSkin.VertexData[i].Position, psk.Points[i]))
                    {
                        vertDeltas.Add(new MorphTarget.MorphVertex()
                        {
                            SourceIdx = (ushort)i,
                            PositionDelta = psk.Points[i] - baseMeshBinary.LODModels[0].VertexBufferGPUSkin.VertexData[i].Position
                            // TODO anything with the tangent deltas?
                            // TODO anything with the bone offsets?
                        });
                    }
                }

                newMorphTargetBin.MorphLODModels[0].Vertices = [.. vertDeltas];

                newMorphTarget.WriteBinary(newMorphTargetBin);
                var targets = morphTargetSet.GetProperty<ArrayProperty<ObjectProperty>>("Targets");
                targets.Add(new ObjectProperty(newMorphTarget.UIndex));
                morphTargetSet.WriteProperty(targets);
            }
        }

        private static bool ApproximatelyEqual(Vector3 first, Vector3 second)
        {
            var acceptabledelta = 0.01;
            if (Math.Abs(first.X - second.X) < acceptabledelta
                && Math.Abs(first.Y - second.Y) < acceptabledelta
                && Math.Abs(first.Z - second.Z) < acceptabledelta)
            {
                return true;
            }
            return false;
        }

        public static void ExportMorphTargetSet(PackageEditorWindow pew)
        {
            if (!GetSelectedItem(pew, "MorphTargetSet", out var morphTargetSet))
            {
                ShowError("you must select a morphTargetSet export for this experiment");
                return;
            }

            var baseMesh = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(morphTargetSet.GetProperty<ObjectProperty>("BaseSkelMesh").Value), new PackageCache());

            if (baseMesh == null || baseMesh.ClassName != "SkeletalMesh")
            {
                ShowError("selected MorphTargetSet must have a base mesh");
                return;
            }

            var baseMeshBin = baseMesh.GetBinaryData<SkeletalMesh>();
            var targets = morphTargetSet.GetProperty<ArrayProperty<ObjectProperty>>("Targets");

            var folderDialog = new OpenFolderDialog()
            {
                Multiselect = false,
                Title = "Choose a folder for the output"
            };

            if (folderDialog.ShowDialog() == true)
            {
                var folder = folderDialog.FolderName;

                // output the special psk into a file with the name of the base head
                // make most of the psk from the base skeletal mesh
                var psk = PSK.CreateFromSkeletalMesh(baseMeshBin);

                foreach (var target in targets)
                {
                    var targetExport = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(target.Value), new PackageCache());
                    var targetBin = targetExport.GetBinaryData<MorphTarget>();
                    psk.Morphs.Add(new PSK.MorphInfo
                    {
                        Name = targetExport.ObjectNameString,
                        VertexCount = targetBin.MorphLODModels[0].Vertices.Length
                    });

                    foreach (var vertex in targetBin.MorphLODModels[0].Vertices)
                    {
                        psk.MorphData.Add(new PSK.MorphDelta
                        {
                            PointIndex = vertex.SourceIdx,
                            PositionDelta = vertex.PositionDelta,
                            // this gets ignored on import to Blender anyway
                            //TangentZDelta = vertex.TangentZDelta
                        });
                    }
                }

                psk.ToFile(Path.Combine(folder, morphTargetSet.ObjectName + ".psk"));

                // now, output the psa file and config file
                var config = new StringBuilder();
                config.AppendLine("[RemoveTracks]");
                var psa = new PSA
                {
                    Bones = [],
                    Infos = [],
                    Keys = []
                };

                foreach (var bone in baseMeshBin.RefSkeleton)
                {
                    psa.Bones.Add(new PSABone
                    {
                        Name = bone.Name,
                        ParentIndex = bone.ParentIndex,
                    });
                }

                var frameNum = 0;
                foreach (var target in targets)
                {
                    var targetExport = SharedMethods.ResolveEntryToExport(pew.Pcc.GetEntry(target.Value), new PackageCache());
                    var targetBin = targetExport.GetBinaryData<MorphTarget>();

                    if (targetBin.BoneOffsets.Length == 0)
                    {
                        continue;
                    }

                    psa.Infos.Add(new PSAAnimInfo
                    {
                        Name = targetExport.ObjectNameString,
                        Group = "None",
                        TotalBones = baseMeshBin.RefSkeleton.Length,
                        KeyQuotum = baseMeshBin.RefSkeleton.Length, // this would be multiplied by the number of frames, but there is just one frame
                        TrackTime = 1,
                        AnimRate = 1,
                        FirstRawFrame = frameNum,
                        NumRawFrames = 1
                    });
                    frameNum += 1;

                    for (int i = 0; i < baseMeshBin.RefSkeleton.Length; i++)
                    {
                        var refBone = baseMeshBin.RefSkeleton[i];
                        // does this bone get influenced by this morph target?
                        var influence = targetBin.BoneOffsets.FirstOrDefault(x => x.Bone == refBone.Name);
                        //var rotQuat = new Quaternion(refBone.Orientation.X, refBone.Orientation.Y, refBone.Orientation.Z, refBone.Orientation.W);
                        var rotQuat = new Quaternion(0, 0, 0, 1);
                        //var posVec = refBone.Position with { Y = refBone.Position.Y * -1 };
                        var posVec = new Vector3(0, 0, 0);
                        if (influence != null)
                        {
                            posVec = new Vector3(refBone.Position.X + influence.Offset.X, -refBone.Position.Y - influence.Offset.Y, refBone.Position.Z + influence.Offset.Z);
                            // do not output rotation when you import this one
                            config.AppendLine($"{targetExport.ObjectName}.{i}=rot");
                        }
                        else
                        {
                            // do not output anything when you import this one
                            config.AppendLine($"{targetExport.ObjectName}.{i}=all");
                        }

                        psa.Keys.Add(new PSAAnimKeys
                        {
                            Position = posVec,
                            Rotation = rotQuat,
                            Time = 30
                        });
                    }
                }

                psa.ToFile(Path.Combine(folder, morphTargetSet.ObjectName + ".psa"));

                // also output a config file next to this to tell it to skip rotations for every sequence and every bone
                File.WriteAllText(Path.Combine(folder, morphTargetSet.ObjectName + ".config"), config.ToString());
            }

            //MessageBox.Show("Done.");
        }

        private static bool GetSelectedItem(PackageEditorWindow pew, string expectedType, out ExportEntry entry)
        {
            entry = null;
            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return false; }

            if (pew.SelectedItem.Entry.ClassName != expectedType)
            {
                return false;
            }

            entry = (ExportEntry)pew.SelectedItem.Entry;

            return entry != null;
        }
    }
}
