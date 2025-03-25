using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal
{
    //WIP. Not sure if it produces correct output
    public class PSK
    {
        public List<Vector3> Points;
        public List<PSKWedge> Wedges;
        public List<PSKTriangle> Faces;
        public List<PSKMaterial> Materials;
        public List<PSA.PSABone> Bones;
        public List<PSKWeight> Weights;

        private const int version = 1999801;

        protected void Serialize(SerializingContainer sc)
        {
            var mainHeader = new PSA.ChunkHeader
            {
                ChunkID = "ACTRHEAD",
                Version = version,
            };
            sc.Serialize(ref mainHeader);

            var pointsHeader = new PSA.ChunkHeader
            {
                ChunkID = "PNTS0000",
                Version = version,
                DataSize = 0xC,
                DataCount = Points?.Count ?? 0
            };
            sc.Serialize(ref pointsHeader);
            sc.Serialize(ref Points, pointsHeader.DataCount, sc.Serialize);
            var wedgesHeader = new PSA.ChunkHeader
            {
                ChunkID = "VTXW0000",
                Version = version,
                DataSize = 0x10,
                DataCount = Wedges?.Count ?? 0
            };
            sc.Serialize(ref wedgesHeader);
            sc.Serialize(ref Wedges, wedgesHeader.DataCount, sc.Serialize);
            var facesHeader = new PSA.ChunkHeader
            {
                ChunkID = "FACE0000",
                Version = version,
                DataSize = 0xC,
                DataCount = Faces?.Count ?? 0
            };
            sc.Serialize(ref facesHeader);
            sc.Serialize(ref Faces, facesHeader.DataCount, sc.Serialize);
            var matsHeader = new PSA.ChunkHeader
            {
                ChunkID = "MATT0000",
                Version = version,
                DataSize = 0x58,
                DataCount = Materials?.Count ?? 0
            };
            sc.Serialize(ref matsHeader);
            sc.Serialize(ref Materials, matsHeader.DataCount, sc.Serialize);
            var bonesHeader = new PSA.ChunkHeader
            {
                ChunkID = "REFSKELT",
                Version = version,
                DataSize = 0x78,
                DataCount = Bones?.Count ?? 0
            };
            sc.Serialize(ref bonesHeader);
            sc.Serialize(ref Bones, bonesHeader.DataCount, sc.Serialize);
            var weightsHeader = new PSA.ChunkHeader
            {
                ChunkID = "RAWWEIGHTS",
                Version = version,
                DataSize = 0xC,
                DataCount = Weights?.Count ?? 0
            };
            sc.Serialize(ref weightsHeader);
            sc.Serialize(ref Weights, weightsHeader.DataCount, sc.Serialize);
        }

        public void ToFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            Serialize(new SerializingContainer(fs, null));
        }

        public static PSK FromFile(string filePath)
        {
            var psk = new PSK();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            psk.Serialize(new SerializingContainer(fs, null, true));
            return psk;
        }

        public static PSK CreateFromSkeletalMesh(SkeletalMesh skelMesh, int lodIdx = 0)
        {
            var lod = skelMesh.LODModels[lodIdx];

            int numVertices = (int)lod.NumVertices;
            var psk = new PSK
            {
                Points = new List<Vector3>(numVertices),
                Wedges = [],
                Faces = [],
                Materials = [],
                Bones = [],
                Weights = []
            };
            int numTriangles = 0;
            var matIndices = new byte[numVertices];
            foreach (SkelMeshSection section in lod.Sections)
            {
                numTriangles += section.NumTriangles;
                for (uint t = 0; t < section.NumTriangles; t++)
                {
                    uint baseIndex = section.BaseIndex;
                    int i1 = lod.IndexBuffer[baseIndex + t * 3];
                    int i2 = lod.IndexBuffer[baseIndex + t * 3 + 1];
                    int i3 = lod.IndexBuffer[baseIndex + t * 3 + 2];
                    byte materialIndex = (byte)section.MaterialIndex;
                    matIndices[i1] = materialIndex;
                    matIndices[i2] = materialIndex;
                    matIndices[i3] = materialIndex;
                    psk.Faces.Add(new PSKTriangle
                    {
                        //intentionally flipped
                        WedgeIdx1 = (ushort)i1,
                        WedgeIdx0 = (ushort)i2,
                        WedgeIdx2 = (ushort)i3,
                        MatIndex = materialIndex
                    });
                }
            }

            foreach (int uIndex in skelMesh.Materials)
            {
                psk.Materials.Add(new PSKMaterial
                {
                    Name = skelMesh.Export.FileRef.GetEntry(uIndex)?.ObjectName.Instanced ?? ""
                });
            }

            const float weightUnpackScale = 1f / 255;
            if (lod.ME1VertexBufferGPUSkin != null)
            {
                for (int i = 0; i < lod.ME1VertexBufferGPUSkin.Length; i++)
                {
                    SoftSkinVertex vertex = lod.ME1VertexBufferGPUSkin[i];
                    psk.Points.Add(new Vector3(vertex.Position.X, vertex.Position.Y * -1, vertex.Position.Z * -1));
                    psk.Wedges.Add(new PSKWedge
                    {
                        MatIndex = matIndices[i],
                        PointIndex = (ushort)i,
                        U = vertex.UV.X,
                        V = vertex.UV.Y
                    });
                    for (int j = 0; j < 4; j++)
                    {
                        if (vertex.InfluenceBones[j] == 0)
                        {
                            break;
                        }

                        psk.Weights.Add(new PSKWeight
                        {
                            Bone = vertex.InfluenceBones[j],
                            Weight = vertex.InfluenceWeights[j] * weightUnpackScale,
                            Point = i
                        });
                    }
                }
            }
            else
            {
                for (int i = 0; i < lod.VertexBufferGPUSkin.VertexData.Length; i++)
                {
                    GPUSkinVertex vertex = lod.VertexBufferGPUSkin.VertexData[i];
                    psk.Points.Add(new Vector3(vertex.Position.X, vertex.Position.Y * -1, vertex.Position.Z));
                    psk.Wedges.Add(new PSKWedge
                    {
                        MatIndex = matIndices[i],
                        PointIndex = (ushort)i,
                        U = vertex.UV.X,
                        V = vertex.UV.Y
                    });
                    for (int j = 0; j < 4; j++)
                    {
                        if (vertex.InfluenceWeights[j] == 0)
                        {
                            break;
                        }

                        // first, we need to find the chunk containing this vertex:
                        var chunk = lod.Chunks.Last(x => x.BaseVertexIndex <= i);


                        psk.Weights.Add(new PSKWeight
                        {
                            Bone = chunk.BoneMap[vertex.InfluenceBones[j]],
                            Weight = vertex.InfluenceWeights[j] * weightUnpackScale,
                            Point = i
                        });
                    }
                }
            }
            foreach (MeshBone meshBone in skelMesh.RefSkeleton)
            {
                psk.Bones.Add(new PSA.PSABone
                {
                    Name = meshBone.Name.Instanced,
                    Flags = meshBone.Flags,
                    ParentIndex = meshBone.ParentIndex,
                    NumChildren = meshBone.NumChildren,
                    Position = new Vector3(meshBone.Position.X, meshBone.Position.Y * -1, meshBone.Position.Z),
                    Rotation = new Quaternion(meshBone.Orientation.X, meshBone.Orientation.Y * -1, meshBone.Orientation.Z, meshBone.Orientation.W)
                });
            }

            return psk;
        }

        public class PSKWedge
        {
            public ushort PointIndex;
            public float U;
            public float V;
            public byte MatIndex;
        }

        public class PSKTriangle
        {
            public ushort WedgeIdx0;
            public ushort WedgeIdx1;
            public ushort WedgeIdx2;
            public byte MatIndex;
        }

        public struct PSKMaterial
        {
            public string Name;
            public int Texture;
            public int polyflags;
            public int auxmaterial;
            public int auxflags;
            public int LODbias;
            public int LODstyle;
        }
        public class PSKWeight
        {
            public float Weight;
            public int Point;
            public int Bone;
        }
    }
}

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class SerializingContainer
    {
        public void Serialize(ref PSK.PSKWedge wedge)
        {
            if (IsLoading)
            {
                wedge = new PSK.PSKWedge();
            }
            Serialize(ref wedge.PointIndex);
            SerializeConstShort(0); //Padding
            Serialize(ref wedge.U);
            Serialize(ref wedge.V);
            Serialize(ref wedge.MatIndex);
            SerializeConstByte(0); //Reserved
            SerializeConstShort(0); //Padding
        }
        public void Serialize(ref PSK.PSKTriangle tri)
        {
            if (IsLoading)
            {
                tri = new PSK.PSKTriangle();
            }
            Serialize(ref tri.WedgeIdx0);
            Serialize(ref tri.WedgeIdx1);
            Serialize(ref tri.WedgeIdx2);
            Serialize(ref tri.MatIndex);
            SerializeConstByte(0);
            SerializeConstInt(0);
        }
        public void Serialize(ref PSK.PSKMaterial mat)
        {
            if (IsLoading)
            {
                mat = new PSK.PSKMaterial();
            }
            SerializeFixedSizeString(ref mat.Name, 64);
            Serialize(ref mat.Texture);
            Serialize(ref mat.polyflags);
            Serialize(ref mat.auxmaterial);
            Serialize(ref mat.auxflags);
            Serialize(ref mat.LODbias);
            Serialize(ref mat.LODstyle);
        }
        public void Serialize(ref PSK.PSKWeight w)
        {
            if (IsLoading)
            {
                w = new PSK.PSKWeight();
            }
            Serialize(ref w.Weight);
            Serialize(ref w.Point);
            Serialize(ref w.Bone);
        }
    }
}
