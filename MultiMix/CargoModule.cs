using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript {
	partial class Program {
		//-------------
		CargoModule cargoMgr = null;
		class CargoModule : TickBase {
			public CargoModule(Program p) : base(p) { }

			public void AddMenu(MenuItem menu) {
				menu.Add(
					Menu(() => LabelOnOff(AutoDecend, "Auto Descend:", "ENABLED", "OFF"))
						.Enter(() => AutoDecend = !AutoDecend),
					Menu("Cargo management").Add(
						Menu(() => LabelOnOff(Enabled, "StoneEject module:", "ENABLED", "OFF"))
							.Enter(() => Enabled = !Enabled),
						Menu(() => LabelOnOff(EjectorsEnabled, "Ejectors:", "ENABLED", "OFF"))
							.Enter(() => EjectorsEnabled = !EjectorsEnabled),
						Menu(() => $"Transfer volume: {MaxTransferVol}")
							.Left(() => MaxTransferVol -= 100)
							.Right(() => MaxTransferVol += 100)
							.Back(() => MaxTransferVol = 1000)
					)
				);
			}

			new public bool Active {
				get { return null != ascMgr ? !ascMgr.IsRunning : true; }
			}

			public bool Enabled {
				get { return isEnabled; }
				set { UpdateBlocks(isEnabled = value); }
			}
			bool isEnabled = true;

			long nextTick = 0;
			StringBuilder sb = new StringBuilder();
			bool wasLocked;
			override public bool Tick() {
				if (nextTick > Pgm.totalTicks) { return false; }
				nextTick = Pgm.totalTicks + TimeSpan.TicksPerSecond;

				sb.Append(DateTime.Now.ToString("HH\\:mm\\:ss\\.fff"));

				if (isEnabled && ejectors.Count > 0 && inventories.Count > 0) {
					bool hasLock, hasMagnet;
					CheckConnectors(out hasLock, out hasMagnet);
					if (hasLock != wasLocked || hasLock) {
						sb.Append("\n Connector: ").Append(hasLock ? "Locked" : "Released");
						if (hasLock != wasLocked) {
							wasLocked = hasLock;
							EjectorsEnabled=EjectorsEnabled;
						}
					} else {
						if (hasMagnet) {
							sb.Append("\n Connector: In proximity");
						}
						TransferStonesToEjectors();
					}
				} else {
					sb.Append("\n StoneEject module ").Append(!isEnabled ? "disabled" : "missing blocks");
				}
				if (cargoVolume.Count > 0) {
					sb.Append("\n-- Cargo fill levels --");
					foreach (var kv in cargoVolume) {
						var p = string.Format("{0,3:F0}%", kv.Value.Value / kv.Value.Key * 100f);
						sb.Append($"\n {p} {kv.Key}");
					}
				}
				if (itemsAmounts.Count > 0) {
					sb.Append("\n-- Item amounts --");
					foreach (var kv in itemsAmounts) {
						var p = string.Format("{0,6:F1}", FixItemAmout(kv.Key, kv.Value));
						sb.Append($"\n {p} = {kv.Key}");
					}
				}

				lcd.WritePublicText(sb);
				return false;
			}

			void CheckConnectors(out bool hasLocked, out bool hasMagnet) {
				bool anyLocked = false, anyMagnet = false;
				connectors.ForEach(c => {
					anyLocked |= c.Status == MyShipConnectorStatus.Connected;
					anyMagnet |= c.Status == MyShipConnectorStatus.Connectable;
				});
				hasLocked = anyLocked;
				hasMagnet = anyMagnet;
			}

			public int MaxTransferVol {
				get { return maxTransferVol; }
				set { maxTransferVol = Math.Max(100, value); }
			}
			int maxTransferVol = 1000;

			int idxEjector = -1;
			int idxInv = -1;
			Dictionary<string, float> itemsAmounts = new Dictionary<string, float>();
			Dictionary<string, KeyValuePair<float, float>> cargoVolume = new Dictionary<string, KeyValuePair<float, float>>();
			void TransferStonesToEjectors() {
				float movedVolume = 0;
				int movedEjectors = 0;
				int maxIter = ejectors.Count;
				itemsAmounts.Clear();
				cargoVolume.Clear();
				for (int i = inventories.Count; maxIter > 0 && i > 0; i--) {
					idxInv = (idxInv + 1) % inventories.Count;
					var inv = inventories[idxInv];
					IMyInventory fromInv = inv.GetInventory(0);

					var blkId = GetBlockType(inv);
					KeyValuePair<float, float> cm;
					if (!cargoVolume.TryGetValue(blkId, out cm)) { cm = new KeyValuePair<float, float>(0, 0); }
					cargoVolume[blkId] = new KeyValuePair<float, float>(cm.Key + (float)fromInv.MaxVolume, cm.Value + (float)fromInv.CurrentVolume);

					int s, t;
					string tpeId;
					float amt;
					do {
						var stacks = fromInv.GetItems();
						t = stacks.Count;
						s = -1;
						while (t-- > 0) {
							var stk = stacks[t];
							if (!itemsAmounts.TryGetValue(tpeId = GetItemType(stk), out amt)) { amt = 0; }
							itemsAmounts[tpeId] = amt + ((float)stk.Amount);

							if (s < 0 && stk.GetDefinitionId().Equals(oreStone.ItemId)) { s = t; }
						}
						if (s >= 0) {
							while (maxIter-- > 0) {
								idxEjector = (idxEjector + 1) % ejectors.Count;
								IMyInventory toInv = ejectors[idxEjector].GetInventory(0);
								float prevVol, toMoveVol = Math.Min(maxTransferVol / 1000, (float)toInv.MaxVolume) - (prevVol = (float)toInv.CurrentVolume);
								if (toMoveVol > 0.01f) {
									//sb.Append($"\nInv#{idxInv} -> Ejector#{idxEjector}");
									fromInv.TransferItemTo(toInv, s, null, null, (VRage.MyFixedPoint)toMoveVol * 1000);
									movedVolume += (float)toInv.CurrentVolume - prevVol;
									movedEjectors++;
									break;
								}
							}
						}
					} while (s >= 0 && maxIter > 0);
				}
				sb.Append($"\nStone moved: {movedVolume:F2}\nInto {movedEjectors} ejector{(movedEjectors == 1 ? "" : "s")}");

				if (movedVolume < 1) { DoAutoDecend(); }
			}

			public bool AutoDecend {
				get { return autoDecendEnabled; }
				set { autoDecendEnabled = value; }
			}
			bool autoDecendEnabled = false;

			long autoDecendNextTick = 0;
			private void DoAutoDecend() {
				if (AutoDecend && autoDecendNextTick < Pgm.totalTicks) {
					autoDecendNextTick = Pgm.totalTicks + TimeSpan.TicksPerSecond;
					Pgm.yieldMgr.Add(DoDescend());
				}
			}
			IEnumerable<int> DoDescend() {
				var rc = Pgm.GetShipController();
				if (null==rc) { yield break; }
				rc.DampenersOverride = false;
				yield return 10;

				rc = Pgm.GetShipController();
				if (null!=rc) { rc.DampenersOverride = true; }
			}

			OutputPanel lcd = null;
			MyInventoryItemFilter oreStone = new MyInventoryItemFilter("MyObjectBuilder_Ore/Stone");
			List<IMyShipConnector> ejectors = new List<IMyShipConnector>();
			List<IMyConveyorSorter> sorters = new List<IMyConveyorSorter>();
			List<IMyTerminalBlock> inventories = new List<IMyTerminalBlock>();
			List<IMyShipConnector> connectors = new List<IMyShipConnector>();

			List<Func<IMyTerminalBlock, bool>> pipeline = null;
			List<MyInventoryItemFilter> filterOreStone = null;

			AscendDecendModule ascMgr;
			public void Refresh(OutputPanel _lcd, AscendDecendModule _mgr = null) {
				lcd = _lcd;
				ascMgr = _mgr;
				nextTick = 0;

				if (null == pipeline) {
					filterOreStone = new List<MyInventoryItemFilter> { oreStone };

					pipeline = new List<Func<IMyTerminalBlock, bool>> {
						(b) => {
							return SameGrid(b,Me) && !NameContains(b,MultiMix_IgnoreBlocks) && b.IsFunctional;
						},
						(b) => {
							var e=b as IMyShipConnector;
							if (null!=e) {
								if (SubtypeContains(e,"ConnectorSmall")) {
									ejectors.Add(e);
									e.ThrowOut = true;
									e.CollectAll = false;
								} else {
									connectors.Add(e);
									inventories.Add(e);
								}
								return false;
							}
							return true; // continue pipeline
						},
						(b) => {
							var s=b as IMyConveyorSorter;
							if (null!=s) {
								sorters.Add(s);
								s.DrainAll = false;
								if (NameContains(s,"INPUT")) {
									s.SetFilter(MyConveyorSorterMode.Blacklist, filterOreStone);
								} else if (NameContains(s,"OUTPUT")) {
									s.SetFilter(MyConveyorSorterMode.Whitelist, filterOreStone);
								}
								return false;
							}
							return true; // continue pipeline
						},
						(b) => {
							// IMyShipConnector and IMyConveyorSorter have already been filtered out.
							if (b.InventoryCount == 1 && !(b is IMyReactor || b is IMyCockpit)) {
								inventories.Add(b);
								return false;
							}
							return true; // continue pipeline
						},
					};
				}

				UpdateBlocks();

				bool unused;
				CheckConnectors(out wasLocked, out unused);
			}
			void UpdateBlocks(bool update=true) {
				connectors.Clear();
				ejectors.Clear();
				sorters.Clear();
				inventories.Clear();

				if (update) { GatherBlocks(Pgm, pipeline); }
			}

			public bool EjectorsEnabled {
				get { return ejectorsEnabled; }
				set { ejectorsEnabled = value; ejectors.ForEach(e => e.Enabled = (!wasLocked && value)); }
			}
			bool ejectorsEnabled = true;
		}
	}
}
