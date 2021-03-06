﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerAndEditorGUI;
using System.IO;
using System.Linq;
using QuizCannersUtilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PlaytimePainter {

    [TaggedType(tag)]
    public class TileableAtlasingPainterModule : PainterComponentModuleBase {

        const string tag = "TilAtlsPntr";
        public override string ClassTag => tag;

        public List<Material> preAtlasingMaterials;
        public Mesh preAtlasingMesh;
        public string preAtlasingSavedMesh;
        private int _inAtlasIndex;
        public int atlasRows = 1;

        private static MyIntVec2 atlasSector = new MyIntVec2();
        private static int sectorSize = 1;

        #region Encode & Decode

        public override CfgEncoder Encode() => new CfgEncoder()
            .Add_References("pam", preAtlasingMaterials)
            .Add_Reference("pamsh", preAtlasingMesh)
            .Add_String("sm", preAtlasingSavedMesh)
            .Add("iai", _inAtlasIndex)
            .Add("ar", atlasRows);

        public override bool Decode(string tg, string data)
        {
            switch (tg) {
                case "pam": data.Decode_References(out preAtlasingMaterials); break;
                case "pamsh": data.Decode_Reference(ref preAtlasingMesh); break;
                case "sm": preAtlasingSavedMesh = data; break;
                case "iai": _inAtlasIndex = data.ToInt(); break;
                case "ar": atlasRows = data.ToInt(); break;
                default: return false;
            }
            return true;
        }

        #endregion

        public Vector2 GetAtlasedSection()  {
          
            float atY = _inAtlasIndex / atlasRows;
            var atX = _inAtlasIndex - atY * atlasRows;

            return new Vector2(atX, atY);
        }

        public override void Update_Brush_Parameters_For_Preview_Shader(PlaytimePainter p) =>
            UnityUtils.ToggleShaderKeywords(!p.IsAtlased(), PainterDataAndConfig.UV_NORMAL, PainterDataAndConfig.UV_ATLASED);
        
        public bool PaintTexture2D(StrokeVector stroke, float brushAlpha, ImageMeta image, BrushConfig bc, PlaytimePainter painter) {
            
            if (!painter.IsAtlased()) return false;
            
            var uvCoords = stroke.uvFrom;

            var atlasedSection = GetAtlasedSection();

            sectorSize = image.width / atlasRows;
            atlasSector.From(atlasedSection * sectorSize);

            BlitFunctions.brAlpha = brushAlpha;

            BlitFunctions.half = (bc.Size(false)) / 2;
            var iHalf = Mathf.FloorToInt(BlitFunctions.half - 0.5f);

            var smooth = bc.GetBrushType(true) != BrushTypePixel.Inst;

            if (smooth)
                BlitFunctions.alphaMode = BlitFunctions.CircleAlpha;
            else
                BlitFunctions.alphaMode = BlitFunctions.NoAlpha;

            BlitFunctions.blitMode = bc.GetBlitMode(true).BlitFunctionTex2D(image);

            if (smooth) iHalf += 1;

            BlitFunctions.alpha = 1;

            BlitFunctions.r = BrushExtensions.HasFlag(bc.mask, BrushMask.R);
            BlitFunctions.g = BrushExtensions.HasFlag(bc.mask, BrushMask.G);
            BlitFunctions.b = BrushExtensions.HasFlag(bc.mask, BrushMask.B);
            BlitFunctions.a = BrushExtensions.HasFlag(bc.mask, BrushMask.A);

            BlitFunctions.cSrc = bc.Color;

            var tmp = image.UvToPixelNumber(uvCoords);

            var fromX = tmp.x - iHalf;

            tmp.y -= iHalf;
            
            var pixels = image.Pixels;

            for (BlitFunctions.y = -iHalf; BlitFunctions.y < iHalf + 1; BlitFunctions.y++)
            {

                tmp.x = fromX;

                for (BlitFunctions.x = -iHalf; BlitFunctions.x < iHalf + 1; BlitFunctions.x++)
                {

                    if (BlitFunctions.alphaMode())
                    {
                        var sx = tmp.x - atlasSector.x;
                        var sy = tmp.y - atlasSector.y;

                        sx %= sectorSize;
                        if (sx < 0)
                            sx += sectorSize;
                        sy %= sectorSize;
                        if (sy < 0)
                            sy += sectorSize;

                        BlitFunctions.blitMode(ref pixels[((atlasSector.y + sy)) * image.width + (atlasSector.x + sx)]);
                    }

                    tmp.x += 1;
                }

                tmp.y += 1;
            }
            return true;

        }
        
        public override bool OffsetAndTileUv(RaycastHit hit, PlaytimePainter p, ref Vector2 uv)
        {
            if (!p.IsAtlased()) return false;
            
            uv.x = uv.x % 1;
            uv.y = uv.y % 1;

            var m = p.GetMesh();

            if (!m) return false;
            
            var vertex = m.triangles[hit.triangleIndex * 3];
            var v4L = new List<Vector4>();
            m.GetUVs(0, v4L);
            if (v4L.Count > vertex)
                _inAtlasIndex = (int)v4L[vertex].z;

            atlasRows = Mathf.Max(atlasRows, 1);

            uv = (GetAtlasedSection() + uv) / (float)atlasRows;

            return true;
        }

        #region Inspector
        #if PEGI
        public override bool BrushConfigPEGI()
        {
            var p = PlaytimePainter.inspected;

            if (!p.IsAtlased()) return false;
            
            var changed = false;
            
            var m = p.Material;
            if (p.NotUsingPreview) {
                if (m.HasProperty(PainterDataAndConfig.ATLASED_TEXTURES))
                    atlasRows = m.GetInt(PainterDataAndConfig.ATLASED_TEXTURES);
            }

            "Atlased Texture {0}*{0}".F(atlasRows).write("Shader has _ATLASED define");

            if ("Undo".Click().nl(ref changed ))
                m.DisableKeyword(PainterDataAndConfig.UV_ATLASED);

            var id = p.ImgMeta;

            if (id.TargetIsRenderTexture())
                "Watch out, Render Texture Brush can change neighboring textures on the Atlas.".writeOneTimeHint("rtOnAtlas");

            if (id.TargetIsTexture2D()) return changed;
            
            "Render Texture painting does not yet support Atlas Editing".writeWarning();
            
            pegi.nl();
            return changed;
        }
        #endif
        #endregion

        public override void BeforeGpuStroke(PlaytimePainter painter, BrushConfig br, StrokeVector st, BrushType type)
        {
            if (!br.IsA3DBrush(painter) || !painter.IsAtlased()) return;
            
            var ats = GetAtlasedSection();
            PainterDataAndConfig.BRUSH_ATLAS_SECTION_AND_ROWS.GlobalValue = new Vector4(ats.x, ats.y, atlasRows, 1);
        }

        public override void AfterGpuStroke(PlaytimePainter painter, BrushConfig br, StrokeVector st, BrushType type) {
            if (br.IsA3DBrush(painter) && painter.IsAtlased())
               PainterDataAndConfig.BRUSH_ATLAS_SECTION_AND_ROWS.GlobalValue = new Vector4(0, 0, 1, 0);
        }
    }
    
    public class FieldAtlas : AbstractKeepUnrecognizedCfg, IPEGI
    {
        static PainterCamera TexMGMT => PainterCamera.Inst; 

        public string atlasedField;
        public int originField;
        public int atlasCreatorId;
        public bool enabled;
        public Color col;
        public AtlasTextureCreator AtlasCreator => TileableAtlasingControllerModule.inst.atlases.Count > atlasCreatorId ? TileableAtlasingControllerModule.inst.atlases[atlasCreatorId] : null; 

        #region Encode & Decode
        public override CfgEncoder Encode() => this.EncodeUnrecognized()
            .Add_String("af", atlasedField)
            .Add("of", originField)
            .Add("acid", atlasCreatorId)
            .Add_Bool("e", enabled)
            .Add("c", col);

        public override bool Decode(string tg, string data) {
            switch (tg) {
                case "af": atlasedField = data; break;
                case "of": originField = data.ToInt(); break;
                case "acid": atlasCreatorId = data.ToInt(); break;
                case "e": enabled = data.ToBool(); break;
                case "c": col = data.ToColor(); break;
            default: return false;
        }
        return true;
        }

        #endregion

        #region Inspector
#if PEGI
        public override bool Inspect() {
            var changed = false;

            var a = MaterialAtlases.inspectedAtlas;

            atlasedField.toggleIcon(ref enabled).nl(ref changed);

            if (!enabled) return changed;
            
            pegi.select_Index(ref originField, a.originalTextures).nl(ref changed);

            pegi.space();

            "Atlas".enter_Inspect(AtlasCreator, ref inspectedItems, 11).nl(ref changed);

            if (inspectedItems == -1) {
                "Atlases".select_Index(70, ref atlasCreatorId, TileableAtlasingControllerModule.inst.atlases).changes(ref changed);
                if (icon.Add.Click("Create new Atlas").nl(ref changed)) {
                    atlasCreatorId = TileableAtlasingControllerModule.inst.atlases.Count;
                    var ac = new AtlasTextureCreator(atlasedField + " for " + a.name);
                    TileableAtlasingControllerModule.inst.atlases.Add(ac);
                }
            }

            if ((atlasedField != null) && (a.originalMaterial) && (AtlasCreator != null) && (originField < a.originalTextures.Count))
            {
                var t = a.originalMaterial.Get(a.originalTextures[originField]);
                if (t && t is Texture2D)
                    icon.Done.write();
                else "Will use Color".edit(ref col).nl();
            }
            else
                "Color".edit("Color that will be used instead of a texture.", 35, ref col).nl();

            return changed;

        }
#endif
        #endregion
    }
    
    public class MaterialAtlases : AbstractKeepUnrecognizedCfg, IGotName, IPEGI {

        public string name;

        public Material originalMaterial;
        private Shader _originalShader;
        public List<ShaderProperty.TextureValue> originalTextures;
        private Material _atlasedMaterial;

        #region Encode & Decode

        public override CfgEncoder Encode() => this.EncodeUnrecognized()
            .Add_String("n", name)
            .Add_Reference("om", originalMaterial)
            .Add("ots", originalTextures)
            .Add_Reference("am", _atlasedMaterial);

        public override bool Decode(string tg, string data)
        {
            switch (tg)
            {
                case "n": name = data; break;
                case "om": data.Decode_Reference(ref originalMaterial); break;
                case "ots": data.Decode_List(out originalTextures); break;
                case "am": data.Decode_Reference(ref _atlasedMaterial); break;
            default: return false;
            }
            return true;
        }

        #endregion

        private Material DestinationMaterial => _atlasedMaterial ? _atlasedMaterial : originalMaterial; 

        public string NameForPEGI { get { return name; } set { name = value; } }

        private readonly List<FieldAtlas> _fields = new List<FieldAtlas>();
        private int _matAtlasProfile;

        private void ConvertToAtlased(PlaytimePainter painter)
        {
#if UNITY_EDITOR

            if (!_atlasedMaterial)
                _atlasedMaterial = painter.InstantiateMaterial(true);

            painter.SetOriginalShaderOnThis();

            painter.UpdateOrSetTexTarget(TexTarget.Texture2D);

            var mat = painter.Material;
            var texProperties = mat.MyGetTextureProperties();

            var index = 0;
            var passedFields = new List<FieldAtlas>();
            var passedTextures = new List<Texture2D>();
            var passedColors = new List<Color>();

            foreach (var f in _fields)
                if (f.enabled && f.AtlasCreator != null)
                {

                    var contains = false;

                    foreach (var p in texProperties)
                        if (p.NameForDisplayPEGI.Equals(originalTextures[f.originField].NameForDisplayPEGI))
                        {
                            contains = true;
                            break;
                        }

                    if (!contains) continue;
                    
                    var original = originalTextures[f.originField];

                    var tex = mat.Get(original);

                    Texture2D texture = null;

                    if (!tex)
                    {
                        var note = painter.name + " no " + original + " texture. Using Color.";
#if PEGI
                        note.showNotificationIn3D_Views();
#endif
                        Debug.Log(note);
                    }
                    else
                    {

                        if (tex.GetType() != typeof(Texture2D))
                        {
                            Debug.Log("Not a Texture 2D: " + original);
                            return;
                        }

                        texture = (Texture2D)tex;

                    }

                    var aTextures = f.AtlasCreator.textures;

                    var added = false;

                    for (var i = index; i < aTextures.Count; i++)
                        if ((aTextures[i] == null) || (!aTextures[i].used) || (aTextures[i].texture == texture))
                        {
                            index = i;
                            passedFields.Add(f);
                            passedTextures.Add(texture);
                            passedColors.Add(f.col);
                            added = true;
                            break;
                        }

                    if (added) continue;
                    
                    Debug.Log("Could not find a place for " + original);
                    
                    return;
                }

            if (passedFields.Count <= 0) return;
            
                var firstAtlasing = false;

                var atlPlug = painter.GetPlugin<TileableAtlasingPainterModule>();

                if (atlPlug.preAtlasingMaterials == null)
                {
                    atlPlug.preAtlasingMaterials = painter.Materials.ToList();
                    atlPlug.preAtlasingMesh = painter.GetMesh();
                    firstAtlasing = true;
                }

                var mainField = passedFields[0];

                atlPlug.atlasRows = mainField.AtlasCreator.Row;

                var tiling = mat.GetTiling(originalTextures[mainField.originField]);
                var offset = mat.GetOffset(originalTextures[mainField.originField]);

                for (var i = 0; i < passedFields.Count; i++) {

                    var f = passedFields[i];
                    var ac = f.AtlasCreator;

                    ac.textures[index] = new AtlasTextureField(passedTextures[i], passedColors[i]);

                    ac.AddTargets(f, originalTextures[f.originField]);
                    ac.ReconstructAsset();
                    _atlasedMaterial.SetTexture(f.atlasedField, ac.aTexture);
                }

                MeshManager.Inst.EditMesh(painter, true);

                if (firstAtlasing)
                    atlPlug.preAtlasingSavedMesh = MeshManager.editedMesh.Encode().ToString();

                painter.selectedMeshProfile = _matAtlasProfile;

                if (tiling != Vector2.one || offset != Vector2.zero)
                {
                    MeshManager.editedMesh.TileAndOffsetUVs(offset, tiling, 0);
                    Debug.Log("offsetting " + offset + " tiling " + tiling);
                }

                TriangleAtlasTool.Inst.SetAllTrianglesTextureTo(index, 0, painter.selectedSubMesh);
                MeshManager.Inst.Redraw();
                MeshManager.Inst.DisconnectMesh();

                _atlasedMaterial.SetFloat(PainterDataAndConfig.ATLASED_TEXTURES, atlPlug.atlasRows);
                painter.Material = _atlasedMaterial;

                if (firstAtlasing)
                {
                    var m = painter.GetMesh();
                    m.name = m.name + "_Atlased_" + index;
                }
 
                _atlasedMaterial.EnableKeyword(PainterDataAndConfig.UV_ATLASED);
            
#endif
                    }

        private void FindAtlas(int field)
        {
            var texMGMT = PainterCamera.Inst;

            var atlases = TileableAtlasingControllerModule.inst.atlases;
            
            for (var a = 0; a < atlases.Count; a++)
            {
                var atl = atlases[a];
                
                if (!atl.atlasFields.Contains(_fields[field].atlasedField)) continue;
                
                foreach (var t in originalTextures)
                {
                    if (!atl.targetFields.Contains(t)) continue;
                    
                    _fields[field].atlasCreatorId = a;
                    
                    var tex = originalMaterial.Get(t);

                    if (!tex) continue; 
                    
                    var t2 = (Texture2D) tex;
                    
                    if (t2 && atl.textures.Contains(t2))
                        return;
                }
            }
        }

        private void OnChangeMaterial(PlaytimePainter painter)
        {
#if UNITY_EDITOR

            if (originalMaterial)
                originalTextures = originalMaterial.MyGetTextureProperties();

            if ((DestinationMaterial) && (DestinationMaterial.HasProperty(PainterDataAndConfig.isAtlasedProperty)))
            {
                var aTextures = DestinationMaterial.MyGetTextureProperties();
                _fields.Clear();
                foreach (var t in aTextures)
                {
                    var ac = new FieldAtlas();
                    _fields.Add(ac);
                    ac.atlasedField = t.NameForDisplayPEGI;
                }

#if PEGI
                _atlasedShader = DestinationMaterial.shader;
#endif

                foreach (var p in MaterialEditor.GetMaterialProperties(new Object[] { DestinationMaterial }))
                    if (p.displayName.Contains(PainterDataAndConfig.isAtlasableDisaplyNameTag))
                        foreach (var f in _fields)
                            if (f.atlasedField.SameAs(p.name))
                                f.enabled = true;
                            

                if (!_atlasedMaterial)
                    for (var i = 0; i < _fields.Count; i++)
                        _fields[i].originField = i;
                else if (originalMaterial)
                {
                    var orTexts = originalMaterial.MyGetTextureProperties();
                    foreach (var f in _fields)
                        for (var i = 0; i < orTexts.Count; i++)
                            if (orTexts[i].NameForDisplayPEGI.SameAs(f.atlasedField))
                                f.originField = i;


                }
            }

            if (!originalMaterial) return;
            
            for (var i = 0; i < _fields.Count; i++)
                FindAtlas(i);
            
#endif
            }

        #region Inspector
        #if PEGI
        private Shader _atlasedShader;
        public static MaterialAtlases inspectedAtlas;
        private bool _showHint;
        public override bool Inspect()
        {
            var changed = false;

#if UNITY_EDITOR
            var painter = PlaytimePainter.inspected;
            inspectedAtlas = this;


            painter.SetOriginalShaderOnThis();

            var mat = painter.Material;

            if ((mat) && ((mat != originalMaterial) || mat.shader != _originalShader))
            {
                originalMaterial = mat;
                _originalShader = mat.shader;
                OnChangeMaterial(painter);
            }
            changed |= "Name".edit(50, ref name).nl();
            if ("Hint".foldout(ref _showHint).nl())
            {

                ("If you don't set Atlased Material(Destination)  it will try to create a copy of current material and set isAtlased toggle on it, if it has one." +
                    " Below you can see: list of Texture Properties, for each you can select or create an atlas. Atlas is a class that holds all textures assigned to an atlas, and also creates and stores the atlas itself." +
                    "After this you can select a field from current Material, texture of which will be copied into an atlas. A bit confusing, I know)" +
                    "Also if light looks smudged, rebuild the light.").writeHint();
            }

            if ((("Atlased Material:".edit(90, ref _atlasedMaterial).nl()) ||
                (_atlasedMaterial && _atlasedMaterial.shader != _atlasedShader)).changes(ref changed)) 
                OnChangeMaterial(painter);
            

            if (painter)
            {
                var mats = painter.Materials;
                if (mats != null)
                {
                    if (mats.Length > 1)
                    {
                        if ("Source Material:".select_Index("Same as selecting a sub Mesh, which will be converted", 90, ref painter.selectedSubMesh, mats).changes(ref changed))
                            OnChangeMaterial(painter);
                    }
                    else if (mats.Length > 0)
                        "Source Material".writeUobj("Sub Mesh which will be converted", 90, mats[0]);
                }
                pegi.nl();
                pegi.space();
                pegi.nl();
            }



            pegi.space();
            pegi.nl();

            foreach (var f in _fields)
                changed |= f.Nested_Inspect();

            changed |= "Mesh Profiles [{0}]".F(PainterCamera.Data.meshPackagingSolutions.Count).select_Index(140, ref _matAtlasProfile, PainterCamera.Data.meshPackagingSolutions).nl();

            if (DestinationMaterial && !DestinationMaterial.HasProperty(PainterDataAndConfig.isAtlasedProperty))
            {
                if (!_atlasedMaterial) "Original Material doesn't have isAtlased property, change shader or add Destination Atlased Material".writeHint();
                else "Atlased Material doesn't have isAtlased property".writeHint();
            }
            else if (originalMaterial)
            {

                var names = "";
                foreach (var f in _fields)
                    if (f.enabled && f.AtlasCreator == null) names += f.atlasedField + ", ";

                if (names.Length > 0)
                    ("Fields " + names + " don't have atlases assigned to them, create some").writeHint();
                else if ("Convert to Atlased".Click())
                    ConvertToAtlased(painter);
            }
#endif

            inspectedAtlas = null;
            
            return changed;

        }
        #endif
        #endregion
    }
    
    public class AtlasTextureField: AbstractKeepUnrecognizedCfg, IPEGI_ListInspect
    {
        public Texture2D texture;
        public Color color = Color.black;
        public bool used;

        public AtlasTextureField() { used = true; }

        public AtlasTextureField(Texture2D tex, Color col)
        {
            texture = tex;
            color = col;
            used = true;
        }

        #region Inspector
        #if PEGI
        public bool InspectInList(IList list, int ind, ref int edited)
        {
            (used ? icon.Active : icon.InActive).write();

            var changed = false;
            pegi.edit(ref texture).changes(ref changed);
            if (!texture)
            pegi.edit(ref color).changes(ref changed);

            return changed;
        }
        #endif
        #endregion

        #region Encode & Decode
        public override CfgEncoder Encode() => this.EncodeUnrecognized()
            .Add_Reference("tex", texture)
            .Add("col", color)
            .Add_IfTrue("u", used);

        public override bool Decode(string tg, string data)
        {
            switch (tg)
            {
                case "tex": data.Decode_Reference(ref texture); break;
                case "col": color = data.ToColor(); break;
                case "u": used = data.ToBool(); break;
                default: return false;
            }
            return true;
        }
        #endregion

    }
    
    public class AtlasTextureCreator : AbstractKeepUnrecognizedCfg, IGotName, IPEGI
    {
        private static PainterDataAndConfig Cfg => PainterCamera.Data;

        private int _atlasSize = 2048;

        private int _textureSize = 512;

        private bool _sRgb = true;
        
        public string NameForPEGI { get; set;}

        public List<ShaderProperty.TextureValue> targetFields = new List<ShaderProperty.TextureValue>();

        public List<string> atlasFields = new List<string>();

        private List<string> _srcFields = new List<string>();

        public Texture2D aTexture;

        public List<AtlasTextureField> textures = new List<AtlasTextureField>();
        private ListMetaData _texturesMeta = new ListMetaData("Textures", enterIcon: icon.Painter);

        #region Encode & Decode

        public override CfgEncoder Encode() => this.EncodeUnrecognized()
            .Add("tf", targetFields)
            .Add("af", atlasFields)
            .Add("sf", _srcFields)
            .Add_Reference("atex", aTexture)
            .Add("txs", textures, _texturesMeta)
            .Add_String("n", NameForPEGI)
            .Add_Bool("rgb", _sRgb)
            .Add("s", _textureSize)
            .Add("as", _atlasSize);

        public override bool Decode(string tg, string data)
        {
            switch (tg) {
                case "tf": data.Decode_List(out targetFields); break;
                case "af": data.Decode_List(out atlasFields); break;
                case "sf": data.Decode_List(out _srcFields); break;
                case "atex": data.Decode_Reference(ref aTexture); break;
                case "txs": data.Decode_List(out textures, ref _texturesMeta); break;
                case "n": NameForPEGI = data; break;
                case "rgb": _sRgb = data.ToBool(); break;
                case "s": _textureSize = data.ToInt(); break;
                case "as": _atlasSize = data.ToInt(); break;
            default: return false;
            }
            return true;
        }

        #endregion

        public int Row => _atlasSize / _textureSize; 

        public void AddTargets(FieldAtlas at, ShaderProperty.TextureValue target)
        {
            if (!atlasFields.Contains(at.atlasedField))
                atlasFields.Add(at.atlasedField);
            if (!targetFields.Contains(target))
                targetFields.Add(target);
        }

        private void Init() => AdjustListSize();
        
        public AtlasTextureCreator()
        {
            Init();
        }
        
        public AtlasTextureCreator(string newName)
        {
            NameForPEGI = newName;
            NameForPEGI = NameForPEGI.GetUniqueName(TileableAtlasingControllerModule.inst.atlases);
            Init();
        }

        private void AdjustListSize()
        {
            var ntc = TextureCount;
            while (textures.Count < ntc)
                textures.Add(new AtlasTextureField(null, Color.gray));
        }

        private int TextureCount
        {
            get { var r = Row; return r * r; }
        }

        private void ColorToAtlas(Color col, int x, int y)
        {
            var size = _textureSize * _textureSize;
            var pix = new Color[size];
            for (var i = 0; i < size; i++)
                pix[i] = col;

            aTexture.SetPixels(x * _textureSize, y * _textureSize, _textureSize, _textureSize, pix);
        }

        private void SmoothBorders(Texture2D atlas, int mipLevel)
        {
            var col = atlas.GetPixels(mipLevel);

            var aSize = _atlasSize;
            var tSize = _textureSize;

            for (var i = 0; i < mipLevel; i++)
            {
                aSize /= 2;
                tSize /= 2;
            }

            if (tSize == 0)
                return;

            var cnt = aSize / tSize;

            for (var ty = 0; ty < cnt; ty++)
            {
                var startY = ty * tSize * aSize;
                var lastY = (ty * tSize + tSize - 1) * aSize;
                for (var tx = 0; tx < cnt; tx++)
                {
                    var startX = tx * tSize;
                    var lastX = startX + tSize - 1;

                    Color tmp = Color.clear;
                    
                    tmp += col[startY + startX].linear;
                    tmp += col[startY + lastX].linear;
                    tmp += col[lastY + startX].linear;
                    tmp += col[lastY + lastX].linear;

                    tmp *= 0.25f;

                    var tmpC = tmp.gamma;
                    
                    col[startY + startX] = tmpC;
                    col[startY + lastX] = tmpC;
                    col[lastY + startX] = tmpC;
                    col[lastY + lastX] = tmpC;


                    for (var x = startX + 1; x < lastX; x++)
                    {
                        tmp = Color.clear;
                        tmp += col[startY + x].linear;
                        tmp += col[lastY + x].linear;
                        tmp *= 0.5f;
                        tmpC = tmp.gamma;
                        col[startY + x] = tmpC;
                        col[lastY + x] = tmpC;
                    }

                    for (var y = startY + aSize; y < lastY; y += aSize)
                    {
                        tmp = Color.clear;

                        tmp += col[y + startX].linear;
                        tmp += col[y + lastX].linear;
                        tmp *= 0.5f;
                        tmpC = tmp.gamma;
                        col[y + startX] = tmpC;
                        col[y + lastX] = tmpC;
                    }
                }
            }

            atlas.SetPixels(col, mipLevel);
        }

        private void ReconstructAtlas()
        {

            if (aTexture && (aTexture.width != _atlasSize)) {
                Object.DestroyImmediate(aTexture);
                aTexture = null;
            }

            if (!aTexture)
                aTexture = new Texture2D(_atlasSize, _atlasSize, TextureFormat.ARGB32, true, !_sRgb);

            var texturesInRow = _atlasSize / _textureSize;
            
            var curIndex = 0;

            var defaultCol = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            for (var y = 0; y < texturesInRow; y++)
                for (var x = 0; x < texturesInRow; x++) {
                    var atlField = textures[curIndex];
                    if ((textures.Count > curIndex) && (atlField != null) && atlField.used)
                    {

                        var tex = atlField.texture;

                        if (tex) {
#if UNITY_EDITOR
                            tex.Reimport_IfNotReadale();
#endif

                            var from = tex.GetPixels(_textureSize, _textureSize);

                            aTexture.SetPixels(x * _textureSize, y * _textureSize, _textureSize, _textureSize, from);

                        }
                        else
                            ColorToAtlas(atlField.color, x, y);
                    }
                    else
                        ColorToAtlas(defaultCol, x, y);

                    curIndex++;
                }

        }


#if UNITY_EDITOR
        public void ReconstructAsset()
        {

            ReconstructAtlas();

            for (var m = 0; m < aTexture.mipmapCount; m++)
                SmoothBorders(aTexture, m);

            aTexture.Apply(false);
            
            var bytes = aTexture.EncodeToPNG();

            var lastPart = Path.Combine(Cfg.texturesFolderName, Cfg.atlasFolderName);
            var fullPath = Path.Combine(Application.dataPath, lastPart);
            Directory.CreateDirectory(fullPath);

            var fileName = NameForPEGI + ".png";
            var relativePath = Path.Combine("Assets", lastPart, fileName);
            fullPath += fileName;

            File.WriteAllBytes(fullPath, bytes);
            
            AssetDatabase.Refresh(); // few times caused color of the texture to get updated to earlier state for some reason

            aTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(relativePath, typeof(Texture2D));

            TextureImporter other = null;

            foreach (var t in textures)
                if ((t != null) && t.texture) {
                    other = t.texture.GetTextureImporter();
                    break;
                }

            var ti = aTexture.GetTextureImporter();

            var needReimport = ti.WasNotReadable();

            if (other != null)
                needReimport |= ti.WasWrongIsColor(other.sRGBTexture);

            needReimport |= ti.WasClamped();

            if (needReimport) ti.SaveAndReimport();

        }
#endif

#if PEGI

        public override bool Inspect() {
            var changed = false;
#if UNITY_EDITOR

            if (inspectedItems == -1) {

                "Atlas size:".editDelayed(ref _atlasSize, 80).nl(ref changed);
                    _atlasSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(_atlasSize, 512, 4096));

                if ("Textures size:".editDelayed(ref _textureSize, 80).nl(ref changed))

                _textureSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(_textureSize, 32, _atlasSize / 2));

                AdjustListSize();
            }

            _texturesMeta.enter_List(ref textures, ref inspectedItems, 11).changes(ref changed);

            if ("Textures:".foldout().nl()) {
                AdjustListSize();
                var max = TextureCount;

                for (var i = 0; i < max; i++) {
                    var t = textures[i];

                    if (!t.used) continue;
                    
                    pegi.edit(ref t.texture);
                    
                    if (!t.texture)
                        pegi.edit(ref t.color);
                    
                    pegi.newLine();
                }
            }

            pegi.newLine();
            "Is Color Atlas:".toggle(80, ref _sRgb).nl(ref changed);

            if ("Generate".Click().nl(ref changed))
                ReconstructAsset();

            if (aTexture)
                ("Atlas At " + AssetDatabase.GetAssetPath(aTexture)).edit(ref aTexture, false).nl(ref changed);

#endif

            return changed;
        }
#endif
    }

    public static class AtlasingExtensions
    {
        public static bool IsAtlased(this PlaytimePainter p)
        {
            if (!p) return false;
            var mat = p.Material;
            return mat && mat.IsAtlased(p.GetMaterialTextureProperty);
        }
        public static bool IsProjected(this PlaytimePainter p) { return p.Material.IsProjected(); }

        public static bool IsAtlased(this Material mat, string property) => mat.IsAtlased() && property.Contains(PainterDataAndConfig.isAtlasableDisaplyNameTag);
        
        public static bool IsAtlased(this Material mat, ShaderProperty.TextureValue property) => mat.IsAtlased() && property.NameForDisplayPEGI.Contains(PainterDataAndConfig.isAtlasableDisaplyNameTag);

        
        public static bool IsAtlased(this Material mat) => mat && mat.shaderKeywords.Contains(PainterDataAndConfig.UV_ATLASED);
      
        public static bool Contains(this IEnumerable<AtlasTextureField> lst, Texture2D tex) => lst.All(ef => (ef == null) || (!ef.texture) || (ef.texture != tex));
        
    }
}