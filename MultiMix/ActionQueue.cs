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
		ActionQueue actionQueue = null;
		class ActionQueue : TickBase {
			public ActionQueue(Program p) : base(p) {}

			public void Add(Action<Program> act) { queued.Enqueue(act); Pause(); }

			public void Pause() { queued.Enqueue((pgm)=>{}); }

			public override bool Tick() {
				Action<Program> act;
				if (queued.TryDequeue(out act)) {
					act.Invoke(Pgm);
					return true;
				}
				return false;
			}

			Queue<Action<Program>> queued = new Queue<Action<Program>>();
		}
	}
}
