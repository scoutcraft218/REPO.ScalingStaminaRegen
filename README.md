# ScalingStaminaRegen
(This mod is partially inspired by headclef's Agility, very few parts of the description are similar since there's like no other way to describe it and 1 method was heavily remodified, everything else is original)

This mod adds bonus passive stamina regen called "Agility" which scales with stamina-related upgrade levels.
- Scales with Stamina, Sprint Speed and Crouch Rest

Here is an example of the Agility buff in action:

| Stamina | Crouch Rest | Speed | Combined | Agility bonus (0.2/sec) | Total regen (3/sec) |
| ------- | ----------- | ----- | -------- | ----------------------- | ------------------- |
| 1       | 0           | 0     | 1        | 0.2/sec                 | 3.3/sec             |
| 2       | 1           | 1     | 4        | 0.8/sec                 | 4.2/sec             |
| 3       | 2           | 2     | 7        | 1.4/sec                 | 5.1/sec             |
| 5       | 3           | 3     | 11       | 2.2/sec                 | 6.3/sec             |


Here is a list of all configurables:

| Key                       | Default | Description                                                                              |
| ------------------------- | ------- | ---------------------------------------------------------------------------------------- |
| BaseStaminaRegen          | `3f`    | What the base stamina regen is. (vanilla is 2 btw)                                       |
| SprintRechargeTime        | `1f`    | How long it takes after sprinting for base stamina regen to reactivate.                  |
| AgilityPerUpgrade         | `0.2f`  | Extra regen per combined stamina-related upgrade level.                                  |
| MaxAgilityCap             | `50f`   | What Agility will be capped at.                                                          |
| ToggleDisableAgility      | `false` | Disables the Agility bonus by fixing AgilityPerUpgrade at 0.                             |
| ToggleAgilityTimer        | `false` | Whether Agility should activate during the Sprint timer.                                 |
| ToggleRecalculatePerFrame | `false` | If Agility should be recalculated every frame. (suppresses most Agility debug logs)      |
| ToggleRecalculateInfo     | `false` | If Agility info should be printed at level "info" of the Logger. (it's Debug by default) |
| ToggleAgilityUncapped     | `false` | If Agility should cap out at MaxAgilityCap.                                              |

Notes:
- This mod is client side (i think), and should work with multiplayer (i think).
	- This is my first mod, idk replication, man.
- The vanilla base stamina regen is actually 2 and not 3, but i find 3 works well.
- Modifying max stamina is very weird and modifying crouch regen rate is very hard so those changes won't come for a while
- I think BaseStaminaRegen and Agility should theoretically work with negative values but never tested.

Super epic icon image made by Bumpy Jr

# Extra details
The values of this mod are updated via "SetValueConfig()". This is called every time Agility gets recalculated via RecalculateAgility(). Agility gets recalculated in 3 cases:
1. A round started in either some map or the shop after 1 second.
2. One of "Stamina", "Sprint" or "Crouch Rest" has been updated by someone.
	1. For developers, it patches "UpdateCrouchRestRightAway", "UpdateSprintSpeedRightAway" and "UpdateEnergyRightAway" in PunManager.
	2. Funnily enough, someone else in multiplayer using the upgrade will cause everyone's Agility to upgrade. idk why.
3. Every frame during level or shop IF the "ToggleRecalculatePerFrame" configurable is true.

Agility is applied to your stamina regen in PlayerController.Update() with few restrictions.
- If you're sprinting, don't
- If you're at max stamina, don't
- If your sprint timer isn't 0, don't
- However if "ToggleAgilityTimer" is true, then Agility bypasses the sprint timer except while sprinting. (hardcoded to apply after 0.2 seconds, since otherwise it flickers)

This mod relies on getting your Steam id to calculate Agility, otherwise your Agility is fixed at 0.
- I patched PlayerController.Start() to grab it, idk when the steam id first appears so the method actually fails 1 time and then works (hopefully).
