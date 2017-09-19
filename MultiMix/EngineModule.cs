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
			public EngineModule(Program p) : base(p) {}

			override public bool Tick() { return false; }

			public void Refresh(IMyTerminalBlock _myGrid, IMyShipController _sc) {
				myGrid = _myGrid;
				dirRefBlk = _sc;
			}

			public void AddMenu(MenuManager menuMgr) {
				if (1 > GetThrustBlocks(ThrustFlags.All, Pgm, Me).Count)
					return; // No thrusters found

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

				if (null == Pgm.GetShipController())
					menuMgr.WarningText += "\n ShipController missing. Some features unavailable!";
				else {
					Action<ThrustFlags, float> tsPower = (tf, modifier) => {
						var lst = GetThrustBlocks(tf, Pgm, Me, Pgm.GetShipController());
						if (0 != modifier && 0 < lst.Count) {
							float current = 0;
							foreach(var b in lst)
								current += (b as IMyThrust).GetValueFloat("Override");

							if (100f == modifier)
								modifier = 0 < current ? 0 : 1; // Toggle between 0% and 100% thrust
							else {
								current /= lst.Count;
								modifier = MathHelper.Clamp(current + Math.Sign(modifier) * 0.1f, 0, 1); // Inc./dec. thrust by 10%
							}
						}
						SetThrustAbsPct(lst, modifier);
					};

					MenuItem md = Menu("Directions");
					MenuItem mo = Menu("Override thrust");
					int[] pad = new[] {p+2,p+1,p+3,p+1,p+2,p+0};

					int i=0;
					foreach(string txt in Pgm.DIRECTIONS) {
						ThrustFlags tf = (ThrustFlags)(1<<i);
						int j=i++;
						md.Add(
							Menu(()=>GetText(1,txt,pad[j]))
								.Collect(this)
								.Enter($"toggle{txt}",()=>tsToggle(tf))
								.Left(()=>tsOn(tf))
								.Right(()=>tsOff(tf))
						);
						mo.Add(
							Menu(()=>GetText(2,txt,pad[j]))
								.Collect(this)
								.Enter($"thrust{txt}",()=>tsPower(tf,100f))
								.Left(()=>tsPower(tf,-1))
								.Right(()=>tsPower(tf,1))
								.Back(()=>tsPower(tf,0))
						);
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
			public void CollectTeardown() {}

			public void CollectBlock(IMyTerminalBlock blk) {
				if (!SameGrid(blk, myGrid) || !blk.IsFunctional)
					return;

				var thr = blk as IMyThrust;
				if (null == thr)
					return;

				ThrustFlags flags = 0;

				if (SubtypeContains(blk, "Atmos"))
					flags |= ThrustFlags.Atmospheric;
				else if (SubtypeContains(blk, "Hydro"))
					flags |= ThrustFlags.Hydrogen;
				else
					flags |= ThrustFlags.Ion;

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
				if (!namedCounters.TryGetValue(name, out cnt))
					namedCounters.Add(name, cnt = new Counters());

				if (thr.IsWorking)
					cnt.enabled++;
				else
					cnt.disabled++;

				cnt.curThrust += thr.ThrustOverride;
				cnt.maxThrust += thr.MaxThrust;
				cnt.maxEffThrust += thr.MaxEffectiveThrust;
			}

			public string GetText(int type, string pfx, int pad = 0) {
				string nme = $"{pfx}:".PadRight(pad);
				Counters cnt;
				switch (type) {
				case 1:
					// Number of thrusters which are on/off
					if (!namedCounters.TryGetValue(pfx, out cnt))
						return $"{nme}-- / --";
					if (0 == cnt.disabled)
						return $"{nme}ON {cnt.enabled} / --";
					if (0 == cnt.enabled)
						return $"{nme}-- / {cnt.disabled} OFF";
					return $"{nme}on {cnt.enabled} / {cnt.disabled} off";
				case 2:
					// The current override value of thrusters
					if (!namedCounters.TryGetValue(pfx, out cnt))
						return $"{nme}???%  ??? N";
					float pct = 0 >= cnt.maxThrust ? 0 : cnt.curThrust / cnt.maxThrust * 100;
					return $"{nme}{pct:F0}%  {cnt.curThrust:F0} N";
				}
				return $"{nme}???";
			}
		}
	}
}
