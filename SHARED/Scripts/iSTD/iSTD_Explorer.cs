﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerAndEditorGUI;
using System;
//using System.Windows;

namespace SharedTools_Stuff
{

    public class ElementData : Abstract_STD
    {
        public string name;
        public string std_dta;
        public string guid;

        public override bool Decode(string tag, string data)
        {
            switch (tag)
            {
                case "n": name = data; break;
                case "std": std_dta = data; break;
                case "guid": guid = data; break;
                default: return false;
            }
            return true;
        }

        public override StdEncoder Encode() => new StdEncoder()
            .Add_String("n", name)
            .Add_String("std", std_dta)
            .Add_String("guid", guid);

    }

    [Serializable]
    public class exploringSTD : Abstract_STD
#if PEGI
        , IPEGI, IGotName, IPEGI_ListInspect
#endif
    {
        iSTD std { get { return iSTD_ExplorerData.inspectedSTD; } }

        public string tag;
        public string data;
        public bool dirty = false;

        public void UpdateData()
        {
            if (tags != null)
            foreach (var t in tags)
                t.UpdateData();

            dirty = false;
            if (tags!= null)
            data = this.Encode().ToString();
        }

        public int inspectedTag = -1;
        [NonSerialized]
        public List<exploringSTD> tags;

        public exploringSTD() { tag = ""; data = "";  }

        public exploringSTD(string ntag, string ndata)
        {
            tag = ntag;
            data = ndata;
        }

#if PEGI
        public bool PEGI()
        {
            
            if (tags == null && data.Contains("|"))
                data.DecodeInto(this);

            if (inspectedTag == -1 && tags == null)
                    tag.write();
            
            if (tags!= null)
                dirty |= tag.edit_List(tags, ref inspectedTag, true);
            
            if (inspectedTag == -1)
            {
                dirty |= "data".edit(40, ref data);

                UnityEngine.Object myType = null;

                if (pegi.edit(ref myType))
                {
                    dirty = true;
                    data = ResourceLoader.LoadStory(myType);
                }

                if (dirty)
                {
                    if (icon.Refresh.Click("Update data string from tags"))
                        UpdateData();

                    if (icon.Load.Click("Load from data String").nl())
                    {
                        tags = null;
                        data.DecodeInto(this);
                        dirty = false;
                    }
                }
            }
               

            pegi.nl();

            return dirty;
        }

        public string NameForPEGI
        {
            get
            {
                return tag;
            }

            set
            {
                tag = value;
            }
        }

        public bool PEGI_inList(IList list, int ind, ref int edited)
        {

            bool changed = false;

            if (data != null && data.Contains("|"))
            {
                changed |= pegi.edit(ref tag, 150);//  tag.write(60);

                if (icon.Enter.Click("Explore data"))
                    edited = ind;
            }
            else
            {
                dirty |= pegi.edit(ref tag);
                dirty |= pegi.edit(ref data);
            }


            if (icon.Copy.Click("Copy current data to buffer"))
                STDExtensions.copyBufferValue = data;

            if (STDExtensions.copyBufferValue != null && icon.Paste.Click("Paste Component Data").nl()) {
                dirty = true;
                data = STDExtensions.copyBufferValue;
            }


            return dirty | changed;
        }

#endif

        public override StdEncoder Encode()
        {
            var cody = new StdEncoder();

            if (tags != null)
                foreach (var t in tags)
                    cody.Add_String(t.tag, t.data);
            

            return cody;

        }

        public override bool Decode(string tag, string data)
        {
            if (tags == null)
                tags = new List<exploringSTD>();
            tags.Add(new exploringSTD(tag, data));
            return true;
        }

     
    }

    [Serializable]
    public class savedISTD
#if PEGI
        : IPEGI, IGotName
#endif
    {
        public string NameForPEGI { get { return dataExplorer.tag; } set { dataExplorer.tag = value; } }
        public string comment;
        public exploringSTD dataExplorer = new exploringSTD("root", "");

        iSTD std { get { return iSTD_ExplorerData.inspectedSTD; } }
#if PEGI
        public bool PEGI()
        {
            bool changed = false;


            if (dataExplorer.inspectedTag == -1)
            {

                this.inspect_Name().nl();

                if (std != null)
                {
                    if (icon.Load.ClickUnfocus("Decode Data into "+std.ToPEGIstring()))
                        std.Decode(dataExplorer.data);
                    if (icon.Save.ClickUnfocus("Save data from "+std.ToPEGIstring()))
                        dataExplorer.data = std.Encode().ToString();
                }

                "Comment:".editBig(ref comment).nl();
            }

            dataExplorer.Nested_Inspect();


            return changed;
        }

#endif
    }

    [Serializable]
    public class iSTD_ExplorerData
    {
        public List<savedISTD> states = new List<savedISTD>();
        public int inspectedState = -1;
        public string fileFolderHolder = "STDEncodes";
        public string fileNameHolder = "file Name";
        public static iSTD inspectedSTD;


#if PEGI

        public static bool PEGI_Static(iSTD target)
        {
            inspectedSTD = target;

            bool changed = false;
            pegi.write("Load File:", 90);
            target.LoadOnDrop().nl();

            if (icon.Copy.Click("Copy Component Data"))
                STDExtensions.copyBufferValue = target.Encode().ToString();
            
            var comp = target as ComponentSTD;
            if (comp != null)
            {
                if ("Clear Component".Click())
                    comp.Reboot();
            }

            pegi.nl();

            return changed;
        }

        public bool PEGI(iSTD target)
        {
            bool changed = false;
            inspectedSTD = target;

            if (target != null && inspectedState == -1)
            {

                "Save Folder:".edit(80, ref fileFolderHolder);

                var uobj = target as UnityEngine.Object;

                if (uobj && icon.Done.Click("Use the same directory as current object."))
                    fileFolderHolder = uobj.GetAssetFolder();
                
                    uobj.clickHighlight();

                pegi.nl();
                "File Name:".edit("No file extension", 80, ref fileNameHolder);

                if (fileNameHolder.Length > 0 && icon.Save.Click("Save To Assets"))
                    target.SaveToAssets(fileFolderHolder, fileNameHolder).RefreshAssetDatabase();

                pegi.nl();

                PEGI_Static(target);
            }

            var aded = "____ Saved States:".edit_List(states, ref inspectedState, true, ref changed);

            if (aded != null && target != null)
            {
                aded.dataExplorer.data = target.Encode().ToString();
                aded.NameForPEGI = target.ToPEGIstring();
                aded.comment = DateTime.Now.ToString();
                inspectedState = states.Count - 1;
            }


            inspectedSTD = null;

            return changed;
        }
#endif
    }

    public class iSTD_Explorer : MonoBehaviour
#if PEGI
        , IPEGI
#endif
    {
        public iSTD ConnectSTD;
        public iSTD_ExplorerData data = new iSTD_ExplorerData();

#if PEGI
       /* public static bool PEGI_Static(iSTD target)
        {
            return iSTD_ExplorerData.PEGI_Static(target);
        }*/

        public bool PEGI()
        {

            UnityEngine.Object obj = ConnectSTD == null ? null : ConnectSTD as UnityEngine.Object;
            if ("Target Obj: ".edit(60, ref obj))
            {
                if (obj != null)
                    ConnectSTD = obj as iSTD;
            }
            
            MonoBehaviour mono = ConnectSTD == null ? null : ConnectSTD as MonoBehaviour;
            if ("Target Obj: ".edit(60, ref mono).nl())
            {
                if (mono != null)
                    ConnectSTD = mono as iSTD;
            }

            return data.PEGI(ConnectSTD);

        }
#endif
        
    }
}