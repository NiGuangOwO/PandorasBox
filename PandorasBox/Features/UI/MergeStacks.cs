using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using PandorasBox.FeaturesSetup;
using System.Collections.Generic;
using System.Linq;

namespace PandorasBox.Features.UI
{
    public unsafe class MergeStacks : Feature
    {
        public override string Name => "自动合并相同道具的堆叠";

        public override string Description => "当您打开库存时，插件会尝试将同一物品的所有堆叠加到一起。";

        public override FeatureType FeatureType => FeatureType.Disabled;

        public List<InventorySlot> inventorySlots = new();

        private bool InventoryOpened { get; set; } = false;

        private Dictionary<uint, Item> Sheet { get; set; }

        public class InventorySlot
        {
            public InventoryType Container { get; set; }

            public short Slot { get; set; }

            public uint ItemId { get; set; }

            public bool ItemHQ { get; set; }
        }

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("合并后排序")]
            public bool SortAfter = false;
        }

        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;


        public override void Enable()
        {
            Sheet = Svc.Data.GetExcelSheet<Item>().ToDictionary(x => x.RowId, x => x);
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        private void RunFeature(IFramework framework)
        {
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied | Dalamud.Game.ClientState.Conditions.ConditionFlag.Casting])
            {
                TaskManager.Abort();
                return;
            }
            var id = AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonId();
            var addon = Common.GetAddonByID(id);
            if (addon == null) return;
            if (addon->IsVisible && !InventoryOpened)
            {
                InventoryOpened = true;
                inventorySlots.Clear();
                var inv = InventoryManager.Instance();
                var inv1 = inv->GetInventoryContainer(InventoryType.Inventory1);
                for (var i = 1; i <= inv1->Size; i++)
                {
                    var item = inv1->GetInventorySlot(i - 1);
                    if (item->Flags.HasFlag(InventoryItem.ItemFlags.Collectable)) continue;
                    if (item->Quantity == Sheet[item->ItemId].StackSize || item->ItemId == 0)
                        continue;
                    var slot = new InventorySlot() { Container = InventoryType.Inventory1, ItemId = item->ItemId, Slot = item->Slot, ItemHQ = item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) };
                    inventorySlots.Add(slot);
                }
                var inv2 = inv->GetInventoryContainer(InventoryType.Inventory2);
                for (var i = 1; i <= inv2->Size; i++)
                {
                    var item = inv2->GetInventorySlot(i - 1);
                    if (item->Flags.HasFlag(InventoryItem.ItemFlags.Collectable)) continue;
                    if (item->Quantity == Sheet[item->ItemId].StackSize || item->ItemId == 0)
                        continue;
                    var slot = new InventorySlot() { Container = InventoryType.Inventory2, ItemId = item->ItemId, Slot = item->Slot, ItemHQ = item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) };
                    inventorySlots.Add(slot);
                }
                var inv3 = inv->GetInventoryContainer(InventoryType.Inventory3);
                for (var i = 1; i <= inv3->Size; i++)
                {
                    var item = inv3->GetInventorySlot(i - 1);
                    if (item->Flags.HasFlag(InventoryItem.ItemFlags.Collectable)) continue;
                    if (item->Quantity == Sheet[item->ItemId].StackSize || item->ItemId == 0)
                        continue;
                    var slot = new InventorySlot() { Container = InventoryType.Inventory3, ItemId = item->ItemId, Slot = item->Slot, ItemHQ = item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) };
                    inventorySlots.Add(slot);
                }
                var inv4 = inv->GetInventoryContainer(InventoryType.Inventory4);
                for (var i = 1; i <= inv4->Size; i++)
                {
                    var item = inv4->GetInventorySlot(i - 1);
                    if (item->Flags.HasFlag(InventoryItem.ItemFlags.Collectable)) continue;
                    if (item->Quantity == Sheet[item->ItemId].StackSize || item->ItemId == 0)
                        continue;
                    var slot = new InventorySlot() { Container = InventoryType.Inventory4, ItemId = item->ItemId, Slot = item->Slot, ItemHQ = item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) };
                    inventorySlots.Add(slot);
                }

                foreach (var item in inventorySlots.GroupBy(x => new { x.ItemId, x.ItemHQ }).Where(x => x.Count() > 1))
                {
                    var firstSlot = item.First();
                    for (var i = 1; i < item.Count(); i++)
                    {
                        var slot = item.ToList()[i];
                        //inv->MoveItemSlot(slot.Container, slot.Slot, firstSlot.Container, firstSlot.Slot, 1);
                    }
                }

                if (inventorySlots.GroupBy(x => new { x.ItemId, x.ItemHQ }).Any(x => x.Count() > 1) && Config.SortAfter)
                {
                    TaskManager.DelayNext("Sort", 100);
                    TaskManager.Enqueue(() => Chat.Instance.SendMessage("/isort condition inventory id"));
                    TaskManager.Enqueue(() => Chat.Instance.SendMessage("/isort execute inventory"));
                }
            }
            else if (!addon->IsVisible)
            {
                InventoryOpened = false;
            }
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Sheet = null;
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
