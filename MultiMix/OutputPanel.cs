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
		class OutputPanel {
			public void Add(IMyTextPanel p) { lcds.Add(p); }

			public void Clear() { lcds.Clear(); }

			public void WritePublicText(StringBuilder sb) {
				var txt = sb.ToString();
				sb.Clear();
				lcds.ForEach(p => p.WritePublicText(txt));
			}

			public void ShowPublicText(string txt=null) {
				lcds.ForEach(p => {
					if (null != txt) { p.WritePublicText(txt); }
					if (!p.ShowText) { p.ShowPublicTextOnScreen(); }
				});
			}

			List<IMyTextPanel> lcds = new List<IMyTextPanel>();
		}
	}
}
