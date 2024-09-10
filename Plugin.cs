/*

Copyright NeonFlames 2024

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS “AS IS” AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE

*/

using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using GPUInstancer;
using HarmonyLib;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace MIDNIGHT_RUNNER;

public class OptionsTemplate {
    public ConfigEntry<bool> enable;
    public void Configure(ConfigFile c, string s) {
        enable = c.Bind(s, "Enable", false, $"Enables {s} modifications");
    }
}

public class VHSOptions : OptionsTemplate {
    public ConfigEntry<bool> showNoise, showJitter, bleed, fisheye, filmgrain,
        feedback, vignette;
    public new void Configure(ConfigFile c, string s) {
        base.Configure(c, s);
        showNoise = c.Bind(s, "ShowNoise", true);
        showJitter = c.Bind(s, "ShowJitter", true);
        bleed = c.Bind(s, "Bleed", true);
        feedback = c.Bind(s, "Feedback", true);
        fisheye = c.Bind(s, "Fisheye", true);
        filmgrain = c.Bind(s, "Filmgrain", true);
        vignette = c.Bind(s, "Vignette", true);
    }
}


public class DOFOptions : OptionsTemplate {
    public ConfigEntry<DepthOfField.BlurSampleCount> blurSampleCount;
    public ConfigEntry<DepthOfField.BlurType> blurType;
    public ConfigEntry<bool> visualizeFocus, highResolution;
    public new void Configure(ConfigFile c, string s) {
        base.Configure(c, s);
        highResolution = c.Bind(s, "HighResolution", true);
        blurSampleCount = c.Bind(s, "BlurSampleCount", DepthOfField.BlurSampleCount.Medium);
        blurType = c.Bind(s, "BlurType", DepthOfField.BlurType.DiscBlur);
        visualizeFocus = c.Bind(s, "VisualizeFocus", true);
    }
}

public class GIMMIOptions : OptionsTemplate {
    public ConfigEntry<bool> importDetails, importObjects, importTrees, prefabInThreads, detailInThreads;
    public new void Configure(ConfigFile c, string s) {
        base.Configure(c, s);
        importDetails = c.Bind(s, "ImportDetails", true);
        importObjects = c.Bind(s, "ImportObjects", true);
        importTrees = c.Bind(s, "ImportTrees", true);
        detailInThreads = c.Bind(s, "DetailRunInThreads", true);
        prefabInThreads = c.Bind(s, "PrefabRunInThreads", true);
    }
}

public class GITSOptions : OptionsTemplate {
    public ConfigEntry<float> maxDetailDistance, maxTreeDistance;
    public new void Configure(ConfigFile c, string s) {
        base.Configure(c, s);
        maxDetailDistance = c.Bind(s, "MaxDetailDistance", 1.0f);
        maxTreeDistance = c.Bind(s, "MaxTreeDistance", 1.0f);
    }
}

public class EngineOptions : OptionsTemplate {
    public ConfigEntry<float> lodBias;
    public ConfigEntry<int> mipmapMaxReduction;
    public ConfigEntry<QualityLevel> qualityLevel;
    public ConfigEntry<ShadowResolution> shadowRes;
    public int _qlevels;
    public new void Configure(ConfigFile c, string s) {
        base.Configure(c, s);
        lodBias = c.Bind(s, "LodBias", 1.0f, "Value must be greater than 0");
        mipmapMaxReduction = c.Bind(s, "MipmapMaxReduction", 2, "Maximum level of mipmap reduction");
        if (lodBias.Value < 0.0f) lodBias.Value = 0.0f;
        string[] _qnames = QualitySettings.names;
        string _qname = _qnames.First();
        if (_qnames.Length > 1) foreach (string str in _qnames[1..]) {
            _qname += str;
        }
        qualityLevel = c.Bind(s, "QualityLevel", QualitySettings.currentLevel, $"Available levels: {_qname}");
        shadowRes = c.Bind(s, "ShadowResolution", ShadowResolution.High);
    }
}

public class MRCFG {
    public bool dofEnable, vhsEnable, gimmiEnable, gitsEnable, engineEnable;
    public delegate void dofD(DepthOfField inst);
    public dofD dofd;
    public delegate void vhsD(postVHSPro inst);
    public vhsD vhsd;
    public delegate void gimmiD(GPUInstancerMapMagicIntegration inst);
    public gimmiD gimmid;
    public delegate void gitsD(GPUInstancerTerrainSettings inst);
    public gitsD gitsd;
    public delegate void engineD();
    public engineD engined;

