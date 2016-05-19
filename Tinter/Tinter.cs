﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Tinter
{
    public static class TDebug
    {
        // debugging stuff from the start! how novel
        public static string dbgTag = "[Tinter] ";

        public static void Print(string dbgString)
        {
            Debug.Log(dbgTag + dbgString);
        }

        public static void Warn(string dbgString)
        {
            Debug.LogWarning(dbgTag + dbgString);
        }

        public static void Err(string dbgString)
        {
            Debug.LogError(dbgTag + dbgString);
        }
    }

    [KSPAddon(KSPAddon.Startup.EditorAny, true)]
    public static class ClipBoard
    {
        static float BlendPoint = 0;
        static float Band = 0;
        static float Falloff = 0;
        static float Threshold = 0;
        static float Hue = 0;
        static float Saturation = 0;
        static float Value = 0;
        static float Gloss = 0;

        public static void Copy(Tinter t)
        {
            BlendPoint = t.tintBlendPoint;
            Band = t.tintBaseTexVBand;
            Falloff = t.tintBaseTexVFalloff;
            Threshold = t.tintBaseTexSatThreshold;
            Hue = t.tintHue;
            Saturation = t.tintSaturation;
            Value = t.tintValue;
            Gloss = t.tintGloss;
        }

        public static void Paste(Tinter t)
        {
            t.tintBlendPoint = BlendPoint;
            t.tintBaseTexVBand = Band;
            t.tintBaseTexVFalloff = Falloff;
            t.tintBaseTexSatThreshold = Threshold;
            t.tintHue = Hue;
            t.tintSaturation = Saturation;
            t.tintValue = Value;
            t.tintGloss = Gloss;
        }
    }

    public class Tinter : PartModule
    {

        /* Reference
         * 
         * Game load:
         * Constructor -> OnAwake -> OnLoad -> GetInfo
         * prefab clone:
         * Constructor -> OnAwake
         * 
         * Editor new instance:
         * Constructor -> OnAwake -> OnStart -> OnSave
         * 
         * Vessel load:
         * Constructor -> OnAwake -> OnLoad -> OnSave -> OnStart
         * 
         * Craft Launch
         * Constructor -> OnAwake -> OnLoad ( from craft file ) -> OnSave -> OnStart -> OnActive -> On[Fixed]Update
         * 
         * Craft switch via tracking:
         * Constructor -> OnAwake -> OnLoad ( from persistence ) -> OnStart -> OnActive -> On[Fixed]Update
         */

        private bool active = false;
        private bool needUpdate = false;
        private bool needShaderReplacement = true;
        private bool isSymmetryCounterpart = false;

        private List<Material> ManagedMaterials = new List<Material>();
/*
        private void EditorPartEvent( ConstructionEventType cEvent, Part part )
        {
 //           if (cEvent != ConstructionEventType.PartTweaked) return;
            TDebug.Print(part.name + "  event "+ cEvent.ToString());
        }

        private void EditorShipEvent( ShipConstruct cEvent )
        {
            TDebug.Print(part.name + " ship event "+ cEvent.ToString());
        }
 */
 
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Value"),
            UI_FloatRange(
                affectSymCounterparts = UI_Scene.Editor,
                minValue = 0,
                maxValue = 255,
                stepIncrement = 1,
                scene = UI_Scene.Editor
        )]
        public float tintBlendPoint = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Band"),
         UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBaseTexVBand = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Falloff"),
         UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBaseTexVFalloff = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Blend Saturation Threshold"),
         UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintBaseTexSatThreshold = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Hue"),
          UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintHue = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Saturation"),
          UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintSaturation = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Tint Value"),
          UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 255, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintValue = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Glossiness"),
          UI_FloatRange(affectSymCounterparts = UI_Scene.Editor, minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float tintGloss = 100;


        [KSPEvent(guiActiveEditor = true, guiName = "Copy colour settings")]
        public void CopytoClipboard()
        {
            ClipBoard.Copy(this);
        }

        [KSPEvent(guiActiveEditor = true, guiName = "Paste colour settings")]
        public void PastefromClipboard()
        {
            ClipBoard.Paste(this);
            needUpdate = true;
        }

        private void onTweakableChange(BaseField field, object what)
        {
            needUpdate = true;
        }


        private void ToggleFields( bool flag )
        {
            for ( int i = 0; i< Fields.Count; i++ )
            {
                var uiField = Fields[i].uiControlEditor as UI_FloatRange; // good grief this looks terrible, surely must be a better way?
                if ( uiField != null ) 
                {
                    Fields[i].guiActiveEditor = flag;
                }
            }
        }

        private void TweakFieldSetup()
        {
            for( int i = 0; i < Fields.Count; i++ )
            {
                var uiField = Fields[i].uiControlEditor as UI_FloatRange;
                if (uiField != null) // all parts with ui components are currently sliders we want to trap changes for
                {
  //                  TDebug.Print("Setting onFieldChange for " + Fields[i].guiName);
                    uiField.onFieldChanged = onTweakableChange;
                }
            }
        }

        private void TraverseAndReplaceShaders()
        {
            if (!AssetLoader.shadersLoaded)
                return;

            if (!needShaderReplacement)
            {
                TDebug.Print("Apparently " + part.name + " doesn't need shader replacement");
                return;
            }

            var Materials = new List<Material>();

            // messy messy, tidy
            // also can't remember why not to use foreach, but I'm sure there was something

            MeshRenderer[] r = GetComponentsInChildren<MeshRenderer>(true);
            for ( int i = 0; i < r.Length; i++ )
            {
                Materials.AddRange(r[i].materials);
            }

            for( int i = 0; i < Materials.Count; i++ )
            {
                Material m = Materials[i];
                TDebug.Print(part.name + " material " + m.name);
                var replacementShader = AssetLoader.FetchRepacementShader(m.shader.name);
                if (replacementShader)
                {
                    active = true;
                    TDebug.Print(part.name+ " Replacing shader " + m.shader.name + " with " + replacementShader.name);
                    m.shader = replacementShader;
                    ManagedMaterials.Add(m);
                    needUpdate = true;
                }
            }
            ToggleFields(active);
            needShaderReplacement = false;
        }

  //      float _TintHue;
  //      float _TintSat;
   //     float _TintVal;
   //     float _TintPoint;
   //     float _TintBand;
   //     float _TintFalloff;
   //     float _TintSatThreshold;

        private float SliderToShaderValue( float v )
        {
            return v / 255;
        }

        private float Saturate( float v )
        {
            return Mathf.Clamp01(v);
        }

        private void UpdateShaderValues()
        {
            foreach (Material m in ManagedMaterials.ToArray())
            {
                TDebug.Print(part.name + " updating material " + m.name);
                m.SetFloat("_TintPoint", SliderToShaderValue(tintBlendPoint));
                m.SetFloat("_TintBand", SliderToShaderValue(tintBaseTexVBand));
                m.SetFloat("_TintFalloff", SliderToShaderValue(tintBaseTexVFalloff));
                m.SetFloat("_TintHue", SliderToShaderValue(tintHue));
                m.SetFloat("_TintSat", SliderToShaderValue(tintSaturation));
                m.SetFloat("_TintVal", SliderToShaderValue(tintValue));

                float shaderTBTST = SliderToShaderValue(tintBaseTexSatThreshold);
                m.SetFloat("_TintSatThreshold", shaderTBTST);

                float shaderSatFalloff = Saturate(shaderTBTST * 0.75f);
                m.SetFloat("_SaturationFalloff", shaderSatFalloff);

                m.SetFloat("_SaturationWindow", shaderTBTST - shaderSatFalloff);
                m.SetFloat("_GlossMult", tintGloss * 0.01f);
            }

            if(isSymmetryCounterpart)
            {
                isSymmetryCounterpart = false;
                return;
            }

            Part[] p = part.symmetryCounterparts.ToArray();
            for ( int i = 0; i < p.Length; i++ )
                p[i].Modules.GetModule<Tinter>().SymmetryUpdate();
        }

        public void SymmetryUpdate()
        {
            needUpdate = true;
            active = true;
            isSymmetryCounterpart = true;
        }

        //

        // hopefully this might cure editor cloning awkwardness
        public Tinter()
        {
            active = false;
            needShaderReplacement = true;
        }


        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            part.OnEditorAttach += new Callback(OnEditorAttach);
            TweakFieldSetup();
            TraverseAndReplaceShaders();
            TDebug.Print("OnStart() [" + part.name + "]");
        }

//        private void OnDestroy()
 //       {
 //       }


        public void OnEditorAttach()
        {
            ToggleFields(active);
            TDebug.Print("Editor - attach [" + this.part.name + "]");
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            TDebug.Print("OnSave() [" + this.part.name + "]");
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            TDebug.Print("OnLoad() [" + this.part.name + "]");
            TraverseAndReplaceShaders();
        }


        public override void OnUpdate()
        {
            if (needUpdate)
            {
                needUpdate = false;
                //              TDebug.Print("Update() - updating shader values");
                UpdateShaderValues();
                // update shader values
            }
        }

        public void Setup()
        {
            TDebug.Print("Setup() [" + this.part.name + "]");
        }
     }
}
