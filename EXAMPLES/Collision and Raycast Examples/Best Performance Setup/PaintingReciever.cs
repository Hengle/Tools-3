﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerAndEditorGUI;
using QuizCannersUtilities;
using UnityEditorInternal.Profiling.Memory.Experimental;


namespace Playtime_Painter.Examples {
    
    [ExecuteInEditMode]
    public class PaintingReciever : MonoBehaviour, IPEGI {

        public enum RendererType {Regular, Skinned }
        
        public RendererType type;

        public Mesh originalMesh;
        public MeshFilter meshFilter;
        public Renderer meshRenderer;
        public SkinnedMeshRenderer skinnedMeshRenderer;

        [SerializeField]
        public int materialIndex;
        [SerializeField]
        private string _textureField = "";

        
        private ShaderProperty.TextureValue textureProperty;

        public String TexturePropertyName
        {
            set
            {
                _textureField = value;
                textureProperty = new ShaderProperty.TextureValue(value);
            }
        }
        
        public ShaderProperty.TextureValue TextureID {


            get
            {
                
                if (textureProperty == null)
                    textureProperty = new ShaderProperty.TextureValue(_textureField);
                
                return textureProperty;
                
              
            }                    
            
        }
        
        
        
        
        public Mesh Mesh { get { return skinnedMeshRenderer ? skinnedMeshRenderer.sharedMesh : meshFilter?.sharedMesh; } }
        public Renderer Renderer { get { return meshRenderer ? meshRenderer : skinnedMeshRenderer; } }
        public Material Material { get {
                if (!Renderer)
                    return null;
                if (materialIndex < Renderer.sharedMaterials.Length)
                    return Renderer.sharedMaterials[materialIndex];
                return null; }  set {
                if (materialIndex < Renderer.sharedMaterials.Length)
                {
                    var mats = Renderer.sharedMaterials;
                    mats[materialIndex] = value;
                    Renderer.materials = mats;
                }
            }
        }
        public Texture MatTex { get {
                if (!Material) return null;
                return Material.Has(TextureID) ? Material.Get(TextureID) : Material.mainTexture;

            } set {
                if (Material.Has(TextureID))
                    Material.Set(TextureID, value);
                else Material.mainTexture = value;   } }
        
        public Texture texture;
        public Texture originalTexture;
        public bool useTexcoord2;
        public bool fromRtManager;
        public Vector2 meshUvOffset;
        public Material originalMaterial;

        private void OnEnable()  {
            if (!Application.isPlaying)
                Refresh();

            if (Application.isPlaying && (originalTexture) && (texture) && (texture.GetType() == typeof(RenderTexture)))
                PainterCamera.Inst.Blit(originalTexture, (RenderTexture)texture);
            
        }

        public Texture GetTexture() {
            if (texture)
                return texture;

            var rtm = TexturesPool._inst;

            if (!Material) {
                Debug.Log("No Material ");
                return null;
            }

            if (!rtm) return texture;
            
            originalMaterial = Material;

            texture = rtm.GetRenderTexture();

            fromRtManager = true;
             
            Material = Instantiate(originalMaterial);

            var tex = originalTexture ? originalTexture : MatTex;
            if (tex)
                PainterCamera.Inst.Blit( tex , (RenderTexture) texture);
            else
                PainterCamera.Inst.Render(Color.black , (RenderTexture)texture);

            MatTex = texture;

            texture.GetImgData().useTexcoord2 = useTexcoord2;

            return texture;
        }

        public void Restore() {
            
            if ((fromRtManager) && (originalMaterial)) {
                fromRtManager = false;
                Material = originalMaterial;
                originalMaterial = null;
                TexturesPool._inst.ReturnOne((RenderTexture)texture);
                texture = null;
                return;
            }

            if (!texture)
                return;

            if (!originalTexture)
            {
                Debug.Log("Original Texture is not defined");
                return;
            } else if (originalTexture.GetType() != typeof(Texture2D)) {
                Debug.Log("There was no original Texture assigned to edit.");
            }

            if (texture.GetType() == typeof(Texture2D)) {
                
                ((Texture2D)texture).SetPixels(((Texture2D)originalTexture).GetPixels());
                ((Texture2D)texture).Apply(true);
            } else 
                PainterCamera.Inst.Blit(originalTexture, (RenderTexture)texture);

        }

        private void Refresh()
        {
            if (!meshFilter)
                meshFilter = GetComponent<MeshFilter>();

            if (!meshRenderer)
                meshRenderer = GetComponent<Renderer>();

            if (!skinnedMeshRenderer)
                skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        }
        
        private void OnDisable() {
            if (fromRtManager) Restore();
           
        }

        #region Inspector

        [SerializeField] bool showOptional = false;
        
        #if PEGI


