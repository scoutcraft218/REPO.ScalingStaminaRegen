using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MyMod.Patches
{
    [HarmonyPatch]
    public static class AgilityPatch
    {
        // config parameters
        internal static float BaseStaminaRegen; // default 2
        internal static float SprintRechargeTime;
        internal static float AgilityPerUpgrade;
        internal static float MaxAgilityCap;
        
        internal static bool ToggleDisableAgility;
        internal static bool ToggleAgilityTimer;
        internal static bool ToggleRecalculatePerFrame;
        internal static bool ToggleRecalculateInfo;
        internal static bool ToggleAgilityUncapped;

        // variables
        static float agilityRechargeTimer = 0f;
        static float agilityRechargeTimerStart = 0.2f;

        internal static float AgilityRegenBuff = 0f;
        internal static string? localSteamID;
        private static bool checkIDObtained = false;
        /*
         * about crouch rest
         *  - value is dynamic and stored in PlayerAvatar.upgradeCrouchRest
         *  - math and changes is done is PlayerAvatar.CrouchRestUpgrade()
         */

        private static void SetValueConfig()
        {
            BaseStaminaRegen = Plugin.BaseStaminaRegen.Value;
            SprintRechargeTime = Plugin.SprintRechargeTime.Value;
            AgilityPerUpgrade = Plugin.AgilityPerUpgrade.Value;
            MaxAgilityCap = Plugin.MaxAgilityCap.Value;

            ToggleDisableAgility = Plugin.ToggleDisableAgility.Value;
            ToggleAgilityTimer = Plugin.ToggleAgilityTimer.Value;
            ToggleRecalculatePerFrame = Plugin.ToggleRecalculatePerFrame.Value;
            ToggleRecalculateInfo = Plugin.ToggleRecalculateInfo.Value;
            ToggleAgilityUncapped = Plugin.ToggleAgilityUncapped.Value;
        }

        /* Finding right method for getting steamID()
         * 
         * StatsManager.Start() is incorrect. triggers at start of menu/game
         * PlayerAvatar.Start() triggers too many times
         * PlayerController.PlayerSetName() works but also triggers too many times
         * PlayerAvatar.LateStart() works but triggers too many times
         * DebugCommandHandler.Start() works but also triggers too many times, also initially null
         * 
         * perhaps: check for _steamID first assign
         * - SteamManager.OpenProfile()
         * 
         * Where is steamID stored?
         *  - PlayerAvatar.steamID
         *  - PlayerController.playerSteamID
         *  
         * When is it called?
         *  - PlayerAvatar.AddToStatsManagerRPC() > PlayerAvatar.AddToStatsManager()
         *  - PlayerController.PlayerSetName() > PlayerAvatar.AddToStatsManagerRPC()
         *  
         * Important
         *  - SemiFunc.PlayerGetSteamID(PlayerAvatar player)
         *  - SemiFunc.PlayerAvatarLocal()
         * 
         * nameof(PlayerAvatar.PlayerSetName)
         */

        [HarmonyPatch(typeof(PlayerController), "Start")] // probably the wrong method but whatever
        [HarmonyPostfix]
        public static void GetSteamID() // check PlayerAvatar.steamID
        {
            if (checkIDObtained) // if localSteamID has been obtained already
            {
                return;
            }
            Plugin.mls.LogDebug("Running GetSteamID()");

            // this is so weird, playerAvatar starts as nothing and then becomes null but that null can be accessed????
            PlayerAvatar playerAvatar = SemiFunc.PlayerAvatarLocal();
            try
            {
                localSteamID = SemiFunc.PlayerGetSteamID(playerAvatar);
            } catch (NullReferenceException)
            {
                Plugin.mls.LogDebug("playerAvatar is (probably) null, aborting GetSteamID()");
                return;
            }
            
            if (localSteamID == null) // if you didn't get it
            {
                Plugin.mls.LogDebug("localSteamID is null, aborting GetSteamID()");
                return;
            }

            // you got localSteamID
            Plugin.mls.LogDebug($"Success! GetSteamID() Player is {localSteamID}");

            checkIDObtained = true;
        }

        [HarmonyPatch(typeof(PlayerController), "Awake")]
        [HarmonyPostfix]
        public static void SetControllerValues(PlayerController __instance, ref float ___sprintRechargeAmount, ref float ___sprintRechargeTime)
        {
            // specifically for SetControllerValues, just check these values in case
            BaseStaminaRegen = Plugin.BaseStaminaRegen.Value;
            SprintRechargeTime = Plugin.SprintRechargeTime.Value;

            ___sprintRechargeAmount = BaseStaminaRegen;
            ___sprintRechargeTime = SprintRechargeTime;
            
            Plugin.mls.LogDebug("PlayerController values patched.");
        }


        [HarmonyPatch(typeof(GameDirector), "Start")]
        [HarmonyPostfix]
        private static void GameDirector_Start_Postfix()
        {
            UnityEngine.Object.FindAnyObjectByType<MonoBehaviour>().StartCoroutine(WaitAndActivate());
            
        }

        // i borrowed this from headclef-CharacterStats-1.0.0
        private static IEnumerator WaitAndActivate()
        {
            //Plugin.mls.LogDebug("SemiFunc run is " + RunManager.instance.levelCurrent);

            // wait until level generation is done
            while (!SemiFunc.LevelGenDone())
            {
                yield return new WaitForSeconds(0.5f);
            }

            // only activate in level or shop
            if (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop())
            {
                yield break;
            }
            
            yield return new WaitForSeconds(1f);
            Plugin.mls.LogDebug("Is Level or Shop, done WaitAndActivate()");
            RecalculateAgility();
            if (ToggleDisableAgility) printAgility(); // print it once if using this setting, just in case
        }

        /* Finding right method for calculating agility, relies on AreStatsReady right now
         * Runmanager.ChangeLevel() post fix triggers too early
         * SemiFunc.StatSyncAll() crashed NullReferenceException
         * perhaps:
         *  - SemiFunc.OnSceneSwitch()? or also SemiFunc.StatSyncAll()
         * 
         * there are probably 2 cases:
         * - check once a level starts, check if you use an upgrade
         * - check every frame, works if you artifically scale upgrades
         * 
         * SOLVED: check StatsManager directly (when rdy), since it's public variable
         * 
         */
        public static void RecalculateAgility()
        {
            SetValueConfig();

            if (ToggleDisableAgility) return;

            if (localSteamID == null)
            {
                if (!ToggleRecalculatePerFrame) Plugin.mls.LogDebug("localSteamID is null, aborting RecalculateAgility()");
                return;
            }

            int ValueCrouchRest = StatsManager.instance.playerUpgradeCrouchRest.GetValueOrDefault(localSteamID, 0);
            int ValueSpeed = StatsManager.instance.playerUpgradeSpeed.GetValueOrDefault(localSteamID, 0);
            int ValueStamina = StatsManager.instance.playerUpgradeStamina.GetValueOrDefault(localSteamID, 0);

            AgilityRegenBuff = Mathf.Min((ValueCrouchRest + ValueSpeed + ValueStamina) * AgilityPerUpgrade,
            ToggleAgilityUncapped ? float.MaxValue : MaxAgilityCap);
            
            if (!ToggleRecalculatePerFrame) printAgility();
        }

        private static void printAgility()
        {
            int ValueCrouchRest = StatsManager.instance.playerUpgradeCrouchRest.GetValueOrDefault(localSteamID, 0);
            int ValueSpeed = StatsManager.instance.playerUpgradeSpeed.GetValueOrDefault(localSteamID, 0);
            int ValueStamina = StatsManager.instance.playerUpgradeStamina.GetValueOrDefault(localSteamID, 0);

            LogLevel level = ToggleRecalculateInfo ? LogLevel.Info : LogLevel.Debug;
            Plugin.mls.Log(level, $"Recaluated AgilityRegenBuff: AgilityRegenBuff: {AgilityRegenBuff} + BaseStaminaRegen: {BaseStaminaRegen} = {AgilityRegenBuff + BaseStaminaRegen}");
            Plugin.mls.LogDebug($"Player {localSteamID} CrouchRest: {ValueCrouchRest}, Speed: {ValueSpeed}, Stamina: {ValueStamina}");
        }

        // every frame of PlayerController, do the following
        [HarmonyPatch(typeof(PlayerController), "Update")]
        [HarmonyPostfix]
        public static void PatchAgility(PlayerController __instance, ref float ___sprintRechargeTimer)
        {
            // don't patch anything if agility is disabled
            if (ToggleDisableAgility) return;

            // either calculate agility per frame OR calculate if AreStatsReady changes
            if (ToggleRecalculatePerFrame) RecalculateAgility();

            float EnergyCurrent = __instance.EnergyCurrent;
            float EnergyStart = __instance.EnergyStart;

            bool sprinting = __instance.sprinting;

            // Don't regen while sprinting
            if (sprinting)
            {
                if (ToggleAgilityTimer) // if using TriggerAgilityTimer
                {
                    agilityRechargeTimer = agilityRechargeTimerStart;// reset timer
                }
                return;
            }

            // Don't regen at max energy, nothing to regen
            if (EnergyCurrent >= EnergyStart) return;

            // Don't regen if sprint cooldown active and not using agility timer
            if (___sprintRechargeTimer > 0f && !ToggleAgilityTimer) return;

            // Don't regen if you are using TriggerAgilityTimer but agility cooldown is active
            if (ToggleAgilityTimer && agilityRechargeTimer > 0f)
            {
                agilityRechargeTimer -= Time.deltaTime;
                return;
            }

            // Regenerate energy, capped at max
            __instance.EnergyCurrent = Mathf.Min(EnergyCurrent + AgilityRegenBuff * Time.deltaTime, EnergyStart);
        }


        // if player has used an upgrade, increase AgilityRegenBuff
        [HarmonyPatch(typeof(PunManager), "UpdateCrouchRestRightAway")]
        [HarmonyPostfix]
        public static void CheckUpdatePlayerCrouchRest()
        {
            RecalculateAgility();
            int ValueCrouchRest = StatsManager.instance.playerUpgradeCrouchRest.GetValueOrDefault(localSteamID, 0);
            Plugin.mls.LogDebug($"Player {localSteamID} changed CrouchRest to {ValueCrouchRest} total.");
        }

        [HarmonyPatch(typeof(PunManager), "UpdateSprintSpeedRightAway")]
        [HarmonyPostfix]
        public static void CheckUpdateSprintSpeed()
        {
            RecalculateAgility();
            int ValueSpeed = StatsManager.instance.playerUpgradeSpeed.GetValueOrDefault(localSteamID, 0);
            Plugin.mls.LogDebug($"Player {localSteamID} changed SprintSpeed to {ValueSpeed} total.");
        }

        [HarmonyPatch(typeof(PunManager), "UpdateEnergyRightAway")]
        [HarmonyPostfix]
        public static void CheckUpdatePlayerEnergy()
        {
            RecalculateAgility();
            int ValueStamina = StatsManager.instance.playerUpgradeStamina.GetValueOrDefault(localSteamID, 0);
            Plugin.mls.LogDebug($"Player {localSteamID} changed Stamina to {ValueStamina} total.");
        }
    }
}

// epic human notes below

/*
* StatsManager is important
* PlayerController is slightly less important, track stamina?
* 
* how are upgrades handled?
* player upgrades are stored in statsManager.PlayerUpgradeXXX[_steadID]
* - for example: statsManager.playerUpgradeSpeed[_steamID] = valueOrDefault + num;
* 
* targets to receive in StatsManager: Dictionary <_steamID, value (base is 0)>
* - Dictionary <string, int> playerUpgradeCrouchRest
* - Dictionary <string, int> playerUpgradeSpeed
* - Dictionary <string, int> playerUpgradeStamina
* 
* there are 2 things that store the value that can be accessed:
* - StatManager.playerUpgradeCrouchRest[_steamID]
*   - 1st option prob easier, playerAvatar doesn't store name
* - playerAvatar.upgradeCrouchRest
*   - 2nd option is weird, tumble is stored in PlayerAvatar.PlayerTumble.tumbleLaunch
* 
* approach: client side
*  - a few parameters:
*      - BaseStaminaRegen = 2 (from 1)
*      - AgilityPerUpgrade = 0.2
*      - MaxStaminaRegen = 50 (or -1)
*  - before anything, change the BaseStaminaRegen through PlayerController.sprintRechargeAmount
*      - go to PlayerController.Update() or PlayerController.Start()
*      - set sprintRechargeAmount = BaseStaminaRegen
*  - at start() of something, pull value from these and store amount in int:
*      - ValueCrouchRest = StatManager.playerUpgradeCrouchRest[_steamID]
*      - ValueSpeed = StatManager.playerUpgradeSpeed[_steamID]
*      - ValueStamina = StatManager.playerUpgradeStamina[_steamID]
*      - probably get these at either PlayerController.Start() or StatsManager.Start()
*  - the player's stamina is managed in 2 places:
*      - PlayerController.FixedUpdate() manages stamina upgrade-less, maybe something related to sprint
*      - PlayerAvatar.Update() through CrouchRestUpgrade() only
*  - The player's stamina is only stored in PlayerController.EnergyCurrent
*  - Somewhere in PlayerController.fixedUpdate(), add additional stamina buff
*      - float EnergyStart = PlayerController.instance.EnergyStart
*      - float EnergyCurrent = PlayerController.instance.EnergyCurrent
*      - float AgilityRegenBuff = Math.min(ValueCrouchRest + ValueSprintSpeed + ValueStamina) * AgilityPerUpgrade * Time.deltaTime, MaxEnergyRegen)
*      - PlayerController.EnergyCurrent = Mathf.min(EnergyCurrent + AgilityRegenBuff, EnergyStart)
*  - if one of the following is activated, increment int bonus by 1
*      - PunManager.UpgradePlayerEnergy
*      - PunManager.UpgradePlayerSprintSpeed
*      - PunManager.UpgradePlayerCrouchRest
*  
*  actually probably have to import headclef CharacterStats, idk how to access stats
* 
* 
*
* 
* notes:
*  - PunManager does something related to upgrades
*  - when ItemUpgradeDeathHeadBattery calls Upgrade(), it calls PunManager
*  - i think PlayerController.CrouchRestUpgrade()
*  - PlayerAvatar has these values:
*      - upgradeCrouchRest
*  - PlayerController stores EnergyStart and EnergyCurrent
*  - however PlayerAvatar has CrouchRestUpgrade()
*      - I believe PlayerAvatar stores how much upgrades the user has, but also 
*        determines how much the user's EnergyCurrent should be using num = upgradeCrouchRest
*        as a variable.
*      - CrouchRestUpgrade() is called in PlayerAvatar.Update()
*      - What it does is that it finds PlayerController.instance.EnergyCurrent and sets it to
*        either nothing (energyStart) or EnergyCurrent + upgradeCrouchRest * Time.deltaTime * 1f
*      - In short, EnergyCurrent += upgradeCrouchRest per second in float form
*  - base stamina regen is 2-4 stamina/sec
*  - what is UpgradeInfo? what happens in StatsManager.Start()?
*      - a class in StatsManager, contains a string displayName and LocalizedAsset displayNameLocalized
*      - upgradesInfo is a Dictionary<string, UpgradeInfo>, aka stores upgrade info of player related to something?
*      - StatsManager.Start() manages a huge amount of stuff, prob related to player save data
*
* What StatsManager.Start() does
*  - sorts data into 3 main dictionaries:
*      - dictionaryOfDictionaries: playerColor, playerInventorySpot1, playerInventorySpot3, playerHasCrown,
*        item, itemStatBattery
*      - doNotSaveTheseDictionaries: playerInventorySpot1 to 3, playerColor
*      - stripTheseDictionaries: itemsPurchased, itemsPurchasedTotal, itemsUpgradesPurchased, itemBatteryUpgrades,
*        itemStatBattery, playerHealth
*          - Dictionary<string, int> stripTheseDictionaries, i don't know why int
*      - then iterate through dictionaryOfDictionary
*          - stripTheseDictionaries.TryAdd for playerColor or starts with playerInventorySpot with int = -1
*          - otherwise if starts with player, TryAdd() the key with int = 0
*  - stats are probably loaded through LocalizedAsset data type of StatsManager.localizedUpgradeCrouchRest?
*  
*/

//Plugin.mls.LogInfo($"_changeLevelType is {_changeLevelType}");
/*IsCurrentLevel(level, level2)
 * IsLevelShop(_level)
 * IsLevelArena(_level)
 * 
 * RunManager.levelCurrent is datatype Level which stores the current level info
 * RunManager._changeLevelType is a ChangeLevelType enum which prob stores the next level info
 *  - prob represents the next upcoming level based on level choosing logic from current level
 *  
 *  mid-run round is RunLevel
 *  shop transition/shop is Normal, same with truck intermission
 *  next is still Normals
 *  stays Normal after
 *  
 * ChangeLevelType is spammed after dying
 * notes:
 *  - IsCurrentLevel compares 2 levels and returns a bool
 * to check
 *  - SemiFunc.IsLevelShop(Level _level)
 *  - SemiFunc.RunIsLobby()
 *  
 */