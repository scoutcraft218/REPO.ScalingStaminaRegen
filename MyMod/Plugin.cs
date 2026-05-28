using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MyMod.Patches;

namespace MyMod
{
    [BepInPlugin(MOD_GUID, MOD_Name, MOD_Version)]
    public class Plugin : BaseUnityPlugin
    {
        //private const string MOD_GUID = "ModName.Username.Top-Level-Domain";
        //private const string MOD_Name = "Username.ModName";
        //private const string MOD_Version = "0.1.0";

        private const string MOD_GUID = "scoutcraft.ScalingStaminaRegen";
        private const string MOD_Name = "Scaling Stamina Regen";
        private const string MOD_Version = "1.0.2";

        private readonly Harmony _harmony = new Harmony(MOD_GUID);
        internal static ManualLogSource mls = BepInEx.Logging.Logger.CreateLogSource(MOD_GUID);

        internal static ConfigEntry<float> BaseStaminaRegen = null!;
        internal static ConfigEntry<float> SprintRechargeTime = null!;
        internal static ConfigEntry<float> AgilityPerUpgrade = null!;
        internal static ConfigEntry<float> MaxAgilityCap = null!;

        internal static ConfigEntry<bool> ToggleAgilityTimer = null!;
        internal static ConfigEntry<bool> ToggleDisableAgility = null!;
        internal static ConfigEntry<bool> ToggleRecalculatePerFrame = null!;
        internal static ConfigEntry<bool> ToggleRecalculateInfo = null!;
        internal static ConfigEntry<bool> ToggleAgilityUncapped = null!;
        private void Awake()
        {
            BindConfiguration(); // get the config values
            _harmony.PatchAll();
            Logger.LogInfo((object)$"{((BaseUnityPlugin)this).Info.Metadata.GUID} v{((BaseUnityPlugin)this).Info.Metadata.Version} has loaded!");
        }

        public void BindConfiguration()
        {
            BaseStaminaRegen = Config.Bind<float>("Stamina Regen.Values", "BaseStaminaRegen", 2f, "What the base stamina regen is.");
            SprintRechargeTime = Config.Bind<float>("Stamina Regen.Values", "SprintRechargeTime", 1f, "How long it takes after sprinting for base stamina regen to reactivate.");
            AgilityPerUpgrade = Config.Bind<float>("Stamina Regen.Values", "AgilityPerUpgrade", 0.2f, "Additional stamina regen per second for each combined level of Stamina + Crouch Rest + Speed upgrades.");
            MaxAgilityCap = Config.Bind<float>("Stamina Regen.Values", "MaxAgilityCap", 50f, "What AgilityPerUpgrade will be capped at.");

            ToggleDisableAgility = Config.Bind<bool>("Stamina Regen.Toggle", "ToggleDisableAgility", false, "Disables the Agility bonus by fixing Agility at 0.");
            ToggleAgilityTimer = Config.Bind<bool>("Stamina Regen.Toggle", "ToggleAgilityTimer", false, "Whether Agility should activate during the Sprint timer.");
            ToggleRecalculatePerFrame = Config.Bind<bool>("Stamina Regen.Toggle", "ToggleRecalculatePerFrame", false, "If Agility should be recalculated every frame. (suppresses most Agility debug logs)");
            ToggleRecalculateInfo = Config.Bind<bool>("Stamina Regen.Toggle", "ToggleRecalculateInfo", false, "If Agility info should be printed in level \"Info\" of Logger. (it's Debug by default)");
            ToggleAgilityUncapped = Config.Bind<bool>("Stamina Regen.Toggle", "ToggleAgilityUncapped", false, "If Agility should cap out at MaxAgilityCap");
        }

        public static void SendLog(string msg)
        {
            mls.LogInfo(msg);
        }

    }
}
