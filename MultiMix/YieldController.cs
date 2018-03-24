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
		const long TPS = TimeSpan.TicksPerSecond;
		YieldModule yieldMgr = null;
		class YieldModule : TickBase {
			public YieldModule(Program p) : base(p) {}

			public void Add(IEnumerable<int> method) {
				Execute(method.GetEnumerator());
			}

			override public UpdateFrequency Tick() {
				if (0 < pending.Count) {
					var p = pending.First();
					if (p.Key <= Pgm.totalTicks) {
						pending.RemoveAt(0);
						Execute(p.Value);
						if (0 == pending.Count)
							return UpdateFrequency.Update100;
						p = pending.First();
					}
					if (p.Key <= Pgm.totalTicks + (TPS / 60))
						return UpdateFrequency.Update1;
					if (p.Key <= Pgm.totalTicks + (TPS / 6))
						return UpdateFrequency.Update10;
				}
				return UpdateFrequency.Update100;
			}

			void Execute(IEnumerator<int> iter) {
				if (!iter.MoveNext())
					iter.Dispose();
				else {
					var nextTick = Pgm.totalTicks + TimeSpan.FromMilliseconds(iter.Current).Ticks;
					while (pending.ContainsKey(nextTick))
						nextTick++; // Ensure no duplicate keys are added to SortedList<>.
					pending.Add(nextTick,iter);
				}
			}

			SortedList<long,IEnumerator<int>> pending = new SortedList<long,IEnumerator<int>>();
		}
	}
}
