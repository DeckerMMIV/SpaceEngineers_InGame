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

		// How many seconds must pass, before a detected open door should be closed
		const int DoorsAutoClose_SecondsToKeepOpen = 3;




		// -- Program ------------------------------------------------
		public Program() {
			Runtime.UpdateFrequency = UpdateFrequency.Update100;
		}

		public void Save() {
		}

		public void Main(string args, UpdateType updateSource) {
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
			if ((Runtime.UpdateFrequency & UpdateFrequency.Update10) != (fastTrigger ? UpdateFrequency.Update10 : 0)) {
				Runtime.UpdateFrequency = UpdateFrequency.Update100 | (fastTrigger ? UpdateFrequency.Update10 : 0);
			}
			lastRunSuccess = true;
		}

		long totalTicks;
		bool lastRunSuccess = false;

		void Init() {
			if (!NameContains(Me, DoorsAutoClose_UsedBlocks))
				throw new Exception($"Programmable block does not have '{DoorsAutoClose_UsedBlocks}' in its custom-name.\nDid you read the instructions?");

			yieldMgr = yieldMgr ?? new YieldModule(this);

			yieldMgr.Add(DoorCloserLooper());
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
				long closeAtTick = nowTick + TimeSpan.TicksPerSecond * DoorsAutoClose_SecondsToKeepOpen;

				foreach(var d in newDoors) {
					if (d is IMyAirtightHangarDoor || !d.IsWorking)
						continue;

					switch(d.Status) {
					case DoorStatus.Open:
					case DoorStatus.Opening:
						if (!openDoors.ContainsKey(d.EntityId)) {
							openDoors.Add(d.EntityId, closeAtTick);
						}
						break;
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
			// Note: Using try-catch blocks here, in case the 'block' that is
			// being acted upon, have "vanished" or been destroyed during the
			// time this method gets through its 'yielding enumerator' flow.

			// Flash doors that are about to close
			for(int i=3; i>0; i--) {
				foreach(var d in doorsToClose) {
					try {
						var door = d as IMyDoor;
						if (DoorStatus.Open == door.Status) {
							door.Enabled = false;
						}
					} catch (Exception) {
						// Ignore, and stop at the outer for-loop
						i = 0;
						break;
					}
				}
				yield return 200;

				try {
					SetEnabled(doorsToClose, true);
				} catch (Exception) {
					// Ignore, and break out of the for-loop
					break;
				}
				yield return 200;
			}

			foreach(var d in doorsToClose) {
				try {
					var door = d as IMyDoor;
					door.Enabled = true;
					door.CloseDoor();
				} catch(Exception) {
					// Ignore this door, but still attempt the others
				}
			}
			yield return 1000;

			foreach(var d in doorsToClose) {
				openDoors.Remove(d.EntityId);
			}
		}
	}
}