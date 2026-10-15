using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace tmmod;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.tmmod.plugin";
    private const string PluginName = "tmmod";
    private const string PluginVersion = "1.0.0";

    private ConfigEntry<int> _levelMoneyAmount;
    private ConfigEntry<float> _oilPrice;
    private ConfigEntry<bool> _oilLockOnStart;
    private ConfigEntry<float> _windowX;
    private ConfigEntry<float> _windowY;
    private ConfigEntry<float> _flowFactor;
    private ConfigEntry<float> _wellRadiusFactor;
    private ConfigEntry<float> _wellOilFactor;


    private bool _visible = true;
    private Rect _windowRect;
    private string _oilPriceText = "3.5";
    private string _flowFactorText = "5";
    private string _wellRadiusText = "3";
    private string _wellOilText = "10";
    private string _status = "";

    private void Awake()
    {
        try
        {
            _levelMoneyAmount = Config.Bind("Money", "LevelAmount", 25000, "In-level money added per press.");
            _oilPrice = Config.Bind("Oil", "LockPrice", 3.5f, "Oil price while the lock is active.");
            _oilLockOnStart = Config.Bind("Oil", "LockOnStart", false, "Start with the oil price lock enabled.");
            _windowX = Config.Bind("Gui", "WindowX", 100f, "GUI window X position.");
            _windowY = Config.Bind("Gui", "WindowY", 100f, "GUI window Y position.");
            _flowFactor = Config.Bind("Boost", "FlowFactor", 5f, "Pipe/wagon transfer speed multiplier.");
            _wellRadiusFactor = Config.Bind("Boost", "WellRadiusFactor", 3f, "Well radius multiplier.");
            _wellOilFactor = Config.Bind("Boost", "WellOilFactor", 10f, "Well oil/gas amount multiplier.");

            OilPriceLockPatch.Enabled = _oilLockOnStart.Value;
            OilPriceLockPatch.Target = _oilPrice.Value;
            FlowBoost.Factor = _flowFactor.Value;
            MainLogger.Bind(Logger);
            _oilPriceText = _oilPrice.Value.ToString("0.0#");
            _flowFactorText = _flowFactor.Value.ToString("0.##");
            _wellRadiusText = _wellRadiusFactor.Value.ToString("0.##");
            _wellOilText = _wellOilFactor.Value.ToString("0.##");
            _windowRect = new Rect(_windowX.Value, _windowY.Value, 300f, 200f);

            new HarmonyLib.Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Press Insert to toggle the GUI.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Awake failed: " + ex);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Insert))
        {
            _visible = !_visible;
            if (!_visible)
            {
                _windowX.Value = _windowRect.x;
                _windowY.Value = _windowRect.y;
            }
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        _windowRect = GUI.Window(0x746D6D, _windowRect, DrawWindow, "Turmoil  [Insert]");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+" + _levelMoneyAmount.Value + " Money"))
        {
            RunSafe("money", () =>
            {
                RequireSingleplayer();
                Game.AddLevelMoney(_levelMoneyAmount.Value);
            });
        }
        bool lockOn = GUILayout.Toggle(OilPriceLockPatch.Enabled, "Lock", GUILayout.Width(48f));
        _oilPriceText = GUILayout.TextField(_oilPriceText, GUILayout.Width(40f));
        GUILayout.EndHorizontal();

        if (lockOn != OilPriceLockPatch.Enabled)
        {
            OilPriceLockPatch.Enabled = lockOn;
            OilPriceLockPatch.Target = ParseFloat(_oilPriceText, _oilPrice.Value);
            _status = lockOn ? "oil lock ON @ " + OilPriceLockPatch.Target : "oil lock OFF";
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Max Pipes"))
        {
            RunSafe("pipes", () => LevelCheats.MaxAllPipes());
        }
        if (GUILayout.Button("Max Rig Speed"))
        {
            RunSafe("rig", () => LevelCheats.MaxRigSpeed());
        }
        if (GUILayout.Button("Max All"))
        {
            RunSafe("maxall", () => LevelCheats.MaxEverything());
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        bool flowOn = GUILayout.Toggle(FlowBoost.Enabled, "Flow x", GUILayout.Width(60f));
        _flowFactorText = GUILayout.TextField(_flowFactorText, GUILayout.Width(36f));
        GUILayout.Label("Well R", GUILayout.Width(42f));
        _wellRadiusText = GUILayout.TextField(_wellRadiusText, GUILayout.Width(30f));
        GUILayout.Label("Oil x", GUILayout.Width(34f));
        _wellOilText = GUILayout.TextField(_wellOilText, GUILayout.Width(34f));
        GUILayout.EndHorizontal();

        if (flowOn != FlowBoost.Enabled)
        {
            FlowBoost.Factor = ParseFloat(_flowFactorText, _flowFactor.Value);
            _flowFactor.Value = FlowBoost.Factor;
            FlowBoost.SetEnabled(flowOn);
            if (flowOn)
            {
                FlowBoost.ResetApplied();
            }

            _status = flowOn ? "flow boost ON x" + FlowBoost.Factor : "flow boost OFF";
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Grow Wells"))
        {
            RunSafe("grow", () => LevelCheats.MassiveWells(ParseFloat(_wellRadiusText, _wellRadiusFactor.Value)));
        }
        if (GUILayout.Button("Enrich Wells"))
        {
            RunSafe("enrich", () => LevelCheats.RichWells(ParseFloat(_wellOilText, _wellOilFactor.Value)));
        }
        GUILayout.EndHorizontal();

        GUILayout.Label(_status);
        GUI.DragWindow();
    }

    private static float ParseFloat(string text, float fallback)
    {
        return float.TryParse(text.Trim(), out float value) ? value : fallback;
    }

    private static void RequireSingleplayer()
    {
        if (Game.IsMultiplayer)
        {
            throw new InvalidOperationException("multiplayer: cheats disabled");
        }
    }

    private void RunSafe(string what, Action action)
    {
        try
        {
            action();
            _status = what + ": done";
            Logger.LogInfo(what + ": done");
        }
        catch (Exception ex)
        {
            _status = what + ": FAILED";
            Logger.LogWarning(what + " failed: " + ex);
        }
    }

    private void RunSafe(string what, Func<string> action)
    {
        try
        {
            string result = action();
            _status = result;
            Logger.LogInfo(what + ": " + result);
        }
        catch (Exception ex)
        {
            _status = what + ": FAILED";
            Logger.LogWarning(what + " failed: " + ex);
        }
    }
}