    public void Consume(DOFOptions dof, VHSOptions vhs, GIMMIOptions gimmi, GITSOptions gits, EngineOptions engine) {
        if (engine.enable.Value) {
            engineEnable = true;
            engined = () => {
                QualitySettings.lodBias = engine.lodBias.Value;
                QualitySettings.SetQualityLevel((int)engine.qualityLevel.Value, true);
                QualitySettings.shadowResolution = engine.shadowRes.Value;
                QualitySettings.streamingMipmapsMaxLevelReduction = engine.mipmapMaxReduction.Value;
            };
        } else engineEnable = false;
        if (dof.enable.Value) {
            dofEnable = true;
            dofd = (DepthOfField inst) => {
                inst.blurSampleCount = dof.blurSampleCount.Value;
                inst.blurType = dof.blurType.Value;
            };
            if (!dof.highResolution.Value) dofd += (DepthOfField inst) => {inst.highResolution = false;};
            if (!dof.visualizeFocus.Value) dofd += (DepthOfField inst) => {inst.visualizeFocus = false;};
        } else dofEnable = false;
        if (vhs.enable.Value) {
            vhsEnable = true;
            vhsd = (_) => {};
            if (!vhs.showNoise.Value) vhsd += (postVHSPro inst) => {inst.g_showNoise = false;}; 
            if (!vhs.showJitter.Value) vhsd += (postVHSPro inst) => {inst.g_showJitter = false;}; 
            if (!vhs.bleed.Value) vhsd += (postVHSPro inst) => {inst.bleedOn = false;}; 
            if (!vhs.filmgrain.Value) vhsd += (postVHSPro inst) => {inst.filmgrainOn = false;}; 
            if (!vhs.fisheye.Value) vhsd += (postVHSPro inst) => {inst.fisheyeOn = false;}; 
            if (!vhs.feedback.Value) vhsd += (postVHSPro inst) => {inst.feedbackOn = false;};
            if (!vhs.vignette.Value) vhsd += (postVHSPro inst) => {inst.vignetteOn = false;};
        } else vhsEnable = false;
        if (gimmi.enable.Value) {
            gimmiEnable = true;
            gimmid = (GPUInstancerMapMagicIntegration inst) => {
                inst.importObjects = gimmi.importObjects.Value;
                inst.importTrees = gimmi.importTrees.Value;
                inst.importDetails = gimmi.importDetails.Value;
                inst.prefabRunInThreads = gimmi.prefabInThreads.Value;
                inst.detailRunInThreads = gimmi.detailInThreads.Value;
            };
            if (gits.enable.Value) {
                gitsEnable = true;
                gitsd += (GPUInstancerTerrainSettings inst) => {
                    inst.maxDetailDistance = gits.maxDetailDistance.Value;
                    inst.maxTreeDistance = gits.maxTreeDistance.Value;
                };
            } else gitsEnable = false;
        } else gimmiEnable = false;
    }
}

public class VHS_Patch {
    static IEnumerator IE(postVHSPro inst) {
            while (true) {
                Plugin.CFG.vhsd(inst);
                yield return new WaitForSeconds(2.5f);
            }
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(postVHSPro), "Awake")]
    public static void AwakePost(ref postVHSPro __instance) {
        __instance.StartCoroutine(IE(__instance).WrapToIl2Cpp());
    }

}

public class DOF_Patch {
    static IEnumerator IE(DepthOfField inst) {
            while (true) {
                Plugin.CFG.dofd(inst);
                yield return new WaitForSeconds(2.5f);
            }
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DepthOfField), "OnEnable")]
    public static void OnEnablePost(ref DepthOfField __instance) {
        __instance.StartCoroutine(IE(__instance).WrapToIl2Cpp());
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DepthOfField), "OnDisable")]
    public static void OnDisablePost(ref DepthOfField __instance) {
        __instance.StopCoroutine(IE(__instance).WrapToIl2Cpp());
    }
}

public partial class MRLive : MonoBehaviour {
    MRCFG.gimmiD gimmi;
    MRCFG.gitsD gits;

    delegate void gameD();
    gameD gamed;

    public void Awake() {
        gits = (_) => {};
        gamed = () => {};
        if (Plugin.CFG.gimmiEnable) {
            gimmi = Plugin.CFG.gimmid;
            if (Plugin.CFG.gitsEnable) gits = Plugin.CFG.gitsd;
            gamed += () => {
                foreach (GPUInstancerMapMagicIntegration inst in FindObjectsOfType<GPUInstancerMapMagicIntegration>()) {
                    gimmi(inst);
                    gits(inst.terrainSettings);
                }
            };
        }
        if (Plugin.CFG.engineEnable) {
            Plugin.CFG.engined();
        }
    }

    public void Start() {
        StartCoroutine(Game().WrapToIl2Cpp());
    }

    public IEnumerator Game() {
        while (true) {
            gamed();
            yield return new WaitForSeconds(5f);
        }
    }
}

[BepInPlugin("neonflames.midnight_runner", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin {
    internal static new ManualLogSource Log;

    static ConfigEntry<bool> enable;
    public DOFOptions dofOpts = new();
    public VHSOptions vhsOpts = new();
    public GIMMIOptions gimmiOpts = new();
    public GITSOptions gitsOpts = new();
    public EngineOptions engineOpts = new();
    static MRCFG cfg = new();
    Harmony harmony;

    public static MRCFG CFG { get => cfg; set => cfg = value; }

    public override void Load() {
        Log = base.Log;
        Log.LogInfo("Loading");
        enable = Config.Bind("", "Enable", true, "Enable MIDNIGHT_RUNNER");
        dofOpts.Configure(Config, "A.DepthOfField");
        vhsOpts.Configure(Config, "A.VHS");
        engineOpts.Configure(Config, "B.Engine");
        gimmiOpts.Configure(Config, "Z.GPUInstancerMapMagicIntegration");
        gitsOpts.Configure(Config, "Z.GPUInstancerMapMagicIntegration.TerrainSettings");
        if (!enable.Value) {
            Log.LogInfo("Disabling");
            return;
        }
        cfg.Consume(dofOpts, vhsOpts, gimmiOpts, gitsOpts, engineOpts);
        Log.LogInfo("Configured");
        harmony = new("neonflames.midnight_runner");
        if (cfg.vhsEnable) harmony.PatchAll(typeof(VHS_Patch));
        if (cfg.dofEnable) harmony.PatchAll(typeof(DOF_Patch));
        AddComponent<MRLive>();
        Log.LogInfo("Loaded");
    }
}
