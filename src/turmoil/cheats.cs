using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace tmmod;

// Fix32 = value * 65536 (int raw), Fix64 = value * 2^32 (long raw).
internal static class Fx
{
    private static Type _fix32;
    private static Type _fix64;
    private static FieldInfo _fix32Raw;
    private static FieldInfo _fix64Raw;

    private static Type Fix32Type => _fix32 ??= Game.TlType("Turmoil.Fix32");
    private static Type Fix64Type => _fix64 ??= Game.TlType("Turmoil.Fix64");
    private static FieldInfo Fix32Raw => _fix32Raw ??= Fix32Type.GetField("m_rawValue", Game.F);
    private static FieldInfo Fix64Raw => _fix64Raw ??= Fix64Type.GetField("m_rawValue", Game.F);

    internal static double ToDouble(object fix32) => (int)Fix32Raw.GetValue(fix32) / 65536.0;
    internal static double ToDouble64(object fix64) => (long)Fix64Raw.GetValue(fix64) / 4294967296.0;

    internal static object FromDouble(double value)
    {
        object box = Activator.CreateInstance(Fix32Type);
        Fix32Raw.SetValue(box, (int)Math.Round(value * 65536.0));
        return box;
    }

    internal static object FromRaw32(int raw)
    {
        object box = Activator.CreateInstance(Fix32Type);
        Fix32Raw.SetValue(box, raw);
        return box;
    }

    internal static int Raw32(object fix32) => (int)Fix32Raw.GetValue(fix32);

    internal static object Mul32(object fix32, double factor) => FromDouble(ToDouble(fix32) * factor);

    internal static object Mul64(object fix64, double factor)
    {
        object box = Activator.CreateInstance(Fix64Type);
        double v = ToDouble64(fix64) * factor;
        if (v > long.MaxValue / 4294967296.0)
        {
            v = long.MaxValue / 4294967296.0;
        }

        Fix64Raw.SetValue(box, (long)Math.Round(v * 4294967296.0));
        return box;
    }
}

internal static class Sim
{
    private static PropertyInfo _dataProp;
    private static PropertyInfo _configProp;

    internal static object Level()
    {
        Type scriptType = Game.CsType("LevelScript");
        Component script = UnityEngine.Object.FindObjectOfType(scriptType) as Component;
        if (script == null)
        {
            return null;
        }

        return scriptType.GetField("level", Game.F).GetValue(script);
    }

    internal static object Data(object level)
    {
        _dataProp ??= level.GetType().GetProperty("Data", Game.F);
        return _dataProp.GetValue(level);
    }

    internal static object Config(object level)
    {
        _configProp ??= level.GetType().GetProperty("Config", Game.F);
        return _configProp.GetValue(level);
    }

    internal static IEnumerable Items(object data, string property)
    {
        return (IEnumerable)data.GetType().GetProperty(property, Game.F).GetValue(data);
    }
}

internal static class LevelCheats
{
    internal static string MaxAllPipes()
    {
        object level = RequireLevel();
        object data = Sim.Data(level);
        object config = Sim.Config(level);
        int maxTier = ((Array)config.GetType().GetField("PipeUnitPrice", Game.F).GetValue(config)).Length - 1;
        int count = 0;
        foreach (object pipe in Sim.Items(data, "Pipes"))
        {
            Type t = pipe.GetType();
            if (!(bool)t.GetProperty("Done", Game.F).GetValue(pipe))
            {
                continue;
            }

            t.GetProperty("Tier", Game.F).SetValue(pipe, maxTier);
            t.GetProperty("Upgrading", Game.F).SetValue(pipe, false);
            count++;
        }

        return "pipes maxed: " + count;
    }

    internal static string MaxRigSpeed()
    {
        object level = RequireLevel();
        object data = Sim.Data(level);
        object config = Sim.Config(level);
        int maxSpeed = ((Array)config.GetType().GetField("RigSpeedPrice", Game.F).GetValue(config)).Length;
        int count = 0;
        foreach (object elbow in Sim.Items(data, "Elbows"))
        {
            elbow.GetType().GetProperty("SpeedTier", Game.F).SetValue(elbow, maxSpeed);
            count++;
        }

        return "rig speed maxed: " + count;
    }

    internal static string MassiveWells(double radiusFactor)
    {
        return ScaleWells(radiusFactor, radiusFactor * radiusFactor, true);
    }

    internal static string RichWells(double oilFactor)
    {
        return ScaleWells(1.0, oilFactor, false);
    }

    private static string ScaleWells(double radiusFactor, double oilFactor, bool reshape)
    {
        object level = RequireLevel();
        object data = Sim.Data(level);
        int count = 0;
        foreach (object well in Sim.Items(data, "Wells"))
        {
            Type t = well.GetType();
            ScaleContainerProp(well, "Supply", oilFactor);
            ScaleContainerProp(well, "Storage", oilFactor);
            if (reshape)
            {
                ReshapeWell(well, t, radiusFactor);
            }

            FireWellUpdated(well, t);
            count++;
        }

        return (reshape ? "wells grown x" + radiusFactor + " (oil x" + oilFactor + "): " : "wells enriched x" + oilFactor + ": ") + count;
    }

    private static void ScaleContainerProp(object well, string property, double factor)
    {
        PropertyInfo prop = well.GetType().GetProperty(property, Game.F);
        object box = prop.GetValue(well);
        Type containerType = box.GetType();
        containerType.GetField("Oil", Game.F).SetValue(box, Fx.Mul64(containerType.GetField("Oil", Game.F).GetValue(box), factor));
        containerType.GetField("Gas", Game.F).SetValue(box, Fx.Mul64(containerType.GetField("Gas", Game.F).GetValue(box), factor));
        prop.SetValue(well, box);
    }

    private static void ReshapeWell(object well, Type wellType, double factor)
    {
        object pos = wellType.GetProperty("Position", Game.F).GetValue(well);
        Type posType = pos.GetType();
        int posI = Fx.Raw32(posType.GetField("I", Game.F).GetValue(pos));
        int posJ = Fx.Raw32(posType.GetField("J", Game.F).GetValue(pos));

        PropertyInfo cornersProp = wellType.GetProperty("Corners", Game.F);
        IEnumerable corners = (IEnumerable)cornersProp.GetValue(well);
        Type posFieldType = posType.GetField("I", Game.F).FieldType;
        Type listType = cornersProp.PropertyType;
        IList newList = (IList)Activator.CreateInstance(listType);
        foreach (object corner in corners)
        {
            int i = posI + (int)Math.Round((Fx.Raw32(posType.GetField("I", Game.F).GetValue(corner)) - posI) * factor);
            int j = posJ + (int)Math.Round((Fx.Raw32(posType.GetField("J", Game.F).GetValue(corner)) - posJ) * factor);
            object scaled = Activator.CreateInstance(posType, Fx.FromRaw32(i), Fx.FromRaw32(j));
            newList.Add(scaled);
        }

        cornersProp.SetValue(well, newList);

        PropertyInfo radiusProp = wellType.GetProperty("Radius", Game.F);
        radiusProp.SetValue(well, Fx.Mul32(radiusProp.GetValue(well), factor));
        PropertyInfo radiusSqProp = wellType.GetProperty("RadiusSquared", Game.F);
        radiusSqProp.SetValue(well, Fx.Mul32(radiusSqProp.GetValue(well), factor * factor));
    }

    private static void FireWellUpdated(object well, Type wellType)
    {
        FieldInfo evtField = wellType.GetField("WellShapeUpdated", Game.F);
        object evt = evtField.GetValue(well);
        if (evt == null)
        {
            return;
        }

        Type argsType = evtField.FieldType.GetGenericArguments()[0];
        object args = Activator.CreateInstance(argsType, well);
        ((Delegate)evt).DynamicInvoke(well, args);
    }

    internal static string MaxEverything()
    {
        string pipes;
        try { pipes = MaxAllPipes(); } catch (Exception e) { pipes = "pipes failed: " + e.Message; }
        string rig;
        try { rig = MaxRigSpeed(); } catch (Exception e) { rig = "rig failed: " + e.Message; }
        return pipes + "; " + rig;
    }

    private static object RequireLevel()
    {
        object level = Sim.Level();
        if (level == null)
        {
            throw new InvalidOperationException("no level loaded");
        }

        return level;
    }
}

// Flow rates reset with every level (fresh Config); apply once per Config instance,
// and restore originals when the boost is turned off so on/off (or on/off/on) never
// compounds the multiplier.
internal static class FlowBoost
{
    internal static bool Enabled;
    internal static float Factor = 5f;

    private static readonly string[] Names = { "PipeOilTransfer", "PipeGasTransfer", "WagonTransfer", "ElbowOilStorage", "ElbowGasStorage", "RigOilCapacity" };

    private static object _boostedConfig;        // the LevelConfig instance currently boosted
    private static float _boostedFactor;           // factor used to build _boostedConfig
    private static Dictionary<string, object> _saved; // backups of pre-boost values

