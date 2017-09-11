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
		ToolsModule toolsMgr = null;
		class ToolsModule : TickBase, IMenuCollector {
			public ToolsModule(Program p) : base(p) {}

			override public bool Tick() { return false; }

			public void AddMenu(MenuManager menuMgr) {
				var tls = Menu("Tools");
				bool foundTools = false;

				foreach (string tool in new[] { "Grinder", "Welder", "OreDetector", "Drill" })
					if (HasTool(tool)) {
						foundTools = true;
						tls.Add(
							Menu(() => GetText(tool))
								.Collect(this)
								.Enter($"toggle{tool}", () => ToolToggle(tool))
								.Back(() => ToolEnable(tool, false))
								.Left(() => ToolDistance(tool, -1))
								.Right(() => ToolDistance(tool, 1))
						);
					}

				if (HasTool("Drill")) {
					Action<int> yawRotate = (dir) => {
						ActionOnBlocksOfType<IMyGyro>(Pgm, Me, g=>{
							if (NameContains(g, "DRILL")) {
								float yawRpm = g.Yaw;
								if (0 == dir)
									if (g.Enabled)
										g.Enabled = false;
									else {
										yawRpm = -yawRpm;
										g.Enabled = true;
									}
								else {
									yawRpm = Math.Sign(dir) * Math.Abs(yawRpm);
									g.Enabled = true;
								}
								g.Yaw = yawRpm;
							}
						});
					};
					tls.Add(
						Menu("Yaw rotation  left< toggle >right")
							.Enter("toggleYaw", () => yawRotate(0))
							.Left(() => yawRotate(-1))
							.Right(() => yawRotate(1))
					);
					Pgm.cargoMgr?.AddMenu(tls);
				}

				if (HasSensors()) {
					foundTools=true;

					const int e=11,d=16,f=10;
					int[] pad = { 0,
						e+2,e+1,e+3,e+2,e+2,e+0,0,0,0,0,
						d+0,d+0,d+2,d+1,d+2,d+1,d+0,0,0,0,
						f+1,f+1,f+2,f+0,
					};

					int i=0;
					MenuItem te=Menu("Extend/Boundaries");
					foreach(string txt in Pgm.DIRECTIONS) {
						int j=++i;
						te.Add(
							Menu(()=>SensorText(txt,j,pad[j]))
								.Left(()=>SensorProp(j,-1))
								.Right(()=>SensorProp(j,1))
								.Back(()=>SensorProp(j,0))
						);
					}

					i=10;
					MenuItem to=Menu("Object detection");
					foreach(string txt in new[] {"Large ships","Small ships","Stations","Subgrids","Players","Asteroids","Floating obj."}) {
						int j=++i;
						to.Add(
							Menu(()=>SensorText(txt,j,pad[j]))
								.Enter(()=>SensorProp(j,0))
						);
					}

					i=20;
					MenuItem tf=Menu("Faction recognition");
					foreach(string txt in new[] {"Owner","Friendly","Neutral","Enemy"}) {
						int j=++i;
						tf.Add(
							Menu(()=>SensorText(txt,j,pad[j]))
								.Enter(()=>SensorProp(j,0))
						);
					}

					tls.Add(
						Menu("Sensors").Add(
							Menu(()=>SensorText("Selected",0,0))
								.Left(()=>SensorProp(0,-1))
								.Right(()=>SensorProp(0,1))
								.Enter(()=>ToggleSensor()),
							te,
							to,
							tf
						)
					);
				}

				if (HasProjectors()) {
					foundTools = true;
					MenuItem tp = Menu("Projectors").Add(
						Menu(()=>ProjectorText("Selected",0))
							.Left(()=>ProjectorProp(0,-1))
							.Right(()=>ProjectorProp(0,1))
							.Enter(()=>ToggleProjector())
					);

					int i=0;
					foreach(string txt in new[] {"Offset X","Offset Y","Offset Z","Rotate X","Rotate Y","Rotate Z"}) {
						int j=++i;
						tp.Add(
							Menu(()=>ProjectorText(txt,j))
								.Left(()=>ProjectorProp(j,-1))
								.Right(()=>ProjectorProp(j,1))
								.Back(()=>ProjectorProp(j,0))
						);
					}

					tls.Add(tp);
				}

				if (foundTools)
					menuMgr.Add(tls);
			}

			public void Refresh() {
				CollectSetup();
				Gts.GetBlocksOfType<IMyTerminalBlock>(null, b => {
					CollectBlock(b);
					return false;
				});
				CollectTeardown();
				projector=null;
				sensor=null;
			}

			public bool ToolToggle(string toolName) {
				return SetEnabledAbsToggle(GetBlocksOfType(Pgm,toolName,Me));
			}

			public bool ToolEnable(string toolName, bool enable) {
				return SetEnabled(GetBlocksOfType(Pgm,toolName,Me), enable);
			}

			public bool HasTool(string toolName) {
				return namedCounters.ContainsKey(toolName);
			}

			public void ToolDistance(string toolName, int dir) {
				var blks = GetBlocksOfType(Pgm,toolName,Me);
				if (0 >= blks.Count)
					return;

				try {
					blks[0].GetProperty("BeamLength");
					var blk = blks[0];
					float beamLength = blk.GetValueFloat("BeamLength");
					beamLength = MathHelper.Clamp(beamLength+dir, 1, 20);
					foreach(var b in blks)
						b.SetValueFloat("BeamLength",beamLength);
				} catch(Exception) {
					// Ignored
				}
			}

			private IMySensorBlock sensor = null;
			public bool HasSensors() {
				return null!=NextBlockInGrid(Pgm,Me,sensor,0);
			}

			public bool ToggleSensor() {
				if (null==sensor)
					return false;
				return sensor.Enabled = !sensor.Enabled;
			}

			public void SensorProp(int propId, int propVal) {
				if (0==propId) {
					sensor = NextBlockInGrid(Pgm,Me,sensor,propVal);
					return;
				}
				if (null==sensor)
					return;

				Func<float,int,float> ext = (v,d) => { return d==0 ? 1 : MathHelper.Clamp(v+d,1,50); };

				switch (propId) {
				case 1: sensor.FrontExtend	=ext(sensor.FrontExtend,propVal);	break;
				case 2: sensor.BackExtend	=ext(sensor.BackExtend,propVal);	break;
				case 3: sensor.LeftExtend	=ext(sensor.LeftExtend,propVal);	break;
				case 4: sensor.RightExtend	=ext(sensor.RightExtend,propVal);	break;
				case 5: sensor.TopExtend	=ext(sensor.TopExtend,propVal);		break;
				case 6: sensor.BottomExtend	=ext(sensor.BottomExtend,propVal);	break;
				case 11: sensor.DetectLargeShips		=!sensor.DetectLargeShips;		break;
				case 12: sensor.DetectSmallShips		=!sensor.DetectSmallShips;		break;
				case 13: sensor.DetectStations			=!sensor.DetectStations;		break; 
				case 14: sensor.DetectSubgrids			=!sensor.DetectSubgrids;		break; 
				case 15: sensor.DetectPlayers			=!sensor.DetectPlayers;			break;
				case 16: sensor.DetectAsteroids			=!sensor.DetectAsteroids;		break; 
				case 17: sensor.DetectFloatingObjects	=!sensor.DetectFloatingObjects;	break;
				case 21: sensor.DetectOwner		=!sensor.DetectOwner;		break;
				case 22: sensor.DetectFriendly	=!sensor.DetectFriendly;	break;
				case 23: sensor.DetectNeutral	=!sensor.DetectNeutral;		break;
				case 24: sensor.DetectEnemy		=!sensor.DetectEnemy;		break;
				}
			}

			public string SensorText(string lbl, int propId, int p=16) {
				string mfx="", sfx= (0==propId) ? "(no sensor selected)" : "???";
				if (null!=sensor) {
					Func<bool,string> yn = (b) => { return b?"YES/--":"--/no"; };

					switch(propId) {
					case 0:
						mfx = sensor.Enabled?" [ON]":" [off]";
						sfx = sensor.CustomName;
						break;
					case 1: sfx=$"{sensor.FrontExtend:F0}"; break;
					case 2: sfx=$"{sensor.BackExtend:F0}"; break;
					case 3: sfx=$"{sensor.LeftExtend:F0}"; break;
					case 4: sfx=$"{sensor.RightExtend:F0}"; break;
					case 5: sfx=$"{sensor.TopExtend:F0}"; break;
					case 6: sfx=$"{sensor.BottomExtend:F0}"; break;
					case 11: sfx=yn(sensor.DetectLargeShips); break;
					case 12: sfx=yn(sensor.DetectSmallShips); break;
					case 13: sfx=yn(sensor.DetectStations); break;
					case 14: sfx=yn(sensor.DetectSubgrids); break;
					case 15: sfx=yn(sensor.DetectPlayers); break;
					case 16: sfx=yn(sensor.DetectAsteroids); break;
					case 17: sfx=yn(sensor.DetectFloatingObjects); break;
					case 21: sfx=yn(sensor.DetectOwner); break;
					case 22: sfx=yn(sensor.DetectFriendly); break;
					case 23: sfx=yn(sensor.DetectNeutral); break;
					case 24: sfx=yn(sensor.DetectEnemy); break;
					}
				}
				string nme=$"{lbl}{mfx}:".PadRight(p);
				return $"{nme}{sfx}";
			}

			private IMyProjector projector = null;
			public bool HasProjectors() {
				return null!=NextBlockInGrid(Pgm,Me,projector,0);
			}

			public bool ToggleProjector() { 
				if (null==projector)
					return false;
				return (projector.Enabled = !projector.Enabled) && projector.IsProjecting;
			}

			public void ProjectorProp(int propId, int propVal) {
				if (0==propId) {
					projector = NextBlockInGrid(Pgm,Me,projector,propVal);
					return;
				}
				if (null==projector)
					return;

				Func<Vector3I,int,int,Func<int,int,int>,Vector3I> updVec = (vec,idx,dir,mod) => {
					switch (idx) {
					case 1: vec.X = dir!=0 ? mod(vec.X, dir) : 0; break;
					case 2: vec.Y = dir!=0 ? mod(vec.Y, dir) : 0; break;
					case 3: vec.Z = dir!=0 ? mod(vec.Z, dir) : 0; break;
					}
					return vec;
				};

				switch (propId) {
				case 1: case 2: case 3:
					projector.ProjectionOffset = updVec(projector.ProjectionOffset, propId, propVal, (v, d) => { return Math.Min(20, Math.Max(-20, (v + d))); });
					projector.UpdateOffsetAndRotation();
					break;
				case 4: case 5: case 6:
					projector.ProjectionRotation = updVec(projector.ProjectionRotation, propId - 3, propVal, (v, d) => { return ((v+4) + d) % 4; });
					projector.UpdateOffsetAndRotation();
					break;
				}
			}

			public string ProjectorText(string pfx, int propId) {
				string mfx="", sfx=(0==propId) ? "(no projectors)" : "???";
				if (null!=projector) {
					Func<Vector3I,int,int> getV = (vec,idx) => {
						switch(idx) {
						case 1: return vec.X;
						case 2: return vec.Y;
						case 3: return vec.Z;
						}
						return 0;
					};

					switch(propId) {
					case 0:
						mfx = projector.Enabled?" [ON]":" [off]";
						sfx = projector.CustomName;
						break;
					case 1: case 2: case 3:
						sfx = getV(projector.ProjectionOffset,propId).ToString();
						break;
					case 4: case 5: case 6:
						sfx = $"{(getV(projector.ProjectionRotation,propId-3) * 90)} degrees";
						break;
					}
				}
				return $"{pfx}{mfx}: {sfx}";
			}

			class Counters { public int enabled; public int disabled; }
			Dictionary<string, Counters> namedCounters = new Dictionary<string, Counters>();
			public void CollectSetup() {
				namedCounters.Clear();
			}
			public void CollectBlock(IMyTerminalBlock blk) {
				var fb = blk as IMyFunctionalBlock;
				if (null==fb || !SameGrid(Me,fb)) {}
				else if (fb is IMyShipGrinder) Inc("Grinder",fb);
				else if (fb is IMyShipWelder) Inc("Welder",fb);
				else if (fb is IMyShipDrill) Inc("Drill",fb);
				else if (fb is IMyOreDetector) Inc("OreDetector",fb);
			}
			private void Inc(string name, IMyFunctionalBlock blk) {
				Counters cnt;
				if (!namedCounters.TryGetValue(name, out cnt))
					namedCounters.Add(name, cnt = new Counters());
				if (blk.IsWorking)
					cnt.enabled++;
				else
					cnt.disabled++;
			}
			public void CollectTeardown() {}

			public string GetText(string name) {
				Counters cnt;
				if (!namedCounters.TryGetValue(name, out cnt))
					return $"{name}: -- / --";
				if (0 == cnt.disabled)
					return $"{name}: ON {cnt.enabled} / --";
				if (0 == cnt.enabled)
					return $"{name}: -- / {cnt.disabled} OFF";
				return $"{name}: on {cnt.enabled} / {cnt.disabled} off";
			}
		}
	}
}
