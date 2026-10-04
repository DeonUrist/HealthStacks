# HealthStacks

A tiny BepInEx mod for **Apocalypter**: tops up your partly used Bandage (F1) / First Aid (F2) from the ones you find.

## What it does
In the vanilla game, when the Bandage or First Aid quick slot already holds an item, pressing **Use (F)** on another one
on the ground only says **FULL** — even if the one you carry has 1 charge left.

With HealthStacks:
- Use on a bandage / first aid kit on the ground moves its charges into the one in your slot, up to **3**.
- Whatever doesn't fit stays on the ground in that item (e.g. slot 2/3 + ground 3/3 → slot 3/3, ground 2/3).
- An item that gives away all its charges disappears.
- Slot empty → the normal pickup. Slot already at 3 → the normal "FULL".

## Install
BepInEx 5 required. Copy `HealthStacks.dll` into `BepInEx\plugins\`.

## Config
`BepInEx\config\com.denis.apocalypter.healthstacks.cfg`

| Setting | Default | |
|---|---|---|
| `[General] Enabled` | `true` | Off = vanilla behaviour |
| `[General] Apocasetter` | `true` | Listed in the Apocasetter Mods menu |

## Build
`./build.sh` (mcs against the game's `Apocalypter_Data\Managed` and `BepInEx\core`; override with `MANAGED=` / `BEPCORE=`),
or `dotnet build` with `HealthStacks.csproj` (deploys to the game's plugins folder).

## How it works
The quick slots `PlayerCamera/QuickItems/Bandage_Item` and `FirstAid_Item` each have a `TakeItem` FSM
(`off → checkID → over`, Use → `SlotChild` → "FULL" when the slot has a child). A Harmony prefix on PlayMaker's
`GetButtonDown.OnUpdate` catches the Use press in `over`: if the slot's item (same `ID` FSM string) has fewer than 3 in
its `Quantity` FSM, charges are moved from the targeted item (`TakeItem` var `Item`), the slot's counter text is
refreshed, and the vanilla branch is skipped. A ground item left at 0 is destroyed (its own `Quantity` FSM would do
the same).
