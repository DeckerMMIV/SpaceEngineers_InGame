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
		public static string LabelOnOff(bool on, string pfx, string onTxt="ON", string offTxt="off", string sfx="") {
			return on ? $"{pfx} {onTxt}/--{sfx}" : $"{pfx} --/{offTxt}{sfx}";
		}

		interface IMenuCollector {
			void CollectSetup();
			void CollectBlock(IMyTerminalBlock blk);
			void CollectTeardown();
		}

		class MenuCommands {
			public const string cUp="UP";
			public const string cDown="DOWN";
			public const string cLeft="LEFT";
			public const string cRight="RIGHT";
			public const string cEnter="ENTER";
			public const string cBack="BACK";
		}

		MenuManager menuMgr = null;
		class MenuManager : MenuCommands {
			public MenuManager(Program p) { pgm = p; }

			Program pgm;
			bool firstTime = true;
			bool dirty = true;
			List<MenuItem> mainMenu = new List<MenuItem>();
			StringBuilder sb = new StringBuilder();

			public void Clear() { mainMenu.Clear(); firstTime = dirty = true; }

			public void Add(params MenuItem[] items) { mainMenu.AddArray(items); dirty = true; }

			public string WarningText {get;set;}="";

			public void DrawMenu(OutputPanel lcds, int maxLines = 15) {
				UpdateMenu(maxLines);
				if (WarningText.Length>0) {
					sb.Append(WarningText);
				}
				lcds.WritePublicText(sb);
			}

			List<MenuItem> linearMenu = new List<MenuItem>();
			private void BuildLinearMenu() {
				Action<int, List<MenuItem>> recurse = null;
				recurse = (i, e) => {
					if (null != e) {
						foreach(var c in e) {
							c.Indent = i;
							linearMenu.Add(c);
							recurse(i + 1, c.GetSubMenu());
						}
					}
				};

				linearMenu.Clear();
				recurse(1, mainMenu);
				menuPos = MathHelper.Clamp(menuPos, 0, linearMenu.Count - 1);
				dirty = false;
			}

			int menuPos = 0;
			int lastStart = 0;
			List<IMenuCollector> collectors = new List<IMenuCollector>();
			private void UpdateMenu(int maxLines) {
				sb.Append('\u2022', 3).Append($" Menu \u2022 MultiMix v{scriptVersion} ").Append('\u2022', 3)
				.Append(DateTime.Now.ToString(" HH\\:mm\\:ss\\.fff"))
				.Append("\n");

				if (firstTime) {
					sb.Append($" MenuManager have been reinitialized. To\n control it please assign six cockpit toolbar-\n slots to run the programmable block with\n one of each argument:\n\n     {cUp}\n     {cDown}\n     {cLeft}\n     {cRight}\n\n     {cEnter}\n     {cBack}\n\n Once toolbar-slots are assigned, then\n use one of them to continue.\n");
					sb.Append('\u2022', 40);
					return;
				} 

				if (dirty) { BuildLinearMenu(); }

				int end = Math.Min(linearMenu.Count, lastStart + maxLines);
				if (menuPos <= lastStart) {
					lastStart = Math.Max(0, menuPos - 1);
					end = Math.Min(linearMenu.Count, lastStart + maxLines);
				} else if (end <= menuPos + 1) {
					lastStart = Math.Max(0, Math.Min(linearMenu.Count, menuPos + 1) - ((linearMenu.Count == menuPos + 1 ? 0 : -1) + maxLines));
					end = Math.Min(linearMenu.Count, lastStart + maxLines);
				}

				for (int j = lastStart; j < end; j++) {
					var collector = linearMenu[j].GetCollect();
					if (null != collector && !collectors.Contains(collector)) { collectors.Add(collector); }
				}
				if (collectors.Count > 0) {
					foreach(var c in collectors) { c.CollectSetup(); }
					pgm.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, b => {
						foreach(var c in collectors) { c.CollectBlock(b); }
						return false;
					});
					foreach(var c in collectors) { c.CollectTeardown(); }
					collectors.Clear();
				}

				MenuItem mnu;
				for (int i = lastStart; i < end; i++) {
					mnu = linearMenu[i];
					if (i == menuPos) { sb.Append(' ').Append('\u00BB', mnu.Indent).Append(' '); }
					else { sb.Append(' ', 1 + mnu.Indent * 2); }
					mnu.VisitLabel(sb); sb.Append("\n");
				}

				sb.Append('\u2022', 40).Append("\n Cmds:");
				linearMenu[menuPos].AvailableCmds(sb);
			}

			public bool DoAction(string arg) {
				arg = arg.ToUpper();

				Func<List<MenuItem>, bool> recurse = null;
				recurse = (e) => {
					if (null != e) {
						foreach (var f in e) {
							if (f.DirectCmd.ToUpper()==arg) { f.DoEnter(); return true; }
							if (recurse(f.GetSubMenu(true))) { return true; }
						}
					}
					return false;
				};

				if (firstTime) {
					firstTime = !(new List<string> { cUp, cDown, cLeft, cRight, cEnter, cBack }).Contains(arg);
					if (firstTime) { return recurse(mainMenu); }
				} else if (arg == cUp) { menuPos = (menuPos - 1 + linearMenu.Count) % linearMenu.Count; }
				else if (arg == cDown) { menuPos = (menuPos + 1) % linearMenu.Count; }
				else {
					MenuItem menu = linearMenu[menuPos];
					bool prevVal = menu.ShowSubMenu;
					switch (arg) {
					case cLeft: menu.DoLeft(); break;
					case cRight: menu.DoRight(); break;
					case cEnter: menu.DoEnter(); break;
					case cBack: menu.DoBack(); break;
					default:return recurse(mainMenu);
					}
					dirty = (prevVal != menu.ShowSubMenu);
				}
				return true;
			}

			public List<string> AllDirectCommands() {
				var lst = new List<string>();
				Action<List<MenuItem>> recurse = null;
				recurse = (e) => {
					if (null != e) {
						foreach (var f in e) {
							if (f.DirectCmd.Length>0) { lst.Add(f.DirectCmd); }
							recurse(f.GetSubMenu(true));
						}
					}
				};
				recurse(mainMenu);
				return lst;
			}
		}

		static MenuItem Menu(Func<string> f) { return (new MenuItem()).SetLabel(f); }
		static MenuItem Menu(string t) { return Menu(() => { return t; }); }
		class MenuItem : MenuCommands {
			public int Indent {get;set;} = 0;

			Func<string> label;
			public MenuItem SetLabel(Func<string> f) { label = f; return this; }
			public void VisitLabel(StringBuilder sb) {
				sb.Append(label());
				if (null!=items && !showItems) { sb.Append(" \u2219\u2219"); }
			}

			string cmd = null;
			public string DirectCmd { get { return cmd ?? ""; } }

			IMenuCollector collect = null;
			public MenuItem Collect(IMenuCollector c) { collect = c; return this; }
			public IMenuCollector GetCollect() { if (null != items & !showItems) return null; else return collect; }

			Action actEnter = null;
			public MenuItem Enter(Action f) { actEnter = f; return this; }
			public MenuItem Enter(string _cmd, Action f) { cmd = _cmd; actEnter = f; return this; }
			public void DoEnter() { if (null != items) { showItems = !showItems; } else { actEnter?.Invoke(); } }

			Action actBack = null;
			public MenuItem Back(Action f) { actBack = f; return this; }
			public void DoBack() { if (null != items) { showItems = false; } else { actBack?.Invoke(); } }

			Action actLeft = null;
			public MenuItem Left(Action f) { actLeft = f; return this; }
			public void DoLeft() { if (null != items) { showItems = false; } else { actLeft?.Invoke(); } }

			Action actRight = null;
			public MenuItem Right(Action f) { actRight = f; return this; }
			public void DoRight() { if (null != items) { showItems = true; } else { actRight?.Invoke(); } }

			bool showItems = false;
			List<MenuItem> items = null;
			public MenuItem Add(params MenuItem[] _items) {
				showItems = false;
				(items = items ?? new List<MenuItem>()).AddArray(_items);
				return this;
			}
			public bool ShowSubMenu { get { return showItems; } }
			public List<MenuItem> GetSubMenu(bool ignoreShowItems=false) { return (showItems || ignoreShowItems) ? items : null; }

			public void AvailableCmds(StringBuilder sb) {
				sb.Append($"{cUp},{cDown}");
				bool more = null != items;
				if (null != actLeft || (more & showItems)) sb.Append($",{cLeft}");
				if (null != actRight || (more & !showItems)) sb.Append($",{cRight}");
				if (null != actEnter || more) sb.Append($",{cEnter}");
				if (null != actBack || (more & showItems)) sb.Append($",{cBack}");
			}
		}
	}
}
