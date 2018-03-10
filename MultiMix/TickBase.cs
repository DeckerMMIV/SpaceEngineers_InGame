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
		abstract class ModuleBase {
			public ModuleBase(Program p) { Pgm = p; }
			protected Program Pgm;
			protected IMyGridTerminalSystem Gts { get { return Pgm.GridTerminalSystem; } }
			protected IMyProgrammableBlock Me { get { return Pgm.Me; } }
		}

		//-------------
		UpdateFrequency Tick(TickBase obj) {
			if (null != obj && obj.Active)
				return obj.Tick();
			return UpdateFrequency.None;
		}

		abstract class TickBase : ModuleBase {
			public TickBase(Program p) : base(p) {}
			public bool Active { get; set; } = true;
			abstract public UpdateFrequency Tick(); // returns; false = use SlowTrigger, true = use FastTrigger
		}
	}
}