        public virtual bool Inspect() {

            var changes = false;

            if (icon.Refresh.Click("Find stuff automatically"))
                Refresh();

            if ("Renderer Type:".editEnum(90, ref type).nl())
            {
                switch (type)
                {
                    case RendererType.Skinned when (!skinnedMeshRenderer):
                        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                        break;
                    case RendererType.Regular when (!meshFilter):
                        meshFilter = GetComponent<MeshFilter>();
                        meshRenderer = GetComponent<MeshRenderer>();
                        break;
                }
            }

            switch (type) {
                case RendererType.Skinned:
                "   Skinned Mesh Renderer".edit(90, ref skinnedMeshRenderer).nl(ref changes);
                    break;

                case RendererType.Regular:
                "   Mesh Filter".edit(90, ref meshFilter).nl(ref changes);
                "   Renderer".edit(90, ref meshRenderer).nl(ref changes);
                    break;
            }

            var r = Renderer;

            if ((r && (r.sharedMaterials.Length > 1)) || materialIndex != 0) 
                "   Material".select(80, ref materialIndex, r.sharedMaterials).nl();
            
            if (Material) {
                var lst = Material.MyGetTextureProperties();
                var prop = TextureID;
                if ("   Property".select(80, ref textureProperty, lst).nl())
                    TexturePropertyName = textureProperty.NameForDisplayPEGI;

            }
            
            if (gameObject.isStatic && !originalMesh) {                
                "For STATIC Game Objects original mesh is needed:".writeHint();

                pegi.nl();

                if (meshFilter && icon.Search.Click("Find mesh")) 
                        originalMesh = meshFilter.sharedMesh;
            }

            if (gameObject.isStatic)
                "  Original Mesh".edit("Static objects use Combined mesh, so original one will be needed for painting", 50, ref originalMesh).nl();



            if ("  Use second texture coordinates".toggleIcon("If shader uses texcoord2 (Baked Light) to display damage, turn this ON.", ref useTexcoord2).nl() && texture)
                texture.GetImgData().useTexcoord2 = useTexcoord2;
            
            if (Material)
            {

                if (!Material.Has(TextureID) && !Material.mainTexture )
                    "No Material Property Selected and no MainTex on Material".nl();
                else
                {

                   
                    
                    if (texture)
                    {
                        
                        
                        var t2D = texture as Texture2D;
                        
                        if (t2D)
                        {
                            icon.Done.write();
                            "CPU brush will work if object has MeshCollider".nl();

                          
                            
                            if (originalTexture) {

                                var ot2D = originalTexture as Texture2D;
                                
                                if (ot2D) {

                                    if ((ot2D.width == t2D.width) && (ot2D.height == t2D.height)) {
                                        if (("Undo Changes".Click()).nl())
                                            Restore();
                                    }
                                    else "Original and edited texture are not of the same size".nl();
                                }
                                else "Original Texture is not a Texture 2D".nl();
                            }
                   
                        }
                        else
                        {
                            if (Renderer)
                            {
                                icon.Done.write();
                                "Will paint if object has any collider".nl();
                                if (skinnedMeshRenderer)
                                {
                                    "Colliders should be placed close to actual mesh".nl();
                                    "Otherwise brush size may be too small to reach the mesh".nl();
                                }
                            }
                            else
                                "Render Texture Painting needs Skinned Mesh or Mesh Filter to work".nl();

                            if ((originalTexture) && ("Undo Changes".Click().nl()))
                                Restore();
                        }
                    }
                    else
                    {
                        var rtm = TexturesPool._inst;

                        if (rtm) {
                            "Render Texture Pool will be used to get texture".nl();
                            if (!Renderer) "! Renderer needs to be Assigned.".nl();
                            else {
                                icon.Done.write();
                                "COMPONENT SET UP CORRECTLY".write();
                                if (fromRtManager && "Restore".Click())
                                    Restore();
                                pegi.nl();
                            }
                        }
                        else
                        {
                            "No Render Texture Pool found".write();
                            if ("Create".Click().nl())
                                (TexturesPool.Inst.gameObject.name + " created").showNotificationIn3D_Views(); 
                        }
                    }
                }
            }
            else "No material found".nl();

            if ("Advanced".foldout(ref showOptional).nl()) {

                if (texture || !MatTex)
                    "Start Texture:".edit("Copy of this texture will be modified.", 110, ref originalTexture).nl(ref changes);
                
                "Target Texture".edit("If not using Render Textures Pool", 120, ref texture);
                if (Renderer && Material && "Find".Click().nl())
                    texture = MatTex;

                if (!texture || texture.GetType() == typeof(RenderTexture)) {

                    "Mesh UV Offset".edit("Some Meshes have UV coordinates with displacement for some reason. " +
                        "If your object doesn't use a mesh collider to provide a UV offset, this will be used.", 80, ref meshUvOffset).nl();
                    if (Mesh && "Offset from Mesh".Click().nl())
                    {
                        var firstVertInSubmeshIndex = Mesh.GetTriangles(materialIndex)[0];
                        meshUvOffset = useTexcoord2 ? Mesh.uv2[firstVertInSubmeshIndex] : Mesh.uv[firstVertInSubmeshIndex];

                        meshUvOffset = new Vector2((int)meshUvOffset.x, (int)meshUvOffset.y);

                        ("Mesh Offset is " + meshUvOffset.ToString()).showNotificationIn3D_Views();
                    }
                }
            }


            return changes;
        }
        #endif
        #endregion
    }

}