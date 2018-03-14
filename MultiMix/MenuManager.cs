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
		public static string LabelOnOff(bool b, string pfx, string onTxt="ON", string offTxt="off", string sfx="") {
			return b ? $"{pfx} {onTxt}/--{sfx}" : $"{pfx} --/{offTxt}{sfx}";
		}

		interface IMenuCollector {
			void CollectSetup();
			void CollectBlock(IMyTerminalBlock blk);
			void CollectTeardown();
		}

		class MenuCommands {
			protected const string UP="UP",DOWN="DOWN",LEFT="LEFT",RIGHT="RIGHT",ENTER="ENTER",BACK="BACK";
		}

		MenuManager menuMgr = null;
		class MenuManager : MenuCommands {
			public MenuManager(Program p) { pgm = p; }

			Program pgm;
			bool firstTime = true;
			bool dirty = true;
			List<MenuItem> mainMenu = new List<MenuItem>();
			StringBuilder sb = new StringBuilder();

			public void Clear() {
				mainMenu.Clear();
				firstTime = dirty = true;
			}

			public void Add(params MenuItem[] items) {
				mainMenu.AddArray(items);
				dirty = true;
			}

			public string WarningText {get;set;} = "";

			public void DrawMenu(OutputPanel lcds, int maxLines = 15) {
				UpdateMenu(maxLines);
				if (0 < WarningText.Length)
					sb.Append(WarningText);
				lcds.WritePublicText(sb);
			}

			List<MenuItem> linearMenu = new List<MenuItem>();
			private void BuildLinearMenu() {
				Action<int, List<MenuItem>> recurse = null;
				recurse = (i, e) => {
					if (null == e)
						return;
					foreach(var f in e) {
						linearMenu.Add(f);
						recurse((f.Indent = i) + 1, f.GetSubMenu());
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
				sb.Append($"\u2022\u2022\u2022 Menu \u2022 MultiMix v{scriptVersion} \u2022\u2022\u2022 ").Append(DateTime.Now.ToString("HH\\:mm\\:ss\\.fff"));

				if (firstTime) {
					sb.Append($"\n MenuManager have been reinitialized. To\n control it please assign six cockpit toolbar-\n slots to run the programmable block with\n one of each argument:\n\n     {UP}\n     {DOWN}\n     {LEFT}\n     {RIGHT}\n\n     {ENTER}\n     {BACK}\n\n Once toolbar-slots are assigned, then\n use one of them to continue.\n")
					.Append('\u2022', 40);
					return;
				} 

				if (dirty)
					BuildLinearMenu();

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
					if (null != collector && !collectors.Contains(collector))
						collectors.Add(collector);
				}
				if (0 < collectors.Count) {
					foreach(var c in collectors)
						c.CollectSetup();
					pgm.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, b => {
						foreach(var c in collectors)
							c.CollectBlock(b);
						return false;
					});
					foreach(var c in collectors)
						c.CollectTeardown();
					collectors.Clear();
				}

				for (int i = lastStart; i < end; i++) {
					MenuItem mnu = linearMenu[i];
					if (i == menuPos)
						sb.Append("\n ").Append('\u00BB', mnu.Indent).Append(' ');
					else
						sb.Append('\n').Append(' ', 1 + mnu.Indent * 2);
					mnu.VisitLabel(sb);
				}

				sb.Append('\n').Append('\u2022', 40).Append("\n Cmds:");
				linearMenu[menuPos].AvailableCmds(sb);
			}

			public bool DoAction(string arg) {
				arg = arg.ToUpper();

				Func<List<MenuItem>, string, bool> recurse = null;
				recurse = (e, arg2) => {
					if (null != e) {
						foreach (var f in e) {
							if (f.DirectCmd.ToUpper() == arg2) {
								f.DoEnter();
								return true;
							}
							if (recurse(f.GetSubMenu(true), arg2))
								return true;
						}
					}
					return false;
				};

				if (firstTime) {
					firstTime = !(new List<string> { UP,DOWN,LEFT,RIGHT,ENTER,BACK }).Contains(arg);
					if (firstTime)
						return recurse(mainMenu,arg);
				} else if (UP == arg) {
					menuPos = (menuPos - 1 + linearMenu.Count) % linearMenu.Count;
				} else if (DOWN == arg) {
					menuPos = (menuPos + 1) % linearMenu.Count;
				} else {
					MenuItem menu = linearMenu[menuPos];
					bool prevVal = menu.ShowSubMenu;
					switch (arg) {
					case LEFT: menu.DoLeft(); break;
					case RIGHT: menu.DoRight(); break;
					case ENTER: menu.DoEnter(); break;
					case BACK: menu.DoBack(); break;
					default: return recurse(mainMenu,arg);
					}
					dirty = (menu.ShowSubMenu != prevVal);
				}
				return true;
			}

			public List<string> AllDirectCommands() {
				var lst = new List<string>();
				Action<List<MenuItem>> recurse = null;
				recurse = (e) => {
					if (null == e)
						return;
					foreach (var f in e) {
						if (0 < f.DirectCmd.Length)
							lst.Add(f.DirectCmd);
						recurse(f.GetSubMenu(true));
					}
				};
				recurse(mainMenu);
				return lst;
			}
		}

		static MenuItem Menu(Func<string> f) {
			return (new MenuItem()).SetLabel(f);
		}

		static MenuItem Menu(string t) {
			return Menu(() => { return t; });
		}

		class MenuItem : MenuCommands {
			public int Indent {get;set;} = 0;

			Func<string> label;
			public MenuItem SetLabel(Func<string> f) {
				label = f;
				return this;
			}
			public void VisitLabel(StringBuilder sb) {
				sb.Append(label());
				if (null!=items && !showItems)
					sb.Append(" \u2219\u2219");
			}

			string cmd = null;
			public string DirectCmd { get { return cmd ?? ""; } }

			IMenuCollector collect = null;
			public MenuItem Collect(IMenuCollector c) {
				collect = c;
				return this;
			}
			public IMenuCollector GetCollect() {
				if (null != items & !showItems)
					return null;
				return collect;
			}

			Action actEnter = null;
			public MenuItem Enter(Action f) {
				actEnter = f;
				return this;
			}
			public MenuItem Enter(string _cmd, Action f) {
				cmd = _cmd;
				actEnter = f;
				return this;
			}
			public void DoEnter() {
				if (null != items) 
					showItems = !showItems;
				else
					actEnter?.Invoke();
			}

			Action actBack = null;
			public MenuItem Back(Action f) {
				actBack = f;
				return this;
			}
			public void DoBack() {
				if (null != items)
					showItems = false;
				else
					actBack?.Invoke();
			}

			Action actLeft = null;
			public MenuItem Left(Action f) {
				actLeft = f;
				return this;
			}
			public void DoLeft() {
				if (null != items)
					showItems = false;
				else
					actLeft?.Invoke();
			}

			Action actRight = null;
			public MenuItem Right(Action f) {
				actRight = f;
				return this;
			}
			public void DoRight() {
				if (null != items)
					showItems = true;
				else
					actRight?.Invoke();
			}

			bool showItems = false;
			List<MenuItem> items = null;
			public MenuItem Add(params MenuItem[] _items) {
				showItems = false;
				(items = items ?? new List<MenuItem>()).AddArray(_items);
				return this;
			}
			public bool ShowSubMenu { get { return showItems; } }
			public List<MenuItem> GetSubMenu(bool ignoreShowItems=false) {
				return (showItems || ignoreShowItems) ? items : null;
			}

			public void AvailableCmds(StringBuilder sb) {
				sb.Append($"{UP},{DOWN}");
				bool more = null != items;
				if (null != actLeft || (more & showItems))
					sb.Append($",{LEFT}");
				if (null != actRight || (more & !showItems))
					sb.Append($",{RIGHT}");
				if (null != actEnter || more)
					sb.Append($",{ENTER}");
				if (null != actBack || (more & showItems))
					sb.Append($",{BACK}");
			}
		}
	}
}