    internal static string Apply(object config)
    {
        if (!Enabled || config == null)
        {
            return null;
        }

        // Same config already boosted with the same factor: do nothing (idempotent).
        if (ReferenceEquals(config, _boostedConfig) && _boostedFactor == Factor)
        {
            return null;
        }

        // Different factor on the same config, or boost lost track: restore first.
        if (_boostedConfig != null && ReferenceEquals(config, _boostedConfig))
        {
            Restore(config);
        }

        // New config instance: boost it and remember what we changed.
        if (!ReferenceEquals(config, _boostedConfig))
        {
            _saved = new Dictionary<string, object>();
            foreach (string name in Names)
            {
                FieldInfo f = config.GetType().GetField(name, Game.F);
                if (f == null) continue;
                object value = f.GetValue(config);
                if (value is Array a)
                {
                    _saved[name] = a.Clone();
                }
                else
                {
                    _saved[name] = value;
                }
            }

            FieldInfo speedCap = config.GetType().GetField("WagonMaxTransferSpeed", Game.F);
            if (speedCap != null)
            {
                _saved["WagonMaxTransferSpeed"] = speedCap.GetValue(config);
            }
        }

        // Apply the boost to the config (arrays + scalars).
        Type t = config.GetType();
        foreach (string name in Names)
        {
            FieldInfo item = t.GetField(name, Game.F);
            if (item == null) continue;
            object value = item.GetValue(config);
            if (value is Array array)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    array.SetValue(Fx.Mul32(array.GetValue(i), Factor), i);
                }
            }
            else
            {
                item.SetValue(config, Fx.Mul32(value, Factor));
            }
        }

        FieldInfo speedField = t.GetField("WagonMaxTransferSpeed", Game.F);
        if (speedField != null)
        {
            speedField.SetValue(config, Fx.Mul32(speedField.GetValue(config), Factor));
        }

        _boostedConfig = config;
        _boostedFactor = Factor;
        return "flow boosted x" + Factor;
    }

    internal static void SetEnabled(bool on)
    {
        if (on == Enabled)
        {
            return;
        }

        // Turning off: undo the boost on the instance we touched, if still tracked.
        if (!on && _boostedConfig != null)
        {
            Restore(_boostedConfig);
            _boostedConfig = null;
            _saved = null;
        }

        Enabled = on;
    }

    internal static void ResetApplied()
    {
        if (_boostedConfig != null && _saved != null)
        {
            Restore(_boostedConfig);
        }

        _boostedConfig = null;
        _saved = null;
    }

    private static void Restore(object config)
    {
        if (config == null || _saved == null)
        {
            return;
        }

        Type t = config.GetType();
        if (_saved.TryGetValue("WagonMaxTransferSpeed", out object speedSaved))
        {
            FieldInfo speedCap = t.GetField("WagonMaxTransferSpeed", Game.F);
            if (speedCap != null)
            {
                speedCap.SetValue(config, speedSaved);
            }
        }

        foreach (string name in Names)
        {
            if (!_saved.TryGetValue(name, out object orig))
            {
                continue;
            }

            FieldInfo item = t.GetField(name, Game.F);
            if (item == null) continue;
            object current = item.GetValue(config);
            if (orig is Array origArray && current is Array currArray)
            {
                Array.Copy(origArray, currArray, Math.Min(origArray.Length, currArray.Length));
            }
            else
            {
                item.SetValue(config, orig);
            }
        }
    }
}

internal static class ConfigGetterPatch
{
    [ThreadStatic] private static bool _inPostfix;
    private static PropertyInfo _configProp;

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(Game.TlType("Turmoil.Level"), "get_Config");
    }

    private static void Postfix(object __instance)
    {
        // Re-entrancy guard: the patch runs on every get_Config, and FlowBoost used to
        // call the property getter again from here, which re-entered this postfix and
        // overflowed the stack. While we are inside, just return for nested calls.
        if (_inPostfix)
        {
            return;
        }

        if (!FlowBoost.Enabled)
        {
            return;
        }

        _inPostfix = true;
        try
        {
            _configProp ??= __instance.GetType().GetProperty("Config", Game.F);
            object config = _configProp.GetValue(__instance);
            string result = FlowBoost.Apply(config);
            if (result != null) MainLogger.Log(result);
        }
        catch (Exception ex)
        {
            FlowBoost.SetEnabled(false);
            MainLogger.Warn("flow boost failed, disabled: " + ex.Message);
        }
        finally
        {
            _inPostfix = false;
        }
    }
}

internal static class MainLogger
{
    private static BepInEx.Logging.ManualLogSource _source;

    internal static void Bind(BepInEx.Logging.ManualLogSource source)
    {
        _source = source;
    }

    internal static void Log(string message)
    {
        _source?.LogInfo(message);
    }

    internal static void Warn(string message)
    {
        _source?.LogWarning(message);
    }
}
