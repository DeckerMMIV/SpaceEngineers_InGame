﻿using Sandbox.Game.EntityComponents;
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
	partial class Program : MyGridProgram {
		// -- User settings ------------------------------------------

		// Blocks with this in their custom-name will be used.
		const string MultiMix_UsedBlocks = "MULTIMIX";

		// Blocks with this in their custom-name, will not be used.
		const string MultiMix_IgnoreBlocks = "IGNORE";




		//--------------------------------------------------------------
		//--------------------------------------------------------------
		//
		// Engine room - Warp chamber - Hot plasma
		// No admittance! Authorised personnel only!
		//
		//--------------------------------------------------------------
		//--------------------------------------------------------------
		const string scriptVersion = "4.4.1"; // 2018-03-24

		Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
		}
		void Save() { }

		StringBuilder profiler = null;

		void Main(string args, UpdateType updateSource) {
			if (null!=profiler) {
				profiler.Append($";{Runtime.LastRunTimeMs:0.000}\n{DateTime.Now.Ticks};{Runtime.TimeSinceLastRun.Ticks}");
				if (99900 < profiler.Length)
					args="PROFILER"; // Force-Stop the profiler!
			}
			try {
				Main1(args);
			} catch (System.Exception excp) {
				Echo($"FAILED!\n{excp.Message}");
				lcdCntr.ShowPublicText($"Program Block Runtime Error!\n{excp.Message}");
			}
			profiler?.Append($";{Runtime.CurrentInstructionCount}");
		}

		void Main2(string args) {
			Runtime.UpdateFrequency = UpdateFrequency.None;
			var blks = GetBlocksOfType(this, args);
			if (1 > blks.Count) {
				Echo($"No blocks found for; {args}");
				return;
			}

			var blk = blks[0];
			Echo(blk.GetType().ToString());

			var acts = new List<ITerminalAction>();
			blk.GetActions(acts);
			Echo($"== Actions for; {args}");
			foreach(var a in acts)
				Echo(a.Id);

			var prps = new List<ITerminalProperty>();
			blk.GetProperties(prps);
			Echo($"== Properties for; {args}");
			foreach(var p in prps)
				Echo($"{p.Id} / {p.TypeName}");
		}

		long totalTicks;
		long mnuUpdTick;
		bool lastRunSuccess = false;
		OutputPanel lcdLeft = new OutputPanel();
		OutputPanel lcdCntr = new OutputPanel();
		OutputPanel lcdRght = new OutputPanel();

		void Main1(string args) {
			var nxtUpdFrq = UpdateFrequency.None;
			if (!lastRunSuccess) {
				Init();
				nxtUpdFrq |= UpdateFrequency.Update1;
			} else {
				lastRunSuccess = false;
				totalTicks += Runtime.TimeSinceLastRun.Ticks;

				var updMnu = mnuUpdTick < totalTicks;
				if (0 == args.Length) {
					var sc = GetShipController();
					if (null != sc && !sc.ControlThrusters) {
						nxtUpdFrq |= UpdateFrequency.Update1;
						args = MoveIndicator2Command(sc.MoveIndicator);
					}
				}
				if (0 < args.Length)
					updMnu |= ProgramCommand(args);

				nxtUpdFrq |= Tick(ascDecMgr);
				nxtUpdFrq |= Tick(yieldMgr);

				if (updMnu) {
					mnuUpdTick = totalTicks + TPS;
					menuMgr.DrawMenu(lcdCntr);
				}
			}
			Runtime.UpdateFrequency = nxtUpdFrq;
			lastRunSuccess = true;
		}

		void Init() {
			if (!NameContains(Me, MultiMix_UsedBlocks))
				throw new Exception($"Programmable block does not have '{MultiMix_UsedBlocks}' in its custom-name.\nDid you read the instructions?");

			lcdLeft.Clear();
			lcdCntr.Clear();
			lcdRght.Clear();
			var lcdBearing = new OutputPanel();
			ActionOnBlocksOfType<IMyTextPanel>(this, Me, p=>{
				if (p.IsWorking && !NameContains(p, MultiMix_IgnoreBlocks))
					if (NameContains(p, "Center"))
						lcdCntr.Add(p);
					else if (NameContains(p, "Left"))
						lcdLeft.Add(p);
					else if (NameContains(p, "Right"))
						lcdRght.Add(p);
					else if (NameContains(p, "Bearing"))
						lcdBearing.Add(p);
			});
			lcdLeft.ShowPublicText("Initializing: Left panel(s)");
			lcdCntr.ShowPublicText("Initializing: Center panel(s)");
			lcdRght.ShowPublicText("Initializing: Right panel(s)");

			var sc = GetShipController(MultiMix_UsedBlocks, true);

			var alignActive = alignMgr?.Active ?? false;
			var cargoActive = cargoMgr?.Active ?? true;

			alignMgr = alignMgr ?? new AlignModule(this);
			ascDecMgr = ascDecMgr ?? new AscendDecendModule(this);
			cargoMgr = cargoMgr ?? new CargoModule(this);
			yieldMgr = yieldMgr ?? new YieldModule(this);

			alignMgr.Refresh(ascDecMgr, sc);
			ascDecMgr.Refresh(lcdRght, alignMgr, sc);
			cargoMgr.Refresh(lcdLeft);

			alignMgr.Active = alignActive;
			cargoMgr.Active = cargoActive;

			engineMgr = engineMgr ?? new EngineModule(this);
			engineMgr.Refresh(Me, sc);

			toolsMgr = toolsMgr ?? new ToolsModule(this);
			toolsMgr.Refresh();

			bearingMgr = bearingMgr ?? new Whip_Bearing(this);
			bearingMgr.Refresh(lcdBearing, sc);
			bearingMgr.Active = (lcdBearing.Count > 0);

			menuMgr = menuMgr ?? new MenuManager(this);
			BuildMenu();
		}

		bool ProgramCommand(string cmd) {
			switch (cmd.ToUpper()) {
			case "RESET":
				Init();
				return true;
			case "HELP":
				var sb = new StringBuilder();
				sb.Append("== Direct Commands ==");
				foreach(var txt in menuMgr.AllDirectCommands())
					sb.Append($"\n  {txt}");
				sb.Append("\n======");
				Echo(sb.ToString());
				return true;
			case "TOGGLECONTROL":
				var sc = GetShipController();
				if (null != sc) {
					foreach(IMyGyro g in GetBlocksOfType(this,"gyro",Me))
						SetGyro(g,1,sc.ControlThrusters);
					sc.ControlThrusters = !sc.ControlThrusters;
				}
				return false;
			case "PROFILER":
				if (null == profiler) {
					profiler = new StringBuilder();
					profiler.Append($"NowTick;TimeSinceLastRunTicks;CurrentInstructionCount;RunTimeMs\n-1;-1");
					Echo("Profiler activated.");
				} else {
					Me.CustomData = profiler.Append(";-1;-1\n").ToString();
					profiler.Clear();
					profiler = null;
					Echo("Profiler deactivated.\nPB's CustomData filled.");
				}
				return false;
			default:
				return menuMgr.DoAction(cmd);
			}
		}

		void BuildMenu() {
			menuMgr.Clear();

			alignMgr?.AddMenu(menuMgr);
			toolsMgr?.AddMenu(menuMgr);
			engineMgr?.AddMenu(menuMgr);
			ascDecMgr?.AddMenu(menuMgr);

			menuMgr.Add(
				Menu("Misc. operations").Add(
					Menu(() => $"Unlock from connector{ConnectorUnlockInfo}")
						.Enter("connectorUnlock", () => yieldMgr.Add(UnlockFromConnector())),
					Menu(() => $"Lock-on to connector{ConnectorLockInfo}")
						.Enter("connectorLock", () => yieldMgr.Add(AttemptLockToConnector())),
					Menu("Radio: Hangar doors toggle").Enter("radioHangarDoors", () => {
						var lst = GetBlocksOfType(this,"radioantenna",Me);
						if (0 < lst.Count)
							(lst[0] as IMyRadioAntenna)?.TransmitMessage("HangarDoors", MyTransmitTarget.Default | MyTransmitTarget.Neutral);
					})
				)
			);

			menuMgr.Add(
				Menu("My often used sequence").Add(
					Menu(() => $"Lock-on to connector{ConnectorLockInfo}")
						.Enter(()=>menuMgr.DoAction("connectorLock")),
					Menu(() => $"Unlock from connector{ConnectorUnlockInfo}")
						.Enter(()=>menuMgr.DoAction("connectorUnlock")),
					Menu(() => LabelOnOff(alignMgr?.Active ?? false, "Align:", "ENABLED", "OFF"))
						.Enter(()=>menuMgr.DoAction("toggleAlign")),
					Menu(()=>LabelOnOff(cruiseState, $"Cruise forward (max {maxTopSpeed}):"))
						.Enter(()=>ForwardCruise())
						.Left(()=>maxTopSpeed=Math.Max(5,maxTopSpeed-5))
						.Right(()=>maxTopSpeed=Math.Min(100,maxTopSpeed+5))
						.Back(()=>maxTopSpeed=90)
				)
			);
		}

		//---------------

		private IMyShipController shipCtrl = null;
		IMyShipController GetShipController(string primaryName = null, bool reset = false) {
			if (null == shipCtrl || reset) {
				shipCtrl = null;
				var lst = new List<IMyShipController>();
				GridTerminalSystem.GetBlocksOfType(lst, b => {
					if (!b.CanControlShip || !b.IsWorking || !SameGrid(Me, b))
						return false;
					if (null != primaryName && NameContains(b, primaryName))
						shipCtrl = b as IMyShipController;
					return (b as IMyShipController).ControlThrusters;
				});
				if (null == shipCtrl && 0 < lst.Count)
					shipCtrl = lst[0] as IMyShipController;
			}
			return shipCtrl;
		}

		//---------------

		double maxTopSpeed = 90;
		bool cruiseState = false;
		void ForwardCruise() {
			if (cruiseState=!cruiseState) {
				engineMgr.tsEnable(ThrustFlags.Front,false);
				yieldMgr.Add(AccelerateCruise(++cruiseInstance));
			} else {
				++cruiseInstance;
				engineMgr.tsEnable(ThrustFlags.Front,true);
			}
		}
		int cruiseInstance = 0;
		IEnumerable<int> AccelerateCruise(int selfInstance) {
			yield return 1;
			engineMgr.tsPower(ThrustFlags.Back,100);
			while (selfInstance == cruiseInstance && shipCtrl.GetShipSpeed() < maxTopSpeed) {
				yield return 20;
			}
			engineMgr.tsPower(ThrustFlags.Back,0);
		}

		//---------------

		public readonly string[] DIRECTIONS = {"Front","Back","Left","Right","Top","Bottom"};

		//---------------

		private string unlockInfo="";
		public string ConnectorUnlockInfo {
			get { return unlockInfo; }
			set { unlockInfo = value.Length>0 ? $"  [{value}]" : ""; }
		}
		IEnumerable<int> UnlockFromConnector() {
			ConnectorLockInfo="";
			var lst = new List<IMyTerminalBlock>();

			var isLocked = false;
			foreach(var b in GetBlocksOfType(lst,this,"connector",Me,MultiMix_IgnoreBlocks,true))
				if (MyShipConnectorStatus.Connected == (b as IMyShipConnector).Status)
					isLocked = true;
			if (!isLocked) {
				ConnectorUnlockInfo="Not locked";
				yield return 2000;
				ConnectorUnlockInfo="";
				yield break;
			}
			ConnectorUnlockInfo="Attempting unlock";
			foreach(var b in GetBlocksOfType(lst,this,"battery",Me))
				(b as IMyBatteryBlock).OnlyDischarge = true;
			var rc = GetShipController();
			if (null != rc)
				rc.DampenersOverride = true;
			yield return 10;

			var atmosphere = -1f;
			foreach(var b in GetBlocksOfType(lst,this,"parachute",Me)) {
				var p = b as IMyParachute;
				if (p.IsWorking)
					atmosphere = Math.Max(atmosphere, p.Atmosphere);
			}
			ThrustFlags tf = atmosphere > 0.5 ? ThrustFlags.Atmospheric : ThrustFlags.Ion;
			SetEnabled(GetThrustBlocks(lst, tf, this, Me), true);
			yield return 100;

			foreach(var b in GetBlocksOfType(lst,this,"landinggear",Me))
				(b as IMyLandingGear).Unlock();
			foreach(var b in GetBlocksOfType(lst,this,"connector",Me,MultiMix_IgnoreBlocks,true))
				(b as IMyShipConnector).Disconnect();
			yield return 100;

			SetEnabled(GetBlocksOfType(lst,this,"connector",Me,MultiMix_IgnoreBlocks,true),false);
			SetEnabled(GetBlocksOfType(lst,this,"weapons",Me),true);
			ConnectorUnlockInfo="Successful unlock";
			yield return 5000;

			ConnectorUnlockInfo="";
		}

		private string lockInfo = "";
		public string ConnectorLockInfo  {
			get { return lockInfo; }
			set { lockInfo = value.Length>0 ? $"  [{value}]" : ""; }
		}
		IEnumerable<int> AttemptLockToConnector() {
			ConnectorUnlockInfo="";
			var lst = new List<IMyTerminalBlock>();

			var isUnlocked = true;
			foreach(var b in GetBlocksOfType(lst,this,"connector",Me,MultiMix_IgnoreBlocks,true))
				if (MyShipConnectorStatus.Connected == (b as IMyShipConnector).Status)
					isUnlocked = false;
			if (!isUnlocked) {
				ConnectorLockInfo="Already locked";
				yield return 2000;
				ConnectorLockInfo="";
				yield break;
			}
			ConnectorLockInfo="Attempting lock";
			yield return 10;

			foreach(var b in GetBlocksOfType(lst,this,"connector",Me,MultiMix_IgnoreBlocks,true)) {
				var c = b as IMyShipConnector;
				c.PullStrength = 0.0001f;
				c.Enabled = true;
			}
			yield return 100;
			
			int maxAttempts = 10;
			var isLocked = false;
			do {
				ConnectorLockInfo=$"Attempting ({maxAttempts})";
				foreach(var b in GetBlocksOfType(lst,this,"connector",Me,MultiMix_IgnoreBlocks,true)) {
					var c = b as IMyShipConnector;
					if (MyShipConnectorStatus.Connected == c.Status)
						isLocked = true;
					else
						c.Connect();
				}
				lst.Clear();
				if (!isLocked)
					yield return 100;
			} while (!isLocked && --maxAttempts > 0);
			if (!isLocked) {
				ConnectorLockInfo="Failed to lock";
				yield return 2000;
				ConnectorLockInfo="";
				yield break;
			}
			yield return 100;

			if (null!=alignMgr)
				alignMgr.Active=false;
			SetEnabled(GetThrustBlocks(lst,ThrustFlags.All, this, Me), false);
			SetEnabled(GetBlocksOfType(lst,this,"weapons",Me), false);
			foreach(var b in GetBlocksOfType(lst,this,"landinggear",Me))
				(b as IMyLandingGear).Lock();
			foreach(var b in GetBlocksOfType(lst,this,"battery",Me))
				(b as IMyBatteryBlock).OnlyRecharge = true;
			ConnectorLockInfo="Successful lock";
			yield return 5000;

			ConnectorLockInfo="";
		}

		private readonly string[] miCmd = {"LEFT","","RIGHT","UP","","DOWN","BACK","","ENTER"};
		private bool keysReleased = true;
		string MoveIndicator2Command(Vector3 mi) {
			var cmd=miCmd[1 + Math.Sign(mi.X)]+
					miCmd[4 + Math.Sign(mi.Z)]+
					miCmd[7 + Math.Sign(mi.Y)];
			if (cmd.Length == 0)
				keysReleased = true;
			else if (!keysReleased)
				return "";
			else
				keysReleased = false;
			return cmd;
		}
 	}
}
