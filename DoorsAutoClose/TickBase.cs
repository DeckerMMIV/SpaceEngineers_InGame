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
		bool Tick(TickBase obj) {
			if (null != obj && obj.Active)
				return obj.Tick();
			return false;
		}

		abstract class TickBase {
			public TickBase(Program p) { Pgm = p; }
			protected Program Pgm;
			protected IMyGridTerminalSystem Gts { get { return Pgm.GridTerminalSystem; } }
			protected IMyProgrammableBlock Me { get { return Pgm.Me; } }
			public bool Active { get; set; } = true;
			abstract public bool Tick(); // returns; false = use SlowTrigger, true = use FastTrigger
		}
	}
}
