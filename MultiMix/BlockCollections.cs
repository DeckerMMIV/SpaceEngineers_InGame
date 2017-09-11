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
		public static T NextBlockInGrid<T>(Program pgm, IMyTerminalBlock gridRef, T curBlk, int dir) where T : class, IMyTerminalBlock {
			var lst = new List<T>();
			pgm.GridTerminalSystem.GetBlocksOfType(lst,b=>(null==gridRef || SameGrid(gridRef,b)));
			if (lst.Count<=0)
				return null;
			if (null==curBlk)
				return lst[0];
			int i=0;
			while (lst[i].EntityId != curBlk.EntityId && ++i < lst.Count) {}
			return lst[(i+dir) % lst.Count];
		}

		//------

		public static void ActionOnBlocksOfType<T>(Program pgm, Action<T> act) where T : class, IMyTerminalBlock {
			pgm.GridTerminalSystem.GetBlocksOfType((List<T>)null, b => {
				act(b as T);
				return false;
			});
		}

		public static void ActionOnBlocksOfType<T>(Program pgm, IMyTerminalBlock gridRef, Action<T> act) where T : class, IMyTerminalBlock {
			pgm.GridTerminalSystem.GetBlocksOfType((List<T>)null, b => {
				if (SameGrid(gridRef,b))
					act(b as T);
				return false;
			});
		}

		//------

		public static void GatherBlocks(Program pgm, List<Func<IMyTerminalBlock, bool>> pipeline) {
			pgm.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, b => {
				// Iterate through pipeline, until a segment returns `false`
				foreach (var f in pipeline)
					if (!f(b))
						break;
				return false;
			});
		}

		//------

		public static List<IMyTerminalBlock> GetBlocksOfType(Program pgm, string blockType, IMyTerminalBlock gridRef=null, string customName = null, bool negName = false) {
			return GetBlocksOfType(new List<IMyTerminalBlock>(),pgm,blockType,gridRef,customName,negName);
		}

		public static List<IMyTerminalBlock> GetBlocksOfType(List<IMyTerminalBlock> blks, Program pgm, string blockType, IMyTerminalBlock gridRef=null, string customName = null, bool negName = false) {
			Func<IMyTerminalBlock, bool> fCmp = (b) => ((null==gridRef || SameGrid(b, gridRef)) && (null==customName || (NameContains(b, customName) ? !negName : negName)));
			var gts = pgm.GridTerminalSystem;

			switch (blockType.ToLower()) {
			case "drill": case "shipdrill":
				gts.GetBlocksOfType<IMyShipDrill>(blks, fCmp); break;
			case "grinder": case "shipgrinder":
				gts.GetBlocksOfType<IMyShipGrinder>(blks, fCmp); break;
			case "welder": case "shipwelder":
				gts.GetBlocksOfType<IMyShipWelder>(blks, fCmp); break;
			case "sorter": case "conveyorsorter":
				gts.GetBlocksOfType<IMyConveyorSorter>(blks, fCmp); break;
			case "connector": case "shipconnector":
				gts.GetBlocksOfType<IMyShipConnector>(blks, x => !SubtypeContains(x, "ConnectorSmall") && fCmp(x)); break;
			case "ejector":
				gts.GetBlocksOfType<IMyShipConnector>(blks, x => SubtypeContains(x, "ConnectorSmall") && fCmp(x)); break;
			case "collector":
				gts.GetBlocksOfType<IMyCollector>(blks, fCmp); break;
			case "light":
				gts.GetBlocksOfType<IMyLightingBlock>(blks, fCmp); break;
			case "gyro":
				gts.GetBlocksOfType<IMyGyro>(blks, fCmp); break;
			case "sensor":
				gts.GetBlocksOfType<IMySensorBlock>(blks, fCmp); break;
			case "detector": case "oredetector":
				gts.GetBlocksOfType<IMyOreDetector>(blks, fCmp); break;
			case "gear": case "landinggear":
				gts.GetBlocksOfType<IMyLandingGear>(blks, fCmp); break;
			case "remote": case "remotecontrol":
				gts.GetBlocksOfType<IMyRemoteControl>(blks, fCmp); break;
			case "button": case "buttonpanel":
				gts.GetBlocksOfType<IMyButtonPanel>(blks, fCmp); break;
			case "radio": case "antenna": case "radioantenna":
				gts.GetBlocksOfType<IMyRadioAntenna>(blks, fCmp); break;
			case "laser": case "laserantenna":
				gts.GetBlocksOfType<IMyLaserAntenna>(blks, fCmp); break;
			case "sound": case "soundblock":
				gts.GetBlocksOfType<IMySoundBlock>(blks, fCmp); break;
			case "lcd": case "textpanel": case "display":
				gts.GetBlocksOfType<IMyTextPanel>(blks, fCmp); break;
			case "solar": case "solarpanel":
				gts.GetBlocksOfType<IMySolarPanel>(blks, fCmp); break;
			case "battery":
				gts.GetBlocksOfType<IMyBatteryBlock>(blks, fCmp); break;
			case "reactor":
				gts.GetBlocksOfType<IMyReactor>(blks, fCmp); break;
			case "cargo": case "container":
				gts.GetBlocksOfType<IMyCargoContainer>(blks, fCmp); break;
			case "parachute":
				gts.GetBlocksOfType<IMyParachute>(blks, fCmp); break;
			case "farm": case "oxygenfarm":
				gts.GetBlocksOfType<IMyOxygenFarm>(blks, fCmp); break;
			case "generator": case "oxygengenerator": case "gasgenerator":
				gts.GetBlocksOfType<IMyGasGenerator>(blks, fCmp); break;
			case "tank": case "gastank":
				gts.GetBlocksOfType<IMyGasTank>(blks, fCmp); break;
			case "oxygentank":
				gts.GetBlocksOfType<IMyGasTank>(blks, x => SubtypeContains(x, "Oxygen") && fCmp(x)); break;
			case "hydrogentank":
				gts.GetBlocksOfType<IMyGasTank>(blks, x => SubtypeContains(x, "Hydrogen") && fCmp(x)); break;
			case "rotor": case "stator":
				gts.GetBlocksOfType<IMyMotorStator>(blks, fCmp); break;
			case "gatlingturret":
				gts.GetBlocksOfType<IMyLargeGatlingTurret>(blks, fCmp); break;
			case "gatlinggun":
				gts.GetBlocksOfType<IMySmallGatlingGun>(blks, fCmp); break;
			case "interiorturret":
				gts.GetBlocksOfType<IMyLargeInteriorTurret>(blks, fCmp); break;
			case "missileturret":
				gts.GetBlocksOfType<IMyLargeMissileTurret>(blks, fCmp); break;
			case "rocketlauncher": case "missilelauncher":
				gts.GetBlocksOfType<IMySmallMissileLauncher>(blks, fCmp); break;
			case "hangar": case "hangardoor":
				gts.GetBlocksOfType<IMyAirtightHangarDoor>(blks, fCmp); break;
			case "slidedoor":
				gts.GetBlocksOfType<IMyAirtightSlideDoor>(blks, fCmp); break;
			case "advanceddoor":
				gts.GetBlocksOfType<IMyAdvancedDoor>(blks, fCmp); break;
			case "door":
				gts.GetBlocksOfType<IMyDoor>(blks, x=> !(x is IMyAirtightHangarDoor || x is IMyAirtightSlideDoor || x is IMyAdvancedDoor) && fCmp(x)); break;
			case "cryo": case "cryochamber":
				gts.GetBlocksOfType<IMyCryoChamber>(blks, fCmp); break;
			case "refinery":
				gts.GetBlocksOfType<IMyRefinery>(blks, fCmp); break;
			case "assembler":
				gts.GetBlocksOfType<IMyAssembler>(blks, fCmp); break;
			default:
				throw new Exception($"Not supported blocktype-id: '{blockType}'");
			}
			return blks;
		}
	}
}
