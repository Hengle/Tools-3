﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

using StoryTriggerData;

namespace Playtime_Painter{

public static class TextureEditorExtensionFunctions  {

        public static float GetChanel(this Color col, ColorChanel chan)
        {

            switch (chan)
            {
                case ColorChanel.R:
                    return col.r;
                case ColorChanel.G:
                    return col.g;
                case ColorChanel.B:
                    return col.b;
                default:
                    return col.a;
            }
        }

        public static void SetChanel(this ColorChanel chan,  ref Color col,  float value)
        {
   
            switch (chan)
            {
                case ColorChanel.R:
                    col.r = value;
                    break;
                case ColorChanel.G:
                    col.g = value;
                    break;
                case ColorChanel.B:
                    col.b = value;
                    break;
                case ColorChanel.A:
                    col.a = value;
                    break;
            }
        }

        public static void Transfer(this BrushMask bm, ref Color col, Color c)
        {
           

            if ((bm & BrushMask.R) != 0)
                col.r = c.r;
            if ((bm & BrushMask.G) != 0)
                col.g =  c.g;
            if ((bm & BrushMask.B) != 0)
                col.b =  c.b;
            if ((bm & BrushMask.A) != 0)
                col.a =  c.a;
        }
        
        public static Mesh getMesh(this PlaytimePainter p) {
        if (p == null) return null;
        if (p.skinnedMeshRendy != null) return p.colliderForSkinnedMesh;//skinnedMeshRendy.sharedMesh;
        if (p.meshFilter != null) return p.meshFilter.sharedMesh;

        return null;
    }

        public static bool ContainsInstanceType(this List<PainterPluginBase> collection, Type type){

		foreach (var t in collection) 
			if (t.GetType() == type) return true; 
		
		return false;
	}

		public static float strokeWidth (this BrushConfig br, float pixWidth, bool world){
			return br.Size(world) / (pixWidth) * 2 * PainterManager.orthoSize;
		}

        public static bool isSingleBufferBrush(this BrushConfig b) { 
                return (PainterManager.inst.isLinearColorSpace && b.blitMode.supportedBySingleBuffer && b.type(false).supportedBySingleBuffer && b.paintingRGB);
        }
        
        public static bool isProjected(this Material mat)
        {
            if (mat == null) return false;
            return mat.shaderKeywords.Contains(PainterConfig.UV_PROJECTED);
        }

        public static stdEncoder EncodeStrokeFor(this BrushConfig brush, PlaytimePainter painter) {
            stdEncoder cody = new stdEncoder();

            bool rt = painter.curImgData.TargetIsRenderTexture();

            BlitMode mode = brush.blitMode;
            BrushType type = brush.type(!rt);
            
            cody.Add(rt ? "typeGPU" : "typeCPU", brush._type(!rt));
            
            bool worldSpace = rt && type.isA3Dbrush;

            if (worldSpace)
                cody.Add("size3D", brush.Brush3D_Radius);
            else
                cody.Add("size2D", brush.Brush2D_Radius/((float)painter.curImgData.width));


            cody.Add("useMask", brush.useMask);

            if (brush.useMask)
                cody.Add("mask", (int)brush.mask);

            cody.Add("mode", brush._bliTMode);

            if (mode.showColorSliders)
                cody.AddIfNotNull(brush.colorLinear);

            if (mode.usingSourceTexture)
                cody.Add("source", brush.selectedSourceTexture);

            if (rt) {

                if ((mode.GetType() == typeof(BlitModeBlur)))
                    cody.Add("blur", brush.blurAmount);

                if (type.isUsingDecals) {
                    cody.Add("decA", brush.decalAngle);
                    cody.Add("decNo", brush.selectedDecal);
                }

                if (brush.useMask) {
                    cody.Add("Smask", brush.selectedSourceMask);
                    cody.Add("maskTil", brush.maskTiling);
                    cody.Add("maskFlip", brush.flipMaskAlpha);
                    cody.Add("maskOff", brush.maskOffset);
                }
            }



            cody.Add("hard",brush.Hardness);
            cody.Add("speed", brush.speed);
          


            return cody;
        }

        public static float Size(this BrushConfig brush, imgData id) {
            bool worldSpace = id.TargetIsRenderTexture() && brush.type(false).isA3Dbrush;
            return brush.Size(worldSpace);
        }

}

}