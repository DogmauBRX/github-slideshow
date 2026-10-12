using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace FicsitInSpace
{
    // Any cargo container whose name contains "Space Elevator" takes deliveries for
    // the current phase (sheets/phases.json). Progress is per world, like the one
    // Space Elevator in Satisfactory.
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CargoContainer), false)]
    public class SpaceElevator : MyGameLogicComponent
    {
        const string NameTag = "space elevator";
        const string PhaseVar = "FIS_phase";

        IMyCargoContainer block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            block = Entity as IMyCargoContainer;
            if (block == null || block.CubeGrid?.Physics == null)
                return;
            block.AppendingCustomInfo += AppendInfo;
            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void Close()
        {
            if (block != null)
                block.AppendingCustomInfo -= AppendInfo;
        }

        bool IsElevator { get { return block != null && block.CustomName != null && block.CustomName.ToLowerInvariant().Contains(NameTag); } }

        public static int Phase
        {
            get
            {
                int p;
                return MyAPIGateway.Utilities.GetVariable(PhaseVar, out p) && p > 0 ? p : 1;
            }
        }

        static string DeliveredVar(int phase, string item) { return "FIS_p" + phase + "_" + item; }

        static int Delivered(int phase, string item)
        {
            int n;
            return MyAPIGateway.Utilities.GetVariable(DeliveredVar(phase, item), out n) ? n : 0;
        }

        public static List<AmountRow> Order(int phase)
        {
            var list = new List<AmountRow>();
            foreach (var r in Sheets.Phases)
                if (r.Phase == phase) list.Add(r);
            return list;
        }

        public override void UpdateAfterSimulation100()
        {
            try
            {
                if (!IsElevator)
                    return;
                if (MyAPIGateway.Session.IsServer)
                    TakeDeliveries();
                block.RefreshCustomInfo();
                block.SetDetailedInfoDirty();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole(FicsitSession.Tag + ": elevator error " + e);
            }
        }

        void TakeDeliveries()
        {
            var session = FicsitSession.Instance;
            if (session == null) return;
            var inv = block.GetInventory() as IMyInventory;
            if (inv == null) return;

            int phase = Phase;
            var order = Order(phase);
            if (order.Count == 0) return; // every phase done

            bool complete = true;
            foreach (var row in order)
            {
                int have = Delivered(phase, row.Item);
                int missing = row.Amount - have;
                if (missing > 0)
                {
                    var id = session.ItemIds[row.Item];
                    int inBox = (int)inv.GetItemAmount(id);
                    int take = Math.Min(missing, inBox);
                    if (take > 0)
                    {
                        inv.RemoveItemsOfType((MyFixedPoint)take, id);
                        have += take;
                        MyAPIGateway.Utilities.SetVariable(DeliveredVar(phase, row.Item), have);
                        MyAPIGateway.Utilities.ShowNotification("Space Elevator: " + have + " / " + row.Amount + " " + session.Name(row.Item), 2000);
                    }
                }
                if (have < row.Amount) complete = false;
            }

            if (complete)
            {
                MyAPIGateway.Utilities.SetVariable(PhaseVar, phase + 1);
                MyLog.Default.WriteLineAndConsole(FicsitSession.Tag + ": phase " + phase + " complete");
                bool more = Order(phase + 1).Count > 0;
                MyAPIGateway.Utilities.ShowMissionScreen(FicsitSession.Tag, "Space Elevator", "Phase " + phase + " complete",
                    more ? "FICSIT has received your shipment. The next phase is now open."
                         : "FICSIT has received your shipment. Phase " + phase + " is everything in this version. Thank you for your service, pioneer.",
                    null, "Continue");
            }
        }

        void AppendInfo(IMyTerminalBlock b, StringBuilder sb)
        {
            if (!IsElevator) return;
            var session = FicsitSession.Instance;
            int phase = Phase;
            var order = Order(phase);
            sb.Append("--- FICSIT Space Elevator ---\n");
            if (session != null && session.DocsError != null)
                sb.Append(session.DocsError).Append('\n');
            if (order.Count == 0)
            {
                sb.Append("All phases complete.\n");
                return;
            }
            sb.Append("Phase ").Append(phase).Append(" order:\n");
            foreach (var row in order)
            {
                sb.Append("  ").Append(session != null ? session.Name(row.Item) : row.Item)
                  .Append(": ").Append(Delivered(phase, row.Item)).Append(" / ").Append(row.Amount).Append('\n');
            }
        }
    }
}
