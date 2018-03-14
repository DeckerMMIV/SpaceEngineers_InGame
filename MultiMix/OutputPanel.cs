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
		class OutputPanel {
			public int Count { get { return lcds.Count; } }

			public void Add(IMyTextPanel p) { lcds.Add(p); }
			public void Clear() { lcds.Clear(); }

			public void SetAlign(int align) {
				foreach(var p in lcds)
					p.SetValue("alignment",(Int64)align);
			}
			public void SetFont(string fontName, float fontSize=1.0f) {
				foreach(var p in lcds) {
					p.FontSize = fontSize;
					p.Font = fontName;
				}
			}
			public void SetColor(Color foreColor, Color backColor) {
				foreach(var p in lcds) {
					p.FontColor = foreColor;
				}
			}

			public void WritePublicText(StringBuilder sb) {
				var txt = sb.ToString();
				sb.Clear();
				foreach(var p in lcds)
					p.WritePublicText(txt);
			}

			public void ShowPublicText(string txt=null) {
				foreach(var p in lcds) {
					if (null != txt)
						p.WritePublicText(txt);
					if (!p.ShowText)
						p.ShowPublicTextOnScreen();
				}
			}

			List<IMyTextPanel> lcds = new List<IMyTextPanel>();
		}
	}
}
