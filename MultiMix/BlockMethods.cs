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
		public static void ApplyAction(List<IMyTerminalBlock> blocks, string actName) {
			blocks.ForEach(b => b.ApplyAction(actName));
		}

		public static bool SetPropertyAbsToggle(List<IMyTerminalBlock> blocks, string propName) {
			int[] cntOffOn = { 0, 0 };
			blocks.ForEach(b => {
				var blk = b as IMyFunctionalBlock;
				if (null != blk) cntOffOn[blk.GetValueBool(propName) ? 1 : 0]++;
			});
			return SetProperty(blocks, propName, cntOffOn[0] >= cntOffOn[1]);
		}

		public static bool SetProperty(List<IMyTerminalBlock> blocks, string propName, bool propVal) {
			blocks.ForEach(b => {
				b.SetValueBool(propName, propVal);
			});
			return propVal;
		}
		public static float SetProperty(List<IMyTerminalBlock> blocks, string propName, float propVal) {
			blocks.ForEach(b => {
				b.SetValueFloat(propName, propVal);
			});
			return propVal;
		}

		public static bool SetEnabledAbsToggle(List<IMyTerminalBlock> blocks) {
			int[] numOffOn = { 0, 0 };
			blocks.ForEach(b => {
				var blk = b as IMyFunctionalBlock;
				if (null != blk) numOffOn[blk.Enabled ? 1 : 0]++;
			});
			return SetEnabled(blocks, numOffOn[0] >= numOffOn[1]);
		}

		public static bool SetEnabled(List<IMyTerminalBlock> blocks, bool enable) {
			blocks.ForEach(b => {
				var blk = b as IMyFunctionalBlock;
				if (null != blk) blk.Enabled = enable;
			});
			return enable;
		}
	}
}
