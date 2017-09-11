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
		const string scriptVersion = "3.3.0"; // 2017-09-10

		Program() { }
		void Save() { }

		void Main(string args) {
			try {
				Main1(args);
				//if (Me.CustomData.Length < 64*1024) {
				//	Me.CustomData = Me.CustomData + $"{Runtime.LastRunTimeMs:F3}\n{Runtime.TimeSinceLastRun.TotalMilliseconds:F3}/{Runtime.CurrentInstructionCount}/";
				//}
			} catch (System.Exception e) {
				Echo($"FAILED! {e.Message}");
				lcdCenter.ShowPublicText($"Program Block Runtime Error!\n{e.Message}");
				// TODO - Future enhancement. If failed because of referencing non-existing block, then try to automatically recover - except if its the timer-block.
			}
		}

		void Main2(string args) {
			var blks = GetBlocksOfType(this, args);
			if (0 >= blks.Count) {
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
		long menuUpdateTick;
		bool lastRunSuccess = false;
		OutputPanel lcdLeft = new OutputPanel();
		OutputPanel lcdCenter = new OutputPanel();
		OutputPanel lcdRight = new OutputPanel();
		IMyTimerBlock timerBlock = null;
		ITerminalAction triggerNow = null;

		void Main1(string args) {
			bool fastTrigger = false;
			if (!lastRunSuccess) {
				Init();
				fastTrigger = true;
			} else {
				lastRunSuccess = false;
				totalTicks += Runtime.TimeSinceLastRun.Ticks;

				bool updateMenu = menuUpdateTick < totalTicks;
				if (args.Length > 0)
					updateMenu |= ProgramCommand(args);

				//
				fastTrigger |= Tick(alignMgr);
				fastTrigger |= Tick(ascDecMgr);
				fastTrigger |= Tick(cargoMgr);
				fastTrigger |= Tick(yieldMgr);

				//
				if (updateMenu) {
					menuMgr.DrawMenu(lcdCenter);
					menuUpdateTick = totalTicks + TimeSpan.TicksPerSecond;
				}
			}
			if (null!=timerBlock) {
				//if (!timerBlock.StillExist()) { return; } // Failure!
				// This is why timer-block should be configured to ONLY execute PB and NOTHING ELSE!
				if (fastTrigger)
					triggerNow.Apply(timerBlock);
				else
					timerBlock.ApplyAction("Start");
			}
			lastRunSuccess = true;
		}

		void Init() {
			if (!NameContains(Me, MultiMix_UsedBlocks))
				throw new Exception($"Programmable block does not have '{MultiMix_UsedBlocks}' in its custom-name.\nDid you read the instructions?");

			InitTimerBlock(MultiMix_UsedBlocks);

			lcdLeft.Clear();
			lcdCenter.Clear();
			lcdRight.Clear();
			ActionOnBlocksOfType<IMyTextPanel>(this, Me, p=>{
				if (p.IsWorking && !NameContains(p, MultiMix_IgnoreBlocks))
					if (NameContains(p, "Center"))
						lcdCenter.Add(p);
					else if (NameContains(p, "Left"))
						lcdLeft.Add(p);
					else if (NameContains(p, "Right"))
						lcdRight.Add(p);
			});
			lcdLeft.ShowPublicText("Initializing: Left panel(s)");
			lcdCenter.ShowPublicText("Initializing: Center panel(s)");
			lcdRight.ShowPublicText("Initializing: Right panel(s)");

			var sc = GetShipController(MultiMix_UsedBlocks, true);

			if (null!=timerBlock) {
				alignMgr = alignMgr ?? new AlignModule(this);
				ascDecMgr = ascDecMgr ?? new AscendDecendModule(this);
				cargoMgr = cargoMgr ?? new CargoModule(this);
				yieldMgr = yieldMgr ?? new YieldModule(this);

				alignMgr.Refresh(ascDecMgr, sc);
				ascDecMgr.Refresh(lcdRight, alignMgr, sc);
				cargoMgr.Refresh(lcdLeft, ascDecMgr);
			} else {
				alignMgr = null;
				ascDecMgr = null;
				cargoMgr = null;
				yieldMgr = null;
			}

			engineMgr = engineMgr ?? new EngineModule(this);
			engineMgr.Refresh(Me, sc);

			toolsMgr = toolsMgr ?? new ToolsModule(this);
			toolsMgr.Refresh();

			menuMgr = menuMgr ?? new MenuManager(this);
			BuildMenu();
		}

		void InitTimerBlock(string tbName) {
			// Try locating a timer-block that also contain the word(s)
			timerBlock = null;
			triggerNow = null;
			int cnt = 0;
			ActionOnBlocksOfType<IMyTimerBlock>(this, Me, b=>{
				if (b.IsWorking && NameContains(b, tbName) && !NameContains(b,MultiMix_IgnoreBlocks)) {
					ToType(b, ref timerBlock);
					cnt++;
				}
			});
			if (null == timerBlock) {
				Echo($"WARNING: TimerBlock for PB not found. Name should contain '{tbName}' and block must be enabled.");
				return;
			}
			if (1 != cnt)
				throw new Exception($"More than a required just one TimerBlock found, where '{tbName}' is contained in their names.");

			timerBlock.TriggerDelay = 1;
			triggerNow = timerBlock.GetActionWithName("TriggerNow");
		}

		bool ProgramCommand(string cmd) {
			switch (cmd.ToUpper()) {
			case "RESET":
				Init();
				return true;
			case "HELP":
				var sb = new StringBuilder();
				sb.Append("== Direct Commands ==");
				foreach(string txt in menuMgr.AllDirectCommands())
					sb.Append($"\n  {txt}");
				sb.Append("\n======");
				Echo(sb.ToString());
				return true;
			default:
				return menuMgr.DoAction(cmd);
			}
		}

		void BuildMenu() {
			menuMgr.Clear();
			menuMgr.WarningText = null==timerBlock ? "\n TimerBlock missing. Some features unavailable!" : "";

			//
			alignMgr?.AddMenu(menuMgr);
			toolsMgr?.AddMenu(menuMgr);
			engineMgr?.AddMenu(menuMgr);
			ascDecMgr?.AddMenu(menuMgr);

			//
			MenuItem tt;
			menuMgr.Add(tt=Menu("Misc. operations"));
			if (null!=yieldMgr)
				tt.Add(
					Menu(() => $"Unlock from connector{ConnectorUnlockInfo}")
						.Enter("connectorUnlock", () => yieldMgr.Add(UnlockFromConnector())),
					Menu(() => $"Lock-on to connector{ConnectorLockInfo}")
						.Enter("connectorLock", () => yieldMgr.Add(AttemptLockToConnector()))
				);

			tt.Add(
				Menu("Radio: Hangar doors toggle").Enter("radioHangarDoors", () => {
					var lst = GetBlocksOfType(this,"radioantenna",Me);
					if (0 < lst.Count)
						(lst[0] as IMyRadioAntenna)?.TransmitMessage("HangarDoors", MyTransmitTarget.Default | MyTransmitTarget.Neutral);
				})
			);
		}

		//---------------

		private IMyTerminalBlock shipCtrl = null;
		IMyShipController GetShipController(string primaryName = null, bool reset = false) {
			if (null == shipCtrl || reset) {
				shipCtrl = null;
				var lst = new List<IMyShipController>();
				GridTerminalSystem.GetBlocksOfType(lst, b => {
					if (!b.IsWorking || !SameGrid(b, Me))
						return false;
					if (null != primaryName && NameContains(b, primaryName))
						shipCtrl = b;
					return (b as IMyShipController).ControlThrusters;
				});
				if (null == shipCtrl && 0 < lst.Count)
					shipCtrl = lst[0];
			}
			return shipCtrl as IMyShipController;
		}

		//---------------

		public readonly string[] DIRECTIONS = {"Front","Back","Left","Right","Top","Bottom"};

		private string unlockInfo="";
		public string ConnectorUnlockInfo {
			get { return unlockInfo; }
			set { unlockInfo = value.Length>0 ? $"  [{value}]" : ""; }
		}
		IEnumerable<int> UnlockFromConnector() {
			ConnectorLockInfo="";

			bool isLocked = false;
			ActionOnBlocksOfType<IMyShipConnector>(this, Me, b => {
				if (MyShipConnectorStatus.Connected == b.Status)
					isLocked = true;
			});
			if (!isLocked) {
				ConnectorUnlockInfo="Not locked";
				yield return 2000;
				ConnectorUnlockInfo="";
				yield break;
			}
			ConnectorUnlockInfo="Attempting unlock";
			yield return 10;

			ActionOnBlocksOfType<IMyBatteryBlock>(this, Me, b => {
				b.OnlyDischarge = true;
			});
			yield return 100;

			IMyShipController rc = GetShipController();
			if (null != rc)
				rc.DampenersOverride = true;
			yield return 10;

			float atmosphere = 0;
			ActionOnBlocksOfType<IMyParachute>(this, Me, b => {
				if (b.IsWorking)
					atmosphere = Math.Max(atmosphere, b.Atmosphere);
			});
			ThrustFlags tf = atmosphere > 0.3 ? ThrustFlags.Atmospheric : ThrustFlags.Ion;
			SetEnabled(GetThrustBlocks(tf, this, Me), true);
			yield return 100;

			GetBlocksOfType(this,"connector",Me).ForEach(c=>(c as IMyShipConnector).Disconnect());
			yield return 100;
			
			SetEnabled(GetBlocksOfType(this,"connector",Me),false);
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

			bool isUnlocked = true;
			ActionOnBlocksOfType<IMyShipConnector>(this, Me, b => {
				if (MyShipConnectorStatus.Connected == b.Status)
					isUnlocked = false;
			});
			if (!isUnlocked) {
				ConnectorLockInfo="Already locked";
				yield return 2000;
				ConnectorLockInfo="";
				yield break;
			}
			ConnectorLockInfo="Attempting lock";
			yield return 10;

			ActionOnBlocksOfType<IMyShipConnector>(this, Me, b => {
				b.PullStrength = 0.0001f;
				b.Enabled = true;
			});
			yield return 100;
			
			int maxAttempts = 10;
			bool isLocked = false;
			List<IMyTerminalBlock> lst = new List<IMyTerminalBlock>();
			do {
				ConnectorLockInfo=$"Attempting ({maxAttempts})";
				foreach(var b in GetBlocksOfType(lst,this,"connector",Me)) {
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
			yield return 10;

			SetEnabled(GetThrustBlocks(ThrustFlags.All, this, Me), false);
			yield return 100;

			ActionOnBlocksOfType<IMyBatteryBlock>(this,Me,b => {
				b.OnlyRecharge = true;
			});
			ConnectorLockInfo="Successful lock";
			yield return 5000;
			ConnectorLockInfo="";
		}
 	}
}
