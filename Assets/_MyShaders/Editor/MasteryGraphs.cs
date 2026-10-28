using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static WaterExerciseSetup;

public static class MasteryGraphs
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    public struct Port
    {
        public object node;
        public int slot;
        public Port(object node, int slot) { this.node = node; this.slot = slot; }
    }
    public sealed class Graph
    {
        public object data;
        object[] blocks;
        public Graph(bool transparent = false, bool clipping = false, bool twoSided = false)
        {
            data = New("UnityEditor.ShaderGraph.GraphData");
            Call(data, "AddContexts");
            Set(data, "path", "Shander");
            var target = New("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
            Call(target, "TrySetActiveSubTarget", T("UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget"));
            EnumValue(target, "surfaceType", transparent ? "Transparent" : "Opaque");
            if (twoSided) EnumValue(target, "renderFace", "Both");
            Set(target, "alphaClip", clipping);
            var targets = Array.CreateInstance(T("UnityEditor.ShaderGraph.Target"), 1);
            targets.SetValue(target, 0);
            var names = new[] { "VertexDescription.Position", "VertexDescription.Normal", "VertexDescription.Tangent", "SurfaceDescription.BaseColor", "SurfaceDescription.NormalTS", "SurfaceDescription.Metallic", "SurfaceDescription.Smoothness", "SurfaceDescription.Emission", "SurfaceDescription.Occlusion", "SurfaceDescription.Alpha", "SurfaceDescription.AlphaClipThreshold" };
            var descriptors = Array.CreateInstance(T("UnityEditor.ShaderGraph.BlockFieldDescriptor"), names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var parts = names[i].Split('.');
                descriptors.SetValue(T("UnityEditor.ShaderGraph.BlockFields+" + parts[0]).GetField(parts[1], F).GetValue(null), i);
            }
            Call(data, "InitializeOutputs", targets, descriptors);
            blocks = ((IEnumerable)Get(Get(data, "fragmentContext"), "blocks")).Cast<object>().Select(b => Get(b, "value")).ToArray();
            Set(Get(data, "fragmentContext"), "position", new Vector2(2100, 100));
            Set(Get(data, "vertexContext"), "position", new Vector2(2100, -220));
        }
        public static void EnumValue(object o, string name, string value)
        {
            var property = o.GetType().GetProperty(name, F);
            property.SetValue(o, Enum.Parse(property.PropertyType, value));
        }
        public Port N(string type, int output, float x, float y) => new Port(Node(data, type, x, y), output);
        public Port Float(string label, string reference, float value, float x, float y, bool slider = false)
        {
            var p = Property(data, "Vector1ShaderProperty", label, reference, value);
            if (slider) { EnumValue(p, "floatType", "Slider"); Set(p, "rangeValues", new Vector2(0, 1)); }
            return new Port(PropertyNode(data, p, x, y), 0);
        }
        public Port Color(string label, string reference, Color value, float x, float y, bool hdr = true)
        {
            var p = Property(data, "ColorShaderProperty", label, reference, value);
            if (hdr) EnumValue(p, "colorMode", "HDR");
            return new Port(PropertyNode(data, p, x, y), 0);
        }
        public Port Vector(string label, string reference, Vector2 value, float x, float y)
        {
            var p = Property(data, "Vector2ShaderProperty", label, reference, new Vector4(value.x, value.y, 0, 0));
            return new Port(PropertyNode(data, p, x, y), 0);
        }
        public Port Constant(float value, float x, float y)
        {
            var p = N("Vector1Node", 0, x, y); FloatSlot(p.node, 1, value); return p;
        }
        public void Wire(Port source, Port destination, int input) => Edge(data, source.node, source.slot, destination.node, input);
        public Port Unary(string type, Port input, float x, float y)
        {
            var n = N(type, 1, x, y); Wire(input, n, 0); return n;
        }
        public Port Binary(string type, Port a, Port b, float x, float y)
        {
            var n = N(type, 2, x, y); Wire(a, n, 0); Wire(b, n, 1); return n;
        }
        public Port Mul(Port a, Port b, float x, float y) => Binary("MultiplyNode", a, b, x, y);
        public Port Add(Port a, Port b, float x, float y) => Binary("AddNode", a, b, x, y);
        public Port Noise(Port uv, float scale, float x, float y, bool cells = false)
        {
            var n = N(cells ? "VoronoiNode" : "NoiseNode", cells ? 3 : 2, x, y);
            Wire(uv, n, 0); FloatSlot(n.node, cells ? 2 : 1, scale); return n;
        }
        public Port Fresnel(Port power, float x, float y)
        {
            var n = N("FresnelNode", 3, x, y); Wire(power, n, 2); return n;
        }
        public Port Flow(Port time, Port speed, float x, float y)
        {
            var offset = Mul(time, speed, x, y);
            var tiling = N("TilingAndOffsetNode", 3, x + 280, y); Wire(offset, tiling, 2); return tiling;
        }
        public Port Pulse(Port time, Port speed, float x, float y)
        {
            var mul = Mul(time, speed, x, y);
            var sine = Unary("SineNode", mul, x + 260, y);
            var remap = N("RemapNode", 3, x + 520, y); Wire(sine, remap, 0); return remap;
        }
        public Port Smooth(Port noise, float low, float high, float x, float y)
        {
            var n = N("SmoothstepNode", 3, x, y);
            Wire(Constant(low, x - 260, y + 230), n, 0);
            Wire(Constant(high, x - 260, y + 360), n, 1);
            Wire(noise, n, 2); return n;
        }
        public void Output(string block, Port value)
        {
            var target = blocks.First(b => (string)Get(b, "name") == "SurfaceDescription." + block);
            Edge(data, value.node, value.slot, target, 0);
        }
        public void Default(string block, float value)
        {
            var target = blocks.FirstOrDefault(b => (string)Get(b, "name") == "SurfaceDescription." + block);
            if (target != null) FloatSlot(target, 0, value);
        }
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Call(data, "ValidateGraph");
            var serialize = T("UnityEditor.ShaderGraph.Serialization.MultiJson").GetMethod("Serialize", F);
            File.WriteAllText(path, (string)serialize.Invoke(null, new[] { data }));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Invalid graph: " + path);
        }
    }

    public static void CreateAll()
    {
        PulseGlow(); Dissolve(); ForceField(); Hologram(); TidePortal();
    }
    static void PulseGlow()
    {
        var g = new Graph();
        var time = g.N("TimeNode", 0, -1000, -100);
        var speed = g.Float("Pulse Speed", "_PulseSpeed", 2, -1000, 180);
        var color = g.Color("Base Color", "_BaseColor", new Color(0.02f, 1.8f, 1.3f), 300, 350);
        var pulse = g.Pulse(time, speed, -650, -100);
        g.Output("Emission", g.Mul(pulse, color, 900, 0));
        g.Output("BaseColor", color);
        g.Default("Smoothness", 0.65f);
        g.Save(ShaderMasterySetup.Root + "/Level1/SG_PulseGlow.shadergraph");
    }
    static void Dissolve()
    {
        var g = new Graph(true, true);
        var uv = g.N("UVNode", 0, -1200, -150);
        var noise = g.Noise(uv, 9, -880, -150, true);
        var amount = g.Float("Dissolve Amount", "_DissolveAmount", 0.42f, -880, 200, true);
        var mask = g.Binary("StepNode", amount, noise, -470, -100);
        var width = g.Float("Edge Width", "_EdgeWidth", 0.055f, -880, 450);
        var next = g.Add(amount, width, -500, 400);
        var inner = g.Binary("StepNode", next, noise, -130, 300);
        var band = g.Binary("SubtractNode", mask, inner, 200, 200);
        var edgeColor = g.Color("Edge Color", "_EdgeColor", new Color(4f, 0.55f, 0.05f), 180, 550);
        var baseColor = g.Color("Base Color", "_BaseColor", new Color(0.3f, 0.4f, 0.5f), 850, -200, false);
        g.Output("BaseColor", baseColor);
        g.Output("Emission", g.Mul(band, edgeColor, 600, 200));
        g.Output("Alpha", mask);
        // A constant threshold is required: using the mask itself would leave clip(0) fragments.
        g.Default("AlphaClipThreshold", 0.5f);
        g.Default("Smoothness", 0.6f);
        g.Save(ShaderMasterySetup.Root + "/Level1/SG_Dissolve.shadergraph");
    }
    static void ForceField()
    {
        var g = new Graph(true);
        var time = g.N("TimeNode", 0, -1300, -200);
        var speed = g.Vector("Flow Speed", "_FlowSpeed", new Vector2(0.02f, 0.035f), -1300, 60);
        var uv = g.Flow(time, speed, -1020, -100);
        var density = g.Float("Cell Density", "_CellDensity", 12, -720, 400);
        var cells = g.Noise(uv, 12, -420, -100, true); g.Wire(density, cells, 2);
        var threshold = g.Constant(0.48f, -400, 250);
        var cellLines = g.Binary("StepNode", threshold, cells, -60, -70);
        var power = g.Float("Rim Power", "_RimPower", 3, -730, 730);
        var rim = g.Fresnel(power, -390, 700);
        var small = g.Mul(cellLines, g.Constant(0.25f, -60, 270), 280, 0);
        var pattern = g.Add(rim, small, 620, 200);
        var mask = g.Unary("SaturateNode", pattern, 900, 200);
        var color = g.Color("Shield Color", "_ShieldColor", new Color(0.08f, 1.5f, 2.5f), 620, 550);
        g.Output("Emission", g.Mul(mask, color, 1250, 200));
        g.Output("Alpha", g.Add(g.Mul(mask, g.Constant(0.65f, 900, 660), 1250, 600), g.Constant(0.04f, 1250, 870), 1620, 600));
        g.Output("BaseColor", g.Color("Base Tint", "_BaseTint", new Color(0.015f, 0.08f, 0.12f), 1400, -180, false));
        g.Default("Smoothness", 0.8f);
        g.Save(ShaderMasterySetup.Root + "/Level2/SG_ForceField.shadergraph");
    }
    static void Hologram()
    {
        var g = new Graph(true);
        var time = g.N("TimeNode", 0, -1350, -100);
        var speed = g.Vector("Scan Speed", "_ScanSpeed", new Vector2(0, 0.12f), -1350, 180);
        var uv = g.Flow(time, speed, -1050, -100);
        var split = g.N("SplitNode", 2, -450, -100); g.Wire(uv, split, 0);
        var density = g.Float("Scan Density", "_ScanDensity", 45, -450, 250);
        var lines = g.Unary("FractionNode", g.Mul(split, density, -130, -100), 170, -100);
        var thin = g.Binary("StepNode", g.Constant(0.65f, 170, 180), lines, 470, -100);
        var power = g.Float("Rim Power", "_RimPower", 2.5f, -450, 650);
        var rim = g.Fresnel(power, -120, 650);
        var combined = g.Unary("SaturateNode", g.Add(thin, rim, 790, 150), 1100, 150);
        var glitchSpeed = g.Float("Glitch Speed", "_GlitchSpeed", 17, -1300, -600);
        var glitchTime = g.Mul(time, glitchSpeed, -1000, -600);
        var glitchSine = g.Unary("SineNode", glitchTime, -700, -600);
        var glitch = g.Binary("StepNode", g.Constant(0.94f, -700, -350), glitchSine, -400, -600);
        var strength = g.Float("Glitch Strength", "_GlitchStrength", 0.72f, -400, -1000, true);
        var dropout = g.Unary("OneMinusNode", g.Mul(glitch, strength, -100, -600), 200, -600);
        var pattern = g.Mul(combined, dropout, 1400, -150);
        var color = g.Color("Holo Color", "_HoloColor", new Color(0.05f, 0.85f, 2.6f), 790, 550);
        g.Output("Emission", g.Mul(pattern, color, 1450, 100));
        var opacity = g.Float("Opacity", "_Opacity", 0.48f, 1100, 800, true);
        g.Output("Alpha", g.Mul(pattern, opacity, 1500, 650));
        g.Output("BaseColor", g.Color("Base Tint", "_BaseTint", new Color(0.01f, 0.05f, 0.1f), 1500, -500, false));
        g.Default("Smoothness", 0.7f);
        g.Save(ShaderMasterySetup.Root + "/Level2/SG_Hologram.shadergraph");
    }
    static void TidePortal()
    {
        var g = new Graph(true, false, true);
        var time = g.N("TimeNode", 0, -1700, -300);
        var flowA = g.Vector("Tide Speed", "_TideSpeed", new Vector2(0.055f, -0.1f), -1700, -50);
        var uvA = g.Flow(time, flowA, -1400, -250);
        var noiseA = g.Noise(uvA, 11, -800, -250);
        var flowB = g.Vector("Counterflow", "_Counterflow", new Vector2(-0.08f, 0.04f), -1700, 600);
        var uvB = g.Flow(time, flowB, -1400, 450);
        var noiseB = g.Noise(uvB, 19, -800, 450);
        var turbulence = g.Mul(noiseA, noiseB, -450, -50);
        var veins = g.Smooth(turbulence, 0.15f, 0.32f, -100, -50);
        var power = g.Float("Rim Power", "_RimPower", 3.5f, -800, 1050);
        var rim = g.Fresnel(power, -450, 1000);
        var pattern = g.Add(veins, rim, 250, 150);
        var pulseSpeed = g.Float("Pulse Speed", "_PulseSpeed", 1.7f, -1400, -700);
        var pulse = g.Pulse(time, pulseSpeed, -1050, -650);
        var gentlePulse = g.Add(g.Mul(pulse, g.Constant(0.35f, -200, -500), 100, -650), g.Constant(0.65f, 100, -420), 420, -600);
        var animated = g.Mul(pattern, gentlePulse, 750, 0);
        var readiness = g.Float("Readiness", "_Readiness", 1, 400, 1000, true);
        var cold = g.Color("Dormant Color", "_DormantColor", new Color(0.03f, 0.08f, 0.15f), 400, 1250);
        var hot = g.Color("Active Color", "_ActiveColor", new Color(0.02f, 2.6f, 1.65f), 400, 1500);
        var color = g.N("LerpNode", 3, 780, 1000); g.Wire(cold, color, 0); g.Wire(hot, color, 1); g.Wire(readiness, color, 2);
        var intensity = g.Float("Energy", "_Energy", 1.6f, 780, 450);
        var energy = g.Mul(animated, intensity, 1100, 0);
        g.Output("Emission", g.Mul(energy, color, 1450, 200));
        g.Output("BaseColor", color);
        var coverage = g.Unary("SaturateNode", pattern, 1100, 800);
        var alpha = g.Add(g.Mul(coverage, g.Constant(0.45f, 1100, 1200), 1450, 800), g.Constant(0.18f, 1450, 1200), 1750, 800);
        g.Output("Alpha", alpha);
        g.Default("Smoothness", 0.85f);
        g.Save(ShaderMasterySetup.Root + "/Level3/SG_TideGate.shadergraph");
    }
}
