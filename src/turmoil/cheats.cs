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

// Flow rates reset with every level (fresh Config); re-apply once per Config instance.
internal static class FlowBoost
{
    internal static bool Enabled;
    internal static float Factor = 5f;
    private static object _applied;

    internal static string Apply(object level, PropertyInfo configProp)
    {
        if (!Enabled)
        {
            return null;
        }

        object config = configProp.GetValue(level);
        if (ReferenceEquals(config, _applied))
        {
            return null;
        }

        _applied = config;
        Type t = config.GetType();
        foreach (string name in new[] { "PipeOilTransfer", "PipeGasTransfer", "WagonTransfer", "ElbowOilStorage", "ElbowGasStorage", "RigOilCapacity" })
        {
            FieldInfo item = t.GetField(name, Game.F);
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

        FieldInfo speedCap = t.GetField("WagonMaxTransferSpeed", Game.F);
        if (speedCap != null)
        {
            speedCap.SetValue(config, Fx.Mul32(speedCap.GetValue(config), Factor));
        }

        return "flow boosted x" + Factor;
    }

    internal static void ResetApplied()
    {
        _applied = null;
    }
}

// Config is fetched constantly by the sim; postfix is the earliest reliable
// per-level hook without chasing the LevelScript init order.
[HarmonyPatch]
internal static class ConfigGetterPatch
{
    private static PropertyInfo _configProp;

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(Game.TlType("Turmoil.Level"), "get_Config");
    }

    private static void Postfix(object __instance)
    {
        if (!FlowBoost.Enabled)
        {
            return;
        }

        _configProp ??= __instance.GetType().GetProperty("Config", Game.F);
        try
        {
            string result = FlowBoost.Apply(__instance, _configProp);
            if (result != null)
            {
                MainLogger.Log(result);
            }
        }
        catch (Exception ex)
        {
            FlowBoost.Enabled = false;
            MainLogger.Warn("flow boost failed, disabled: " + ex.Message);
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
