﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Playtime_Painter {


    [Serializable]
    public class MeshConstructor {

        public List<int>[] tris;
        public uint[] baseVertex;

        Vector4[] perVertexTrisTexture;
        Vector2[] uvs;
        Vector2[] uvs1;

        Vector3[] position;
        Vector3[] normals;
        Vector3[] sharpNormals;

        BoneWeight[] boneWeights;
        Matrix4x4[] bindPoses;

        public Vector4[] Tangents;

        Vector3[] edgeNormal0;
        Vector3[] edgeNormal1;
        Vector3[] edgeNormal2;

        Color[] colors;
        Vector4[] edgeData;
        Vector3[] edgeWeightedOnly;
        Vector4[] shadowBake;
        Countless<vertexAnimationFrame> anims; // outer tree - animation no, inner - vertices
        int[] originalIndex;

        public MeshPackagingProfile profile;

        public EditableMesh edMesh;

        public Mesh mesh;

        public int vertsCount;

        public Color[] _colors {
            get {
                if (colors == null) {
                    colors = new Color[vertsCount];
                    foreach (var vp in edMesh.vertices)
                        foreach (var uvi in vp.uvpoints)
                            colors[uvi] = uvi._color;
                }
                return colors;
            }
        }

        public Vector4[] _shadowBake {
            get {
                if (shadowBake == null) {
                    shadowBake = new Vector4[vertsCount];
                    foreach (var vp in edMesh.vertices)
                        foreach (var uvi in vp.uvpoints)
                            shadowBake[uvi] = vp.shadowBake;
                }
                return shadowBake;
            }
        }

        public Vector2[] _uv { get {
                if (uvs == null) {
                    uvs = new Vector2[vertsCount];
                    foreach (var vp in edMesh.vertices)
                        foreach (var uvi in vp.uvpoints)
                            uvs[uvi] = uvi.GetUV(0);
                }
                return uvs;
            }
        }

        public Vector2[] _uv1 {
            get {
                if (uvs1 == null) {
                    uvs1 = new Vector2[vertsCount];
                    foreach (var vp in edMesh.vertices)
                        foreach (var uvi in vp.uvpoints)
                            uvs1[uvi] = uvi.GetUV(1);
                }
                return uvs1;
            }
        }

        public Vector4[] _trisTextures {
            get {
                if (perVertexTrisTexture == null) {

                    perVertexTrisTexture = new Vector4[vertsCount];

                    foreach (var tri in edMesh.triangles) {
                        for (int no = 0; no < 3; no++)
                            perVertexTrisTexture[tri.uvpnts[no]] = tri.textureNo;

                    }
                }

                return perVertexTrisTexture;
            }
        }

        public Vector4[] _edgeData {
            get {
                if (edgeData == null) {
                    edgeData = new Vector4[vertsCount];

                    foreach (var tri in edMesh.triangles) {
                        for (int no = 0; no < 3; no++) {
                            UVpoint up = tri.uvpnts[no];
                            float edge = (up.tris.Count == 1 //up.vert.SmoothNormal
                                ) ? 1 : 0;
                            edgeData[up] = new Vector4(no == 0 ? 0 : edge, no == 1 ? 0 : edge, no == 2 ? 0 : edge, up.vert.edgeStrength);
                        }
                    }
                }
                return edgeData;
            }
        }

        public Vector3[] _edgeDataByWeight
        {
            get
            {

                // Set edge on triangle side. 


                if (edgeWeightedOnly == null)
                {
                    edgeWeightedOnly = new Vector3[vertsCount];

                    foreach (var tri in edMesh.triangles)
                        for (int no = 0; no < 3; no++)
                        {
                            UVpoint up = tri.uvpnts[no];

                            // If other triangles of the point 

                            // A weight for line 1-2 is in position 3, for 2-3 in 1 and so on.
                            var ew = tri.edgeWeight;


                            var weight = new Vector3(
                                      no == 0 ? 0 : ew[0]
                                    , no == 1 ? 0 : ew[1]
                                    , no == 2 ? 0 : ew[2]
                                    );

                            if (weight.magnitude < 0.3f)
                                weight[(no + 1) % 3] = up.vert.edgeStrength;

                            edgeWeightedOnly[up] = weight;

                        }

                }
                return edgeWeightedOnly;
            }
        }

        public Vector3[] _normals {
            get { if (normals == null) GenerateNormals(); return normals; } }

        public Vector3[] _sharpNormals { get { if (sharpNormals == null) GenerateNormals(); return sharpNormals; } }

        public Vector3[] _edgeNormal_0_OrSharp {
            get {
                if (edgeNormal0 == null) {
                    edgeNormal0 = new Vector3[vertsCount];

                    var sn = _sharpNormals;

                    foreach (var tri in edMesh.triangles)
                    {
                        UVpoint up = tri.uvpnts[0];
                        edgeNormal0[up] = sn[up];

                        UVpoint up1 = tri.uvpnts[1];
                        UVpoint up2 = tri.uvpnts[2];

                        var tris = up1.vert.getTrianglesFromLine(up2.vert);

                        var nrm = tris.SmoothVector();

                        edgeNormal0[up1] = nrm;
                        edgeNormal0[up2] = nrm;
                    }
                }
                return edgeNormal0;
            }
        }

        public Vector3[] _edgeNormal_1_OrSharp {
            get {
                if (edgeNormal1 == null)
                {
                    edgeNormal1 = new Vector3[vertsCount];

                    var sn = _sharpNormals;

                    foreach (var tri in edMesh.triangles)
                    {
                        UVpoint up = tri.uvpnts[1];
                        edgeNormal1[up] = sn[up];

                        UVpoint up0 = tri.uvpnts[0];
                        UVpoint up2 = tri.uvpnts[2];

                        var tris = up0.vert.getTrianglesFromLine(up2.vert);

                        var nrm = tris.SmoothVector();

                        edgeNormal1[up0] = nrm;
                        edgeNormal1[up2] = nrm;
                    }
                }
                return edgeNormal1;
            }
        }

        public Vector3[] _edgeNormal_2_OrSharp {
            get
            {
                if (edgeNormal2 == null) {
                    edgeNormal2 = new Vector3[vertsCount];

                    var sn = _sharpNormals;

                    foreach (var tri in edMesh.triangles) {
                        UVpoint up = tri.uvpnts[2];
                        edgeNormal2[up] = sn[up];

                        UVpoint up0 = tri.uvpnts[0];
                        UVpoint up1 = tri.uvpnts[1];

                        var tris = up0.vert.getTrianglesFromLine(up1.vert);

                        var nrm = tris.SmoothVector();

                        edgeNormal2[up0] = nrm;
                        edgeNormal2[up1] = nrm;
                    }
                }
                return edgeNormal2;
            }
        }

        public Vector4[] _tangents
        {
            get
            {

                if (Tangents == null)
                {

                    Tangents = new Vector4[vertsCount];
                    Vector3[] tan1 = new Vector3[vertsCount];
                    Vector3[] tan2 = new Vector3[vertsCount];

                    int tri = 0;

                    foreach (var t in edMesh.triangles)
                    {

                        var i1 = t.uvpnts[0];
                        var i2 = t.uvpnts[1];
                        var i3 = t.uvpnts[2];

                        Vector3 v1 = t.uvpnts[0].pos;
                        Vector3 v2 = t.uvpnts[1].pos;
                        Vector3 v3 = t.uvpnts[2].pos;

                        Vector2 w1 = t.uvpnts[0].GetUV(0);// texcoords[i1];
                        Vector2 w2 = t.uvpnts[1].GetUV(0);
                        Vector2 w3 = t.uvpnts[2].GetUV(0);

                        float x1 = v2.x - v1.x;
                        float x2 = v3.x - v1.x;
                        float y1 = v2.y - v1.y;
                        float y2 = v3.y - v1.y;
                        float z1 = v2.z - v1.z;
                        float z2 = v3.z - v1.z;

                        float s1 = w2.x - w1.x;
                        float s2 = w3.x - w1.x;
                        float t1 = w2.y - w1.y;
                        float t2 = w3.y - w1.y;

                        float r = 1.0f / (s1 * t2 - s2 * t1);
                        Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                        Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                        tan1[i1] += sdir;
                        tan1[i2] += sdir;
                        tan1[i3] += sdir;

                        tan2[i1] += tdir;
                        tan2[i2] += tdir;
                        tan2[i3] += tdir;

                        tri += 3;

                    }



                    for (int i = 0; i < (vertsCount); i++)
                    {

                        Vector3 n = normals[i];
                        Vector3 t = tan1[i];

                        // Gram-Schmidt orthogonalize
                        Vector3.OrthoNormalize(ref n, ref t);

                        Tangents[i].x = t.x;
                        Tangents[i].y = t.y;
                        Tangents[i].z = t.z;

                        // Calculate handedness
                        Tangents[i].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f;

                    }


                }

                return Tangents;
            }
        }

        public Vector3[] _position {
            get  {

                if (position == null) {
                    position = new Vector3[vertsCount];

                    foreach (var vp in edMesh.vertices) {
                        var lp = vp.localPos;
                        foreach (var uvi in vp.uvpoints)
                            position[uvi] = lp;
                    }
                }
                
                return position;
            }
        }

           




        public int[] _vertexIndex
        {
            get
            {
                if (originalIndex == null)
                {
                    originalIndex = new int[vertsCount];

                    foreach (var vp in edMesh.vertices)
                        foreach (var uvi in vp.uvpoints)
                            originalIndex[uvi] = vp.index;

                }
                return originalIndex;
            }
        }
        
        /*
        public Countless<vertexAnimationFrame> _anim
        {
            get
            {
                if (anims == null)
                {

                    List<int> frameInds = edMesh.hasFrame.GetItAll();

                    anims = new Countless<vertexAnimationFrame>();

                    foreach (int i in frameInds)
                        anims[i] = new vertexAnimationFrame();

                    foreach (var vp in edMesh.vertices)
                    {
                        List<Vector3> framesOfVertex = vp.anim.GetAllObjsNoOrder();
                        for (int j = 0; j < frameInds.Count; j++)
                            anims[frameInds[j]].verts[vp.index] = framesOfVertex[j]; // This is likely to be super wrong
                    }
                }

                return anims;
            }
        }
        */

        public MeshConstructor(EditableMesh edmesh, MeshPackagingProfile solution, Mesh fmesh)
        {
            profile = solution;
            edMesh = edmesh;
            mesh = fmesh;
            if (mesh == null)
                mesh = new Mesh();
        }

        void GenerateNormals()
        {

            normals = new Vector3[vertsCount];
            sharpNormals = new Vector3[vertsCount];
            bool[] NormalForced = new bool[vertsCount];

            foreach (var vp in edMesh.vertices)
            {
                vp.NormalIsSet = false;
                vp.normal = Vector3.zero;
            }

            for (int i = 0; i < vertsCount; i++)
            {
                normals[i] = Vector3.zero;
                sharpNormals[i] = Vector3.zero;
            }

            foreach (var tri in edMesh.triangles)
            {

                // ********* Calculating Normals

                tri.sharpNormal = tri.GetNormal() * tri.area;

                for (int no = 0; no < 3; no++)
                {

                    vertexpointDta vertPnt = tri.uvpnts[no].vert;
                    int mDIndex = tri.uvpnts[no];

                    sharpNormals[mDIndex] = tri.sharpNormal;

                    if (tri.SharpCorner[no])
                    {

                        normals[mDIndex] = tri.sharpNormal;
                        NormalForced[mDIndex] = true;

                        if (vertPnt.NormalIsSet)
                            vertPnt.normal += tri.sharpNormal;
                        else
                            vertPnt.normal = tri.sharpNormal;

                        vertPnt.NormalIsSet = true;

                    }
                    else
                    {
                        if (!NormalForced[mDIndex])
                            normals[mDIndex] = tri.sharpNormal;

                        if (!vertPnt.NormalIsSet)
                            vertPnt.normal += tri.sharpNormal;

                    }
                }
            }

            for (int i = 0; i < vertsCount; i++)
            {
                normals[i].Normalize();
                sharpNormals[i].Normalize();
            }


            foreach (var vp in edMesh.vertices)
                if (vp.SmoothNormal)
                {
                    vp.normal = vp.normal.normalized;
                    foreach (UVpoint uv in vp.uvpoints)
                        normals[uv] = vp.normal;

                }
        }

        void GenerateTris() {

            if (mesh != null)
                mesh.Clear();

            if (edMesh.triangles.Count == 0)
                return;

            edMesh.RefresVerticleTrisList();

            vertsCount = edMesh.AssignIndexes();

            if (edMesh.submeshCount > 1) {

                int maxSubmesh = 0;

                foreach (var t in edMesh.triangles)
                    maxSubmesh = Mathf.Max(maxSubmesh, t.submeshIndex);

                edMesh.submeshCount = maxSubmesh + 1;
            }

            tris = new List<int>[edMesh.submeshCount];
            for (int i = 0; i < edMesh.submeshCount; i++)
                tris[i] = new List<int>();

            foreach (var tri in edMesh.triangles) {
                tris[tri.submeshIndex].Add(tri.uvpnts[0]);
                tris[tri.submeshIndex].Add(tri.uvpnts[1]);
                tris[tri.submeshIndex].Add(tri.uvpnts[2]);
            }

            baseVertex = edMesh.baseVertex.ToArray();

        }

        public Mesh UpdateMesh<T>() where T: VertexDataType {

            vertsCount = edMesh.vertexCount;

            profile.UpdatePackage(this, typeof(T));

            return mesh;
        }

        public Mesh Construct() {

            GenerateTris();

            bool valid = profile.Repack(this);
            if (!valid) return mesh;

            if (edMesh.gotBindPos)
            {
                bindPoses = new Matrix4x4[vertsCount];
                for (int i = 0; i < edMesh.vertices.Count; i++)
                    bindPoses[i] = edMesh.vertices[i].bindPoses;
                mesh.bindposes = bindPoses;
            }

            if (edMesh.gotBoneWeights)
            {
                boneWeights = new BoneWeight[vertsCount];
                for (int i = 0; i < edMesh.vertices.Count; i++)
                    boneWeights[i] = edMesh.vertices[i].boneWeight;
                mesh.boneWeights = boneWeights;
            }

            int vCnt = mesh.vertices.Length;

            if (edMesh.shapes != null)
                for (int s = 0; s < edMesh.shapes.Count; s++)
                {
                    var name = edMesh.shapes[s];
                    int frames = edMesh.vertices[0].shapes[s].Count;

                    for (int f = 0; f < frames; f++)
                    {

                        var pos = new Vector3[vCnt];
                        var nrm = new Vector3[vCnt];
                        var tng = new Vector3[vCnt];

                        for (int v = 0; v < vCnt; v++)
                        {
                            BlendFrame bf = edMesh.uvsByFinalIndex[v].vert.shapes[s][f];

                            pos[v] = bf.deltaPosition;
                            nrm[v] = bf.deltaNormal;
                            tng[v] = bf.deltaTangent;

                        }
                        mesh.AddBlendShapeFrame(name, edMesh.blendWeights[s][f], pos, nrm, tng);
                    }
                }

            mesh.name = edMesh.meshName;
            // TODO: Add a function that will return blend shapes to where they should be
            return mesh;
        }

        public bool valid { get { return ((tris != null) && (edMesh.vertexCount >= 3) && (tris.TotalCount() >= 3) && (mesh != null)); } }

        public void AssignMeshAsCollider(MeshCollider c)  {
            c.sharedMesh = null;
            c.sharedMesh = mesh;
        }

        public void AssignMesh(GameObject go) {
            AssignMesh(go.GetComponent<MeshFilter>(), go.GetComponent<MeshCollider>());

        }

        public void AssignMesh(MeshFilter m, MeshCollider c) {
            if ((tris == null) || (tris.TotalCount() < 3)) return;
            if (m!= null)
            m.sharedMesh = mesh;
            if (c != null) {
                c.sharedMesh = null;
                c.sharedMesh = m.sharedMesh;
            }
        }
    }

    public enum MegavoxelRole { Solid, Damaged, Decorative }

    [Serializable]
    public class vertexAnimationFrame : CanCopy<vertexAnimationFrame> {
        public Countless<Vector3> verts;
        [NonSerialized]
       // public vertAnimNo animTexLines;

        public static ArrayManager<vertexAnimationFrame> array = new ArrayManager<vertexAnimationFrame>();
        public ArrayManager<vertexAnimationFrame> getArrMan() {
            return array;
        }

        public vertexAnimationFrame DeepCopy() {
            vertexAnimationFrame tmp = new vertexAnimationFrame();
            return tmp;
        }

        public vertexAnimationFrame() {
            verts = new Countless<Vector3>();
        }
    }


}