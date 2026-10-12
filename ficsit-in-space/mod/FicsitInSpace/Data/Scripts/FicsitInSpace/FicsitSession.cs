using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace FicsitInSpace
{
    // Loads the player's Satisfactory Docs file and turns the sheet rows into live
    // Space Engineers definitions: item names, recipe amounts and times. Every edit
    // is undone on unload so other worlds stay vanilla.
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class FicsitSession : MySessionComponentBase
    {
        public const string DocsPath = "Satisfactory/Docs.json";
        public const string Tag = "FICSIT in Space";

        public static FicsitSession Instance;

        public SatDocs Docs;
        public string DocsError;
        public readonly Dictionary<string, MyDefinitionId> ItemIds = new Dictionary<string, MyDefinitionId>();
        public readonly Dictionary<string, string> ItemNames = new Dictionary<string, string>();

        readonly List<Action> undo = new List<Action>();
        int kitTick;
        bool kitDone, warned;

        public override void LoadData()
        {
            Instance = this;
            foreach (var row in Sheets.Items)
            {
                ItemIds[row.Key] = MyDefinitionId.Parse(row.SeType + "/" + row.SeSubtype);
                ItemNames[row.Key] = row.Key;
            }

            try
            {
                LoadDocs();
            }
            catch (Exception e)
            {
                DocsError = "Could not read Satisfactory data: " + e.Message;
                MyLog.Default.WriteLineAndConsole(Tag + ": " + e);
            }

            if (Docs != null)
            {
                PatchItems();
                PatchBlueprints();
            }
            MyLog.Default.WriteLineAndConsole(Tag + ": loaded, docsClasses=" + (Docs != null ? Docs.ClassCount : 0) + (DocsError != null ? " error=" + DocsError : ""));
        }

        void LoadDocs()
        {
            if (!MyAPIGateway.Utilities.FileExistsInModLocation(DocsPath, ModContext.ModItem))
            {
                DocsError = "Satisfactory data not found. Install Satisfactory and start this mashup from Melty.";
                return;
            }
            string text;
            using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(DocsPath, ModContext.ModItem))
                text = reader.ReadToEnd();
            Docs = SatDocs.Parse(text);
        }

        MyPhysicalItemDefinition ItemDef(MyDefinitionId id)
        {
            MyPhysicalItemDefinition def;
            return MyDefinitionManager.Static.TryGetPhysicalItemDefinition(id, out def) ? def : null;
        }

        void PatchItems()
        {
            foreach (var row in Sheets.Items)
            {
                string name = Docs.DisplayName(row.SatClass);
                if (!string.IsNullOrEmpty(name))
                    ItemNames[row.Key] = name;
                if (row.Kind != "new")
                    continue; // vanilla SE items keep their own names

                var def = ItemDef(ItemIds[row.Key]);
                if (def == null)
                {
                    MyLog.Default.WriteLineAndConsole(Tag + ": missing definition " + ItemIds[row.Key]);
                    continue;
                }

                var look = ItemDef(MyDefinitionId.Parse("Component/" + row.LooksLike));
                if (look != null)
                {
                    Edit(def, d => d.Model, (d, v) => d.Model = v, look.Model);
                    Edit(def, d => d.Icons, (d, v) => d.Icons = v, look.Icons);
                }
                if (!string.IsNullOrEmpty(name))
                {
                    Edit(def, d => d.DisplayNameEnum, (d, v) => d.DisplayNameEnum = v, null);
                    Edit(def, d => d.DisplayNameString, (d, v) => d.DisplayNameString = v, name);
                }
                string desc = Docs.Description(row.SatClass);
                if (!string.IsNullOrEmpty(desc))
                {
                    Edit(def, d => d.DescriptionEnum, (d, v) => d.DescriptionEnum = v, null);
                    Edit(def, d => d.DescriptionString, (d, v) => d.DescriptionString = v, desc);
                }
            }
        }

        void PatchBlueprints()
        {
            var bySat = new Dictionary<string, MyDefinitionId>();
            foreach (var row in Sheets.Items)
                bySat[row.SatClass] = ItemIds[row.Key];

            foreach (var row in Sheets.Recipes)
            {
                var bp = MyDefinitionManager.Static.GetBlueprintDefinition(MyDefinitionId.Parse("BlueprintDefinition/" + row.SeBlueprint));
                var recipe = Docs.GetRecipe(row.SatRecipe);
                if (bp == null || recipe == null || recipe.Products.Count == 0)
                {
                    MyLog.Default.WriteLineAndConsole(Tag + ": recipe " + row.SatRecipe + " not patched (bp=" + (bp != null) + ", docs=" + (recipe != null) + ")");
                    continue;
                }

                var pre = Map(recipe.Ingredients, bySat);
                var res = Map(recipe.Products, bySat);
                if (pre == null || res == null || res[0].Id != ItemIds[row.Output])
                {
                    MyLog.Default.WriteLineAndConsole(Tag + ": recipe " + row.SatRecipe + " uses items outside the sheets; left as is");
                    continue;
                }

                Edit(bp, b => b.Prerequisites, (b, v) => b.Prerequisites = v, pre);
                Edit(bp, b => b.Results, (b, v) => b.Results = v, res);
                if (recipe.Seconds > 0)
                    Edit(bp, b => b.BaseProductionTimeInSeconds, (b, v) => b.BaseProductionTimeInSeconds = v, recipe.Seconds);
                if (!string.IsNullOrEmpty(recipe.DisplayName))
                {
                    Edit(bp, b => b.DisplayNameEnum, (b, v) => b.DisplayNameEnum = v, null);
                    Edit(bp, b => b.DisplayNameString, (b, v) => b.DisplayNameString = v, recipe.DisplayName);
                }
                bp.Postprocess(); // recompute output volume for the new results
            }
        }

        static MyBlueprintDefinitionBase.Item[] Map(List<SatDocs.Amount> amounts, Dictionary<string, MyDefinitionId> bySat)
        {
            var arr = new MyBlueprintDefinitionBase.Item[amounts.Count];
            for (int i = 0; i < amounts.Count; i++)
            {
                MyDefinitionId id;
                if (!bySat.TryGetValue(amounts[i].SatClass, out id) || amounts[i].Count <= 0)
                    return null;
                arr[i] = new MyBlueprintDefinitionBase.Item { Id = id, Amount = amounts[i].Count };
            }
            return arr;
        }

        void Edit<TObj, TVal>(TObj obj, Func<TObj, TVal> get, Action<TObj, TVal> set, TVal value)
        {
            TVal old = get(obj);
            set(obj, value);
            undo.Add(() => set(obj, old));
        }

        protected override void UnloadData()
        {
            try
            {
                for (int i = undo.Count - 1; i >= 0; i--)
                    undo[i]();
                undo.Clear();
                foreach (var row in Sheets.Recipes)
                {
                    var bp = MyDefinitionManager.Static.GetBlueprintDefinition(MyDefinitionId.Parse("BlueprintDefinition/" + row.SeBlueprint));
                    if (bp != null) bp.Postprocess();
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole(Tag + ": unload error " + e);
            }
            Instance = null;
        }

        public string Name(string key)
        {
            string n;
            return ItemNames.TryGetValue(key, out n) ? n : key;
        }

        // Starting kit, once per player per world (solo: the local player).
        public override void UpdateAfterSimulation()
        {
            if (kitDone || ++kitTick % 60 != 0)
                return;
            try
            {
                var player = MyAPIGateway.Session?.Player;
                var character = player?.Character;
                if (character == null || !MyAPIGateway.Session.IsServer)
                    return;

                if (!warned)
                {
                    warned = true;
                    if (DocsError != null)
                        MyAPIGateway.Utilities.ShowMessage(Tag, DocsError);
                }

                string key = "FIS_kit_" + player.SteamUserId;
                bool given;
                if (MyAPIGateway.Utilities.GetVariable(key, out given) && given)
                {
                    kitDone = true;
                    return;
                }

                var inv = character.GetInventory() as IMyInventory;
                if (inv == null)
                    return;
                foreach (var row in Sheets.Kit)
                {
                    var ob = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(ItemIds[row.Item]);
                    inv.AddItems((MyFixedPoint)row.Amount, ob);
                }
                MyAPIGateway.Utilities.SetVariable(key, true);
                kitDone = true;

                int need = 0;
                string item = null;
                foreach (var p in Sheets.Phases)
                    if (p.Phase == 1) { need = p.Amount; item = p.Item; }
                MyAPIGateway.Utilities.ShowMissionScreen(Tag, "Space Elevator", "Phase 1",
                    "FICSIT has sent you a starter kit.\n\n" +
                    "Name a cargo container \"Space Elevator\" and deliver " + need + " x " + Name(item) + " to it.\n\n" +
                    "Build the parts in an Assembler: the recipes come from your Satisfactory install.",
                    null, "Get to work");
            }
            catch (Exception e)
            {
                kitDone = true;
                MyLog.Default.WriteLineAndConsole(Tag + ": kit error " + e);
            }
        }
    }
}
