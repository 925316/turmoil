using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace tmmod;

// All game types are reached via runtime reflection: no compile-time reference to
// Assembly-CSharp or Turmoil, so no stub projects are needed.
internal static class Game
{
    internal const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static Assembly _cs;
    private static Assembly _tl;

    internal static Assembly Cs => _cs ??= Assembly.Load("Assembly-CSharp");
    internal static Assembly Tl => _tl ??= Assembly.Load("Turmoil");

    internal static Type CsType(string name) => Cs.GetType(name, true);
    internal static Type TlType(string name) => Tl.GetType(name, true);

    internal static bool IsMultiplayer =>
        (bool)CsType("SceneSwitcher").GetProperty("IsMultiplayer", F).GetValue(null);

    // Level.CheatMoney goes through the game's own replay-aware code path.
    internal static void AddLevelMoney(int amount)
    {
        Type scriptType = CsType("LevelScript");
        Component script = UnityEngine.Object.FindObjectOfType(scriptType) as Component;
        if (script == null)
        {
            return;
        }

        object level = scriptType.GetField("level", F).GetValue(script);
        Type fix32 = TlType("Turmoil.Fix32");
        object value = Activator.CreateInstance(fix32, amount);
        level.GetType().GetMethod("CheatMoney", F, null, new[] { fix32 }, null)
            .Invoke(level, new[] { value });
    }
}

internal static class OilLock
{
    // Company.OilPrice = BaseOilPrice + OilPriceIncrement; the sim rewrites both every
    // tick, so the lock is a per-tick overwrite after the two mutating ticks.
    public static void Apply(object level)
    {
        object data = level.GetType().GetProperty("Data", Game.F).GetValue(level);
        IEnumerable companies = (IEnumerable)data.GetType().GetProperty("Companies", Game.F).GetValue(data);
        Type fix32 = Game.TlType("Turmoil.Fix32");
        object target = Activator.CreateInstance(fix32, OilPriceLockPatch.Target);
        object zero = Activator.CreateInstance(fix32, 0f);
        foreach (object company in companies)
        {
            Type t = company.GetType();
            t.GetProperty("BaseOilPrice", Game.F).SetValue(company, target);
            t.GetProperty("OilPriceIncrement", Game.F).SetValue(company, zero);
        }
    }
}

[HarmonyPatch]
internal static class OilPriceTickPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(Game.TlType("Turmoil.Level"), "DoCompanyPriceTick");

    private static void Postfix(object __instance)
    {
        if (OilPriceLockPatch.Enabled)
        {
            OilLock.Apply(__instance);
        }
    }
}

// Gas sales bump OilPriceIncrement after the price tick; overwrite again so the
// locked price survives until the next tick's price rewrite.
[HarmonyPatch]
internal static class OilGasTickPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(Game.TlType("Turmoil.Level"), "DoSellCompanyGasTick");

    private static void Postfix(object __instance)
    {
        if (OilPriceLockPatch.Enabled)
        {
            OilLock.Apply(__instance);
        }
    }
}

internal static class OilPriceLockPatch
{
    internal static bool Enabled;
    internal static float Target = 3.5f;
}
