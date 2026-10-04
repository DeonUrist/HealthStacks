using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace HealthStacks
{
    /// Bandage (F1) and First Aid (F2) quick slots: pressing Use on a matching item on the ground while the slot holds a
    /// partly used one tops the slot up to MaxCharges. Whatever is left stays on the ground; an emptied item is destroyed.
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.denis.apocalypter.healthstacks";
        public const string NAME = "HealthStacks";
        public const string VERSION = "1.0.0";
        internal const int MaxCharges = 3;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;

        private void Awake()
        {
            Log = Logger;
            Config.Bind("General", "Apocasetter", true, "Show this mod in the Apocasetter Mods menu");
            Enabled = Config.Bind("General", "Enabled", true,
                "Use on a bandage / first aid kit on the ground tops up the partly used one in your F1 / F2 slot (max " + MaxCharges + " charges). Leftover charges stay on the ground; an emptied item disappears.");
            new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo(NAME + " " + VERSION + " loaded");
        }
    }

    [HarmonyPatch(typeof(GetButtonDown), "OnUpdate")]
    internal static class TakeItemUsePatch
    {
        static bool Prefix(GetButtonDown __instance)
        {
            try
            {
                if (!Plugin.Enabled.Value) return true;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "TakeItem" || __instance.buttonName == null || __instance.buttonName.Value != "Use") return true;
                if (fsm.ActiveStateName != "over") return true;
                var slot = fsm.GameObject;
                if (slot == null || (slot.name != "Bandage_Item" && slot.name != "FirstAid_Item")) return true;
                if (!Input.GetButtonDown("Use")) return true;
                if (!Merge(fsm, slot)) return true;
                if (__instance.storeResult != null) __instance.storeResult.Value = false;
                return false; // handled: the vanilla "FULL" branch never runs
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("TakeItem patch: " + e);
                return true;
            }
        }

        static bool Merge(Fsm takeFsm, GameObject slot)
        {
            var itemVar = takeFsm.Variables.GetFsmGameObject("Item");
            var ground = itemVar != null ? itemVar.Value : null;
            if (ground == null) return false;

            GameObject held = null;
            for (int i = 0; i < slot.transform.childCount; i++)
            {
                var c = slot.transform.GetChild(i).gameObject;
                if (QuantityVar(c) != null) { held = c; break; }
            }
            if (held == null || held == ground) return false; // empty slot -> vanilla pickup

            string id = ItemId(ground);
            if (id == null || id != ItemId(held)) return false;

            var hq = QuantityVar(held);
            var gq = QuantityVar(ground);
            if (gq == null) return false;
            int need = Plugin.MaxCharges - hq.Value;
            if (need <= 0 || gq.Value <= 0) return false; // slot already full -> vanilla "FULL"

            int moved = Math.Min(need, gq.Value);
            hq.Value += moved;
            gq.Value -= moved;
            Plugin.Log.LogInfo(string.Format("{0}: took {1} charge(s), slot {2}/{3}, ground item {4}", slot.name, moved, hq.Value,
                Plugin.MaxCharges, gq.Value > 0 ? gq.Value + " left" : "used up"));

            if (gq.Value <= 0)
            {
                itemVar.Value = null;
                UnityEngine.Object.Destroy(ground); // its own Quantity FSM would do the same (Quantity < 1 -> DestroySelf)
            }
            PlayPickupSound(takeFsm, slot);
            RefreshSlotUi(slot, hq.Value);
            return true;
        }

        static FsmInt QuantityVar(GameObject go)
        {
            var f = FindFsm(go, "Quantity");
            return f != null ? f.FsmVariables.GetFsmInt("Quantity") : null;
        }

        static string ItemId(GameObject go)
        {
            var f = FindFsm(go, "ID");
            var s = f != null ? f.FsmVariables.GetFsmString("ID") : null;
            return s != null ? s.Value : null;
        }

        static PlayMakerFSM FindFsm(GameObject go, string name)
        {
            foreach (var f in go.GetComponents<PlayMakerFSM>())
                if (f.FsmName == name) return f;
            return null;
        }

        /// SlotEmptyFull writes the counter only when it enters "full": write it directly and keep its cached var in sync.
        static void RefreshSlotUi(GameObject slot, int quantity)
        {
            var sef = FindFsm(slot, "SlotEmptyFull");
            if (sef == null) return;
            var q = sef.FsmVariables.GetFsmInt("Quantity");
            if (q != null) q.Value = quantity;
            var full = FindState(sef.Fsm, "full");
            if (full == null) return;
            foreach (var a in full.Actions)
            {
                var t = a as UiTextSetText;
                if (t == null) continue;
                var go = sef.Fsm.GetOwnerDefaultTarget(t.gameObject);
                var txt = go != null ? go.GetComponent<UnityEngine.UI.Text>() : null;
                if (txt != null) txt.text = quantity.ToString();
            }
        }

        static void PlayPickupSound(Fsm takeFsm, GameObject slot)
        {
            var st = FindState(takeFsm, "takeWeapon");
            if (st == null) return;
            foreach (var a in st.Actions)
            {
                var p = a as PlaySound;
                if (p == null || p.clip == null) continue;
                var clip = p.clip.Value as AudioClip;
                if (clip != null) AudioSource.PlayClipAtPoint(clip, slot.transform.position, p.volume != null ? p.volume.Value : 1f);
                return;
            }
        }

        static FsmState FindState(Fsm fsm, string name)
        {
            foreach (var s in fsm.States)
                if (s.Name == name) return s;
            return null;
        }
    }
}
