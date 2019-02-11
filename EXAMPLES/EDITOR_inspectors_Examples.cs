﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerAndEditorGUI;
using Playtime_Painter.Examples;

namespace Playtime_Painter
{

#if PEGI && UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(GodMode))]
    public class GodModeDrawer : PEGI_Inspector<GodMode> {}
    
    [CustomEditor(typeof(PixelArtMeshGenerator))]
    public class PixelArtMeshGeneratorEditor : PEGI_Inspector<PixelArtMeshGenerator> { }

    [CustomEditor(typeof(LightCaster))]
    public class BakedShadowsLightProbeEditor : PEGI_Inspector<LightCaster> {}

    [CustomEditor(typeof(MergingTerrainController))]
    public class MergingTerrainEditor : PEGI_Inspector<MergingTerrainController> { }

    [CustomEditor(typeof(PainterBall))]
    public class PainterBallEditor : PEGI_Inspector<PainterBall> { }

    [CustomEditor(typeof(PaintingReciever))]
    public class PaintingRecieverEditor : PEGI_Inspector<PaintingReciever>  { }

    [CustomEditor(typeof(PaintWithoutComponent))]
    public class PaintWithoutComponentEditor : PEGI_Inspector<PaintWithoutComponent>  { }

    [CustomEditor(typeof(RaycastOnCollisionPainter))]
    public class PainterCasterEditor : PEGI_Inspector<RaycastOnCollisionPainter> { }

    [CustomEditor(typeof(ShadowVolumeTexture))]
    public class ShadowVolumeTextureEditor : PEGI_Inspector<ShadowVolumeTexture> { }

    [CustomEditor(typeof(SkinnedMeshCaster))]
    public class SkinnedMeshCasterEditor : PEGI_Inspector<SkinnedMeshCaster> { }
    
    [CustomEditor(typeof(WaterController))]
    public class WaterEditor : PEGI_Inspector<WaterController> { }

#endif
}