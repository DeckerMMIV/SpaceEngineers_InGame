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
		EngineModule engineMgr = null;
		class EngineModule : TickBase, IMenuCollector {
			public EngineModule(Program p):base(p){}
			override public bool Tick() { return false; }

			public void Refresh(IMyTerminalBlock _myGrid, IMyShipController _sc) {
				myGrid = _myGrid;
				dirRefBlk = _sc;
			}

			public void AddMenu(MenuManager menuMgr) {
				if (GetThrustBlocks(ThrustFlags.All, Pgm, Me).Count<=0) {
					return; // No thrusters found
				}

				Action<ThrustFlags> tsToggle = (tf) => {
					SetEnabledAbsToggle(GetThrustBlocks(tf, Pgm, Me, Pgm.GetShipController()));
				};
				Action<ThrustFlags> tsOn = (tf) => {
					SetEnabled(GetThrustBlocks(tf, Pgm, Me, Pgm.GetShipController()), true);
				};
				Action<ThrustFlags> tsOff = (tf) => {
					SetEnabled(GetThrustBlocks(tf, Pgm, Me, Pgm.GetShipController()), false);
				};

				const int p = 16; // padding
				MenuItem tt;
				menuMgr.Add(
					tt = Menu("Engine/Thrust settings").Add(
						Menu(() => GetText(1, "All Thrusters", p))
							.Collect(this)
							.Enter("toggleEngines", () => tsToggle(ThrustFlags.All))
							.Left(() => tsOn(ThrustFlags.All))
							.Right(() => tsOff(ThrustFlags.All)),
						Menu("Engine types").Add(
							Menu(() => GetText(1, "Atmospheric", p + 0))
								.Collect(this)
								.Enter("toggleAtmos", () => tsToggle(ThrustFlags.Atmospheric))
								.Left(() => tsOn(ThrustFlags.Atmospheric))
								.Right(() => tsOff(ThrustFlags.Atmospheric)),
							Menu(() => GetText(1, "Ion", p + 8))
								.Collect(this)
								.Enter("toggleIon", () => tsToggle(ThrustFlags.Ion))
								.Left(() => tsOn(ThrustFlags.Ion))
								.Right(() => tsOff(ThrustFlags.Ion)),
							Menu(() => GetText(1, "Hydrogen", p + 2))
								.Collect(this)
								.Enter("toggleHydro", () => tsToggle(ThrustFlags.Hydrogen))
								.Left(() => tsOn(ThrustFlags.Hydrogen))
								.Right(() => tsOff(ThrustFlags.Hydrogen))
						)
					)
				);

				if (null==Pgm.GetShipController()) {
					menuMgr.WarningText += "\n ShipController missing. Some features unavailable!";
				} else {
					Action<ThrustFlags, int> tsPower = (tf, m) => {
						var lst = GetThrustBlocks(tf, Pgm, Me, Pgm.GetShipController());
						if (0 != m) {
							int o=0;
							lst.ForEach(b => { o += (int)((b as IMyThrust).GetValueFloat("Override")); });
							if (100==m) {
								m = o>0 ? 0 : m;
							} else {
								if (0 < lst.Count) { o /= lst.Count; }
								m = Math.Min(100, Math.Max(0, o + m * 10));
							}
						}
						SetThrustAbsPct(lst, m);
					};

					int[] pad = new[] {p+2,p+1,p+3,p+1,p+2,p+0};
					MenuItem md = Menu("Directions");
					MenuItem mo = Menu("Override thrust");
					int i=0;
					foreach(string txt in Pgm.DIRECTIONS) {
						ThrustFlags tf = (ThrustFlags)(1<<i);
						int j=i++;
						md.Add(Menu(()=>GetText(1,txt,pad[j])).Collect(this).Enter($"toggle{txt}",()=>tsToggle(tf)).Left(()=>tsOn(tf)).Right(()=>tsOff(tf)));
						mo.Add(Menu(()=>GetText(2,txt,pad[j])).Collect(this).Enter($"thrust{txt}",()=>tsPower(tf,100)).Left(()=>tsPower(tf,-1)).Right(()=>tsPower(tf,1)).Back(()=>tsPower(tf,0)));
					}
					tt.Add(md, mo);
				}
			}

			IMyTerminalBlock myGrid;
			IMyShipController dirRefBlk;
			class Counters { public int enabled; public int disabled; public float curThrust; public float maxThrust; public float maxEffThrust; }
			Dictionary<string, Counters> namedCounters = new Dictionary<string, Counters>();

			public void CollectSetup() {
				namedCounters.Clear();
			}
			public void CollectTeardown() { }

			public void CollectBlock(IMyTerminalBlock blk) {
				if (!SameGrid(blk, myGrid) || !blk.IsFunctional) return;
				var thr = blk as IMyThrust;
				if (null == thr) return;

				ThrustFlags flags = 0;

				if (SubtypeContains(blk, "Atmos")) flags |= ThrustFlags.Atmospheric;
				else if (SubtypeContains(blk, "Hydro")) flags |= ThrustFlags.Hydrogen;
				else flags |= ThrustFlags.Ion;

				if (null != dirRefBlk) {
					// Calculate direction of thrust-block according to supplied reference-block.
					int blkDir = (int)dirRefBlk.Orientation.TransformDirectionInverse(thr.Orientation.TransformDirection(Base6Directions.Direction.Forward));
					flags |= (ThrustFlags)(1 << blkDir);
				}

				//
				Inc("All Thrusters", thr);

				if (flags.HasFlag(ThrustFlags.Atmospheric)) Inc("Atmospheric", thr);
				if (flags.HasFlag(ThrustFlags.Hydrogen)) Inc("Hydrogen", thr);
				if (flags.HasFlag(ThrustFlags.Ion)) Inc("Ion", thr);

				if (flags.HasFlag(ThrustFlags.Front)) Inc("Front", thr);
				if (flags.HasFlag(ThrustFlags.Back)) Inc("Back", thr);
				if (flags.HasFlag(ThrustFlags.Left)) Inc("Left", thr);
				if (flags.HasFlag(ThrustFlags.Right)) Inc("Right", thr);
				if (flags.HasFlag(ThrustFlags.Top)) Inc("Top", thr);
				if (flags.HasFlag(ThrustFlags.Bottom)) Inc("Bottom", thr);
			}
			void Inc(string name, IMyThrust thr) {
				Counters cnt;
				if (!namedCounters.TryGetValue(name, out cnt)) {
					namedCounters.Add(name, cnt = new Counters());
				}
				if (thr.IsWorking) cnt.enabled++; else cnt.disabled++;
				cnt.curThrust += thr.ThrustOverride;
				cnt.maxThrust += thr.MaxThrust;
				cnt.maxEffThrust += thr.MaxEffectiveThrust;
			}

			public string GetText(int type, string pfx, int pad = 0) {
				string nme = $"{pfx}:".PadRight(pad);
				Counters cnt;
				switch (type) {
				case 1: {
					// Number of thrusters which are on/off
					if (!namedCounters.TryGetValue(pfx, out cnt)) {
						return $"{nme}-- / --";
					} else if (cnt.disabled == 0) {
						return $"{nme}ON {cnt.enabled} / --";
					} else if (cnt.enabled == 0) {
						return $"{nme}-- / {cnt.disabled} OFF";
					} else {
						return $"{nme}on {cnt.enabled} / {cnt.disabled} off";
					}
				}
				case 2: {
					// The current override value of thrusters
					if (!namedCounters.TryGetValue(pfx, out cnt)) {
						return $"{nme}???%  ??? N";
					}
					float pct = 0 < cnt.maxThrust ? (100 * cnt.curThrust) / cnt.maxThrust : 0;
					return $"{nme}{pct:F0}%  {cnt.curThrust:F0} N";
				}
				}
				return $"{nme}???";
			}
		}
	}
}
