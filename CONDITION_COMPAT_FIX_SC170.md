NSC UltimateStormAPI SC 1.70 Condition Compatibility Fix v1

Purpose
-------
Fixes a verified signature mismatch between the user's NSUNSC 1.70 executable and the uploaded UltimateStormAPI DLL.
The API searches the SC Condition PRM functions using legacy limits 0x1FB / 0x1FC / 0x1FD.
This executable has the same functions but updated limits 0x1FE / 0x1FF / 0x200, so the API's Condition PRM hook block is skipped.

What this plugin does
---------------------
1. Validates exact EXE/API fingerprints before touching memory.
2. Reads the actual runtime condition count from UltimateStormAPI's parsed condition vector.
3. Replaces the missed vanilla condition lookup function with UltimateStormAPI's own dynamic lookup helper (API RVA 0x8450), which is what the original API installer intended to do.
4. Expands the two associated condition limits from the executable's current values to count-1 / count.
5. Does NOT contain Tobi, mtob, characode 281, COND_2DNZ, or any stage name. It is a generic compatibility fix for this EXE/API pair.

Install/Test
------------
- REMOVE/disable NSCApiDPadInternalTrace_v7_1.dll and older tracers first.
- Keep d3dcompiler_47.dll unchanged.
- Put NSCApiConditionCompatFix_v2.dll in moddingapi\\mods\\base_game\\
- Start the game and wait several seconds before entering battle.
- Test Right D-Pad Izanagi and Left D-Pad.
- Test the Ultimate/Kamui sequence.
- Send moddingapi\\api_condition_compat_fix.log and tell whether Izanagi and Kamui changed.

Expected success log
--------------------
Status: CONDITION_COMPAT_FIX_APPLIED
Runtime condition count: 516   (with the currently compiled Tobi output)
Max-index old=0x000001FF new=0x00000203
Count old=0x00000200 new=0x00000204

Safety
------
The plugin refuses to patch if the EXE/API fingerprints or expected instruction bytes do not match.
Remove the DLL to revert the runtime patch; it does not modify the EXE file on disk.
