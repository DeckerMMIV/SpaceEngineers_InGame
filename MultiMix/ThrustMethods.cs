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
		public static float SetThrustAbsPct(List<IMyTerminalBlock> blks, float absPct) {
			return ApplyThrustPct(blks, absPct, 0);
		}

		public static float ChangeThrustDiffPct(List<IMyTerminalBlock> blks, float diffPct) {
			return ApplyThrustPct(blks, -1, diffPct);
		}

		private static float ApplyThrustPct(List<IMyTerminalBlock> blks, float absPct, float diffPct) {
			float sumMaxOverride = 0, sumNewOverride = 0, maxOverride, newOverride, pct;
			Func<float, IMyThrust, float, float> calcThrust;
			if (0 != diffPct) {
				pct = MathHelper.Clamp(diffPct, -1, 1);
				calcThrust = (pct2, t, maxT) => { return Math.Max(0, Math.Min(t.GetValueFloat("Override") + (maxT * pct2), maxT)); };
			} else if (0 <= absPct) {
				pct = MathHelper.Clamp(absPct, 0, 1);
				calcThrust = (pct2, t, maxT) => { return maxT * pct2; };
			} else
				return 0;

			foreach(var b in blks) {
				var t = b as IMyThrust;
				if (null != t) {
					t.SetValueFloat("Override", newOverride = calcThrust(pct, t, maxOverride = t.GetMaximum<float>("Override")));
					sumMaxOverride += maxOverride;
					sumNewOverride += newOverride;
				}
			}
			return 0 >= sumMaxOverride ? 0 : sumNewOverride / sumMaxOverride;
		}

		//-------

		[Flags]
		public enum ThrustFlags {
			All = 0,

			// Block / Thrust orientation
			// MUST BE in same bitset-order as 'Base6Directions.DirectionFlags'
			Forward = 1, PushBackward = 1, Front = 1,
			Backward = 2, PushForward = 2, Back = 2,
			Left = 4, PushRight = 4,
			Right = 8, PushLeft = 8,
			Up = 16, PushDownward = 16, Top = 16, Above = 16,
			Down = 32, PushUpward = 32, Bottom = 32, Below = 32,

			AllDirections = Front | Back | Left | Right | Up | Down,

			// Engine size (this is NOT grid-size)
			Large = 64,
			Small = 128,

			AllSizes = Large | Small,

			// Specific engine types
			Ion = 256,
			Hydrogen = 512,
			Atmospheric = 1024,

			AllEngines = Ion | Hydrogen | Atmospheric,
		}

		public static List<IMyTerminalBlock> GetThrustBlocks(ThrustFlags flags, Program pgm, IMyTerminalBlock myGrid = null, IMyTerminalBlock dirRefBlk = null) {
			return GetThrustBlocks(new List<IMyTerminalBlock>(), flags, pgm, myGrid, dirRefBlk);
		}

		public static List<IMyTerminalBlock> GetThrustBlocks(List<IMyTerminalBlock> lst, ThrustFlags flags, Program pgm, IMyTerminalBlock myGrid = null, IMyTerminalBlock dirRefBlk = null) {
			// Default return value is an empty list
			lst.Clear();

			ThrustFlags engineTypes = flags & ThrustFlags.AllEngines;
			if (ThrustFlags.AllEngines == engineTypes)
				// When 'all engine types', then use value zero
				engineTypes = 0;

			ThrustFlags engineSizes = flags & ThrustFlags.AllSizes;
			if (ThrustFlags.AllSizes == engineSizes)
				// When 'all engine sizes', then use value zero
				engineSizes = 0;

			ThrustFlags thrustDirections = flags & ThrustFlags.AllDirections;
			if (ThrustFlags.AllDirections == thrustDirections)
				// When 'all thrust directions', then use value zero
				thrustDirections = 0;

			if (0 < thrustDirections && null == dirRefBlk)
				// Requested specific thrust-direction(s), but missing a 'directionReferenceBlock' for orientation
				return lst;

			// Collect the thrust-blocks which matches criteria of the thrust-flags requested
			pgm.GridTerminalSystem.GetBlocksOfType<IMyThrust>(lst, thr => {
				if (null != myGrid && !SameGrid(thr, myGrid))
					// Requested that thruster should be on same grid as the ´myGrid´ block, but it was not
					return false;

				if (0 < engineSizes) {
					// If thruster-block has more than 4 cubes, then it is (probably) a large thruster
					bool isLarge = (thr.Max - thr.Min).Size > 4;
					if (isLarge) {
						if (0 == (engineSizes & ThrustFlags.Large))
							// Thruster is (probably) 'large', but it was not requested
							return false;
					} else if (0 == (engineSizes & ThrustFlags.Small)) {
						// Thruster is (probably) 'small', but it was not requested
						return false;
					}
				}

				if (0 < engineTypes) {
					if (SubtypeContains(thr, "Atmo")) {
						if (0 == (engineTypes & ThrustFlags.Atmospheric))
							// Thruster is (probably) 'atmospheric', but it was not requested
							return false;
					} else if (SubtypeContains(thr, "Hydr")) {
						if (0 == (engineTypes & ThrustFlags.Hydrogen))
							// Thruster is (probably) 'hydrogen', but it was not requested
							return false;
					} else if (0 == (engineTypes & ThrustFlags.Ion)) {
						// Thruster is (probably) 'ion', but it was not requested
						return false;
					}
				}

				if (0 == thrustDirections)
					// All/any direction is requested
					return true;

				// Calculate direction of thrust-block according to supplied reference-block.
				int blkDir = (int)dirRefBlk.Orientation.TransformDirectionInverse(thr.Orientation.TransformDirection(Base6Directions.Direction.Forward));
				return 0 != ((int)thrustDirections & (1 << blkDir)); // Is direction accepted?
			});

			return lst;
		}
	}
}
