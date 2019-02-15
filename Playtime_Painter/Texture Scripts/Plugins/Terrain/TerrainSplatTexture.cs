﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerAndEditorGUI;
using QuizCannersUtilities;


namespace Playtime_Painter
{

    [TaggedType(tag)]
    public class TerrainSplatTexture : PainterComponentPluginBase
    {

        const string tag = "TerSplat";
        public override string ClassTag => tag;

        public override bool GetTexture(ShaderProperty.TextureValue field, ref Texture tex, PlaytimePainter painter)
        {
            if (!painter.terrain || (!field.HasUsageTag(PainterDataAndConfig.terrainTexture))) return false;
            var no = field.NameForDisplayPEGI[0].CharToInt();



#if UNITY_2018_3_OR_NEWER
            var l = painter.terrain.terrainData.terrainLayers;

            if (l.Length > no)
                tex = l[no].diffuseTexture;
#else

                tex = painter.terrain.terrainData.splatPrototypes[no].texture;
#endif
            return true;
        }

        public override void GetNonMaterialTextureNames(PlaytimePainter painter, ref List<ShaderProperty.TextureValue> dest) {
            if (!painter.terrain) return;
            
#if UNITY_2018_3_OR_NEWER
            var sp = painter.terrain.terrainData.terrainLayers;

            for (var i = 0; i < sp.Length; i++) {
                var l = sp.TryGet(i);
                if (l != null)
                    dest.Add(new ShaderProperty.TextureValue( i + PainterDataAndConfig.terrainTexture + l.diffuseTexture.name, PainterDataAndConfig.terrainTexture));
            }

#else
                
                SplatPrototype[] sp = painter.terrain.terrainData.splatPrototypes;
                for (int i = 0; i < sp.Length; i++)
                {
                    if (sp[i].texture != null)
                        dest.Add(i + PainterDataAndConfig.terrainTexture + sp[i].texture.name);
                }
#endif

        }

        public override bool UpdateTilingFromMaterial(ShaderProperty.TextureValue  fieldName, PlaytimePainter painter)
        {
            if (!painter.terrain) return false;

            if (!fieldName.HasUsageTag(PainterDataAndConfig.terrainTexture)) return false;

            var no = fieldName.NameForDisplayPEGI[0].CharToInt();



#if UNITY_2018_3_OR_NEWER
            var ls = painter.terrain.terrainData.terrainLayers;

        
            if (ls.Length <= no) return true;

            var l = ls.TryGet(no);

            var terrainData = painter.terrain.terrainData;
            var width = terrainData.size.x / l.tileSize.x;
            var length = terrainData.size.z / l.tileSize.y;

            var id = painter.ImgMeta;
            id.tiling = new Vector2(width, length);
            id.offset = l.tileOffset;

#else
                    SplatPrototype[] splats = painter.terrain.terrainData.splatPrototypes;

                    if (splats.Length <= no) return true; 

                    SplatPrototype sp = painter.terrain.terrainData.splatPrototypes[no];

                        float width = painter.terrain.terrainData.size.x / sp.tileSize.x;
                    float length = painter.terrain.terrainData.size.z / sp.tileSize.y;

                    var id = painter.ImgData;
                    id.tiling = new Vector2(width, length);
                    id.offset = sp.tileOffset;

#endif


            return true;
        }

        public override bool SetTextureOnMaterial(ShaderProperty.TextureValue  field, ImageMeta id, PlaytimePainter painter)
        {
            var tex = id.CurrentTexture();
            if (!painter.terrain) return false;
            if (!field.HasUsageTag(PainterDataAndConfig.terrainTexture)) return false;
            var no = field.NameForDisplayPEGI[0].CharToInt();
            painter.terrain.SetSplashPrototypeTexture(id.texture2D, no);
            if (tex.GetType() != typeof(Texture2D))
                Debug.Log("Can only use Texture2D for Splat Prototypes. If using regular terrain may not see changes.");
            else
            {

#if UNITY_EDITOR
                var texImporter = ((Texture2D)tex).GetTextureImporter();
                if (texImporter == null) return true;
                var needReimport = texImporter.WasClamped();
                needReimport |= texImporter.HadNoMipmaps();

                if (needReimport)
                    texImporter.SaveAndReimport();
#endif

            }
            return true;
        }
    }

}