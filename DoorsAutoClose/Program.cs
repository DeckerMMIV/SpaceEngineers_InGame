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
		const string DoorsAutoClose_UsedBlocks = "DoorsAutoClose";

		// Blocks with this in their custom-name, will not be used.
		const string DoorsAutoClose_IgnoreBlocks = "IGNORE";





		// -- Program ------------------------------------------------
		public Program() {
		}

		public void Save() {
		}

		public void Main(string args) {
			if (null!=profiler) {
				profiler.Append($";{Runtime.LastRunTimeMs:F3}\n{DateTime.Now.Ticks};{Runtime.TimeSinceLastRun.Ticks}");
				if (99900 < profiler.Length) { args="PROFILER"; } // Force-Stop the profiler!
			}
			try {
				Main1(args);
			} catch (Exception e) {
				Echo($"FAILED!\n{e.Message}");
				//lcd.ShowPublicText($"Program Block Runtime Error!\n{e.Message}");
				// TODO - Future enhancement. If failed because of referencing non-existing block, then try to automatically recover - except if its the timer-block.
			}
			profiler?.Append($";{Runtime.CurrentInstructionCount}");
		}

		StringBuilder profiler = null;

		void Main1(string args) {
			bool fastTrigger = false;
			if (!lastRunSuccess) {
				Init();
				fastTrigger = true;
			} else {
				lastRunSuccess = false;
				totalTicks += Runtime.TimeSinceLastRun.Ticks;

				fastTrigger |= Tick(yieldMgr);
			}
			if (null != timerBlock) {
				// This is why timer-block should be configured to ONLY execute PB and NOTHING ELSE!
				if (fastTrigger)
					triggerNow.Apply(timerBlock);
				else
					timerBlock.ApplyAction("Start");
			}
			lastRunSuccess = true;
		}

		long totalTicks;
		bool lastRunSuccess = false;

		void Init() {
			if (!NameContains(Me, DoorsAutoClose_UsedBlocks))
				throw new Exception($"Programmable block does not have '{DoorsAutoClose_UsedBlocks}' in its custom-name.\nDid you read the instructions?");

			InitTimerBlock(DoorsAutoClose_UsedBlocks);

			yieldMgr = yieldMgr ?? new YieldModule(this);

			yieldMgr.Add(DoorCloserLooper());
		}

		IMyTimerBlock timerBlock = null;
		ITerminalAction triggerNow = null;
		void InitTimerBlock(string tbName) {
			// Try locating a timer-block that also contain the word(s)
			timerBlock = null;
			triggerNow = null;
			int cnt = 0;
			ActionOnBlocksOfType<IMyTimerBlock>(this, Me, b=>{
				if (b.IsWorking && NameContains(b, tbName) && !NameContains(b,DoorsAutoClose_IgnoreBlocks)) {
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

		//--------------

		
		Dictionary<long, long> openDoors = new Dictionary<long, long>();
		int numIteration = 0;

		IEnumerable<int> DoorCloserLooper() {
			int thisIteration = ++numIteration; // In case multiple calls to this `DoorCloserLooper` occurs, then only the "last one" should survive.

			var newDoors = new List<IMyDoor>();
			var obsoleteDoors = new List<long>();
			var closingDoors = new List<long>();

			while (thisIteration == numIteration) {
				GridTerminalSystem.GetBlocksOfType(newDoors, x=>SameGrid(x,Me) & !NameContains(x,DoorsAutoClose_IgnoreBlocks));

				long nowTick = DateTime.Now.Ticks;
				long closeAtTick = nowTick + TimeSpan.TicksPerSecond * 3;

				foreach(var d in newDoors) {
					if (d is IMyAirtightHangarDoor)
						continue;

					if (d.IsWorking && 2 > (int)d.Status) {
						if (!openDoors.ContainsKey(d.EntityId)) {
							openDoors.Add(d.EntityId, closeAtTick);
						}
					}
				}
				newDoors.Clear();

				var doorsToClose = new List<IMyTerminalBlock>();
				foreach(var d in openDoors) {
					if (d.Value < nowTick) {
						var door = GridTerminalSystem.GetBlockWithId(d.Key);
						if (null != door) {
							closingDoors.Add(d.Key);
							doorsToClose.Add(door);
						} else {
							obsoleteDoors.Add(d.Key);
						}
					}
				}

				foreach(var k in obsoleteDoors) {
					openDoors.Remove(k);
				}
				obsoleteDoors.Clear();

				foreach(var k in closingDoors) {
					openDoors[k] = DateTime.MaxValue.Ticks;
				}
				closingDoors.Clear();

				if (0 < doorsToClose.Count) {
					yieldMgr.Add(CloseDoors(doorsToClose));
				}

				yield return 1000;
			}
		}

		IEnumerable<int> CloseDoors(List<IMyTerminalBlock> doorsToClose) {
			// Flash doors that are about to close
			for(int i=3; i>0; i--) {
				foreach(var d in doorsToClose) {
					try {
						var door = d as IMyDoor;
						if (DoorStatus.Open == door.Status) {
							door.Enabled = false;
						}
					} catch (Exception) {
						// Ignore
					}
				}
				yield return 200;

				try {
					SetEnabled(doorsToClose, true);
				} catch (Exception) {
					// Ignore
				}
				yield return 200;
			}

			foreach(var d in doorsToClose) {
				try {
					var door = d as IMyDoor;
					door.Enabled = true;
					door.CloseDoor();
				} catch(Exception) {
					// Ignore
				}
			}
			yield return 1000;

			foreach(var d in doorsToClose) {
				openDoors.Remove(d.EntityId);
			}
		}
	}
}