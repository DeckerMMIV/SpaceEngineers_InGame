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
	partial class Program {
		//-------------
		StringBuilder _log = new StringBuilder();
		public void Log(string txt, bool nl = true) {
			_log.Append(txt);
			if (nl) _log.Append("\n");
		}
		public void LogPctBar(string txt, double pct, int barLen = 70, bool nl = true) {
			AppendPctBar(_log, txt, pct, barLen, nl);
		}

		//-------------
		public static StringBuilder AppendPctBar(StringBuilder sb, string txt, double pct, bool nl) {
			return AppendPctBar(sb, txt, pct, 70, nl);
		}
		public static StringBuilder AppendPctBar(StringBuilder sb, string txt, double pct, int barLen = 70, bool nl = true, bool center = false) {
			const char SPC = '\u02D9'; // '\'';
			const char FIL = '!'; // '|';
			int fil = Math.Min(barLen, (int)(Math.Max(0.0, Math.Abs(pct)) * barLen));
			sb.Append(txt).Append("[");
			if (center) {
				int mty = (barLen - fil) / 2;
				sb.Append(SPC, mty).Append(FIL, fil).Append(SPC, mty + (mty * 2 + fil != barLen ? 1 : 0));
			} else {
				if (Math.Sign(pct) < 0)
					sb.Append(SPC, (barLen - fil)).Append(FIL, fil);
				else
					sb.Append(FIL, fil).Append(SPC, (barLen - fil));
			}
			sb.Append(nl ? "]\n" : "]");
			return sb;
		}
		public static StringBuilder AppendCenterPctBar(StringBuilder sb, double pct, int barLen = 70, bool nl = true) {
			return AppendPctBar(sb, "", pct, barLen, nl, true);
		}

		public static float FixItemAmout(string itm, float amt) {
			if (itm.StartsWith("Ore/")) return amt / 1000f;
			return amt;
		}
		public static string GetItemType(IMyInventoryItem invItem) {
			string itype = invItem.Content.TypeId.ToString();
			itype = itype.Substring(itype.LastIndexOf('_') + 1);
			string stype = invItem.Content.SubtypeId.ToString();
			if (itype.StartsWith("Ore") || itype.StartsWith("Ingot")) return $"{itype}/{stype}";
			return stype;
		}

		public static string GetBlockType(IMyTerminalBlock blk) {
			string itype = blk.BlockDefinition.TypeId.ToString();
			itype = itype.Substring(itype.LastIndexOf('_') + 1);
			return itype;
		}

		public static bool SameGrid(IMyTerminalBlock me, IMyTerminalBlock blk) {
			return me.CubeGrid == blk.CubeGrid;
		}
		public static bool NameContains(IMyTerminalBlock blk, string search) {
			return blk.CustomName.IndexOf(search, StringComparison.OrdinalIgnoreCase) > -1;
		}
		public static bool SubtypeContains(IMyTerminalBlock blk, string search) {
			return blk.BlockDefinition.SubtypeId.Contains(search);
		}
		public static bool ToType<T>(IMyTerminalBlock i, ref T o) where T : class, IMyTerminalBlock {
			return null != (o = i as T);
		}

		public static void SetGyro(IMyGyro g, float power = 1, bool active = false, float p = 0, float y = 0, float r = 0) {
			g.GyroOverride = active;
			g.Pitch = p;
			g.Yaw = y;
			g.Roll = r;
			g.GyroPower = power;
		}

		public static int GetDetailedInfo(IMyTerminalBlock blk, out string[] lines) {
			return (lines = blk.DetailedInfo.Split('\n')).Length;
		}

		public static float GetPower(string detailLine) {
			if (detailLine == null) return 0;
			int idx = detailLine.IndexOf(':');
			if (idx < 0) return 0;
			var words = detailLine.Substring(idx + 1).Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
			return float.Parse(words[0]) * (float)Math.Pow(1000, "WkMGTPEZY".IndexOf(words[1][0]));
		}

		public static string PowerAsString(float value, int factor = 0) {
			if (factor == 4 || (factor == 0 && value > 1000000000.0)) return $"{(value / 1000000000.0):F2} GW";
			if (factor == 3 || (factor == 0 && value > 1000000.0)) return $"{(value / 1000000.0):F2} MW";
			if (factor == 2 || (factor == 0 && value > 1000.0)) return $"{(value / 1000.0):F2} kW";
			return $"{value:N0} W";
		}

		public static string VecToString(Vector3D vec) {
			return (vec == null) ? "" : string.Format("{0:F3}:{1:F3}:{2:F3}", vec.GetDim(0), vec.GetDim(1), vec.GetDim(2));
		}
	}
}
