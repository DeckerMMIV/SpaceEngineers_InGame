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
		public static void ApplyAction(List<IMyTerminalBlock> blks, string actName) {
			foreach(var b in blks)
				b.ApplyAction(actName);
		}

		public static bool SetPropertyAbsToggle(List<IMyTerminalBlock> blks, string propName) {
			int[] cntOffOn = {0,0};
			foreach(var b in blks) {
				var f = b as IMyFunctionalBlock;
				if (null != f)
					cntOffOn[f.GetValueBool(propName) ? 1 : 0]++;
			}
			return SetProperty(blks, propName, cntOffOn[0] >= cntOffOn[1]);
		}

		public static bool SetProperty(List<IMyTerminalBlock> blks, string propName, bool propVal) {
			foreach(var b in blks)
				b.SetValueBool(propName, propVal);
			return propVal;
		}
		public static float SetProperty(List<IMyTerminalBlock> blks, string propName, float propVal) {
			foreach(var b in blks)
				b.SetValueFloat(propName, propVal);
			return propVal;
		}

		public static bool SetEnabledAbsToggle(List<IMyTerminalBlock> blks) {
			int[] cntOffOn = {0,0};
			foreach(var b in blks) {
				var f = b as IMyFunctionalBlock;
				if (null != f)
					cntOffOn[f.Enabled ? 1 : 0]++;
			}
			return SetEnabled(blks, cntOffOn[0] >= cntOffOn[1]);
		}

		public static bool SetEnabled(List<IMyTerminalBlock> blks, bool enable) {
			foreach(var b in blks) {
				var f = b as IMyFunctionalBlock;
				if (null != f)
					f.Enabled = enable;
			}
			return enable;
		}
	}
}
