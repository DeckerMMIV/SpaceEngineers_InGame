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
		ToolsModule toolsMgr = null;
		class ToolsModule : ModuleBase, IMenuCollector {
			public ToolsModule(Program p) : base(p) {}

			const string GR="Grinder",WL="Welder",OD="OreDetector",DR="Drill",LG="LandingGear",BL="BeamLength";

			public void AddMenu(MenuManager menuMgr) {
				var tls = Menu("Tools");
				bool foundTools = false;

				foreach (string tool in new[] { GR, WL, OD, DR })
					if (HasTool(tool)) {
						foundTools = true;
						tls.Add(
							Menu(() => GetText(tool))
								.Collect(this)
								.Enter("toggle"+tool, () => ToolToggle(tool))
								.Back(() => ToolEnable(tool, false))
								.Left(() => ToolDistance(tool, -1))
								.Right(() => ToolDistance(tool, 1))
						);
					}

				if (HasTool(DR)) {
					Action<int> yawRotate = (dir) => {
						ActionOnBlocksOfType<IMyGyro>(Pgm, Me, g=>{
							if (NameContains(g, DR)) {
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
					MenuItem bbox=Menu("Extend/Boundaries");
					foreach(string txt in Pgm.DIRECTIONS) {
						int j=++i;
						bbox.Add(
							Menu(()=>SensorText(txt,j,pad[j]))
								.Left(()=>SensorProp(j,-1))
								.Right(()=>SensorProp(j,1))
								.Back(()=>SensorProp(j,0))
						);
					}

					i=10;
					MenuItem typs=Menu("Object detection");
					foreach(string txt in new[] {"Large ships","Small ships","Stations","Subgrids","Players","Asteroids","Floating obj."}) {
						int j=++i;
						typs.Add(
							Menu(()=>SensorText(txt,j,pad[j]))
								.Enter(()=>SensorProp(j,0))
						);
					}

					i=20;
					MenuItem fctn=Menu("Faction recognition");
					foreach(string txt in new[] {"Owner","Friendly","Neutral","Enemy"}) {
						int j=++i;
						fctn.Add(
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
							bbox,
							typs,
							fctn
						)
					);
				}

				if (0 < GetBlocksOfType(Pgm,LG,Me).Count) {
					foundTools = true;
					Action<int> lgMode=(wanted)=>{
						var lst = GetBlocksOfType(Pgm,LG,Me);
						if (-1 == wanted) {
							int[] cnts = {0,0,0};
							foreach(var b in lst)
								cnts[(int)(b as IMyLandingGear).LockMode]++;
							wanted = 0 < cnts[1] ? 2 : 0; // `ReadyToLock` takes priority over `Unlocked`.
						}
						foreach(var b in lst) {
							var l = b as IMyLandingGear;
							switch ((int)l.LockMode) {
							// BUG: Why is `LandingGearMode` not whitelisted?
							case 0: break;
							case 1: if (2==wanted) l.Lock(); break;
							case 2: if (0==wanted) l.Unlock(); break;
							}
						}
					};
					tls.Add(
						Menu(() => GetText(LG,2))
							.Collect(this)
							.Enter("toggle"+LG, () => lgMode(-1))
							.Left(() => lgMode(2))
							.Right(() => lgMode(0))
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

				if (HasGravityGenerators()) {
					foundTools = true;
					MenuItem tp = Menu("Gravity generators").Add(
						Menu(()=>GravityGeneratorText("Selected",0))
							.Left(()=>GravityGeneratorProp(0,-1))
							.Right(()=>GravityGeneratorProp(0,1))
							.Enter(()=>ToggleGravityGen())
					);

					int i=0;
					foreach(string txt in new[] {"Width","Height","Depth","Strength"}) {
						int j=++i;
						tp.Add(
							Menu(()=>GravityGeneratorText(txt,j))
								.Left(()=>GravityGeneratorProp(j,-1))
								.Right(()=>GravityGeneratorProp(j,1))
								.Back(()=>GravityGeneratorProp(j,0))
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

			public void CollectSetup() {
				namedCounters.Clear();
			}
			public void CollectTeardown() {}
			public void CollectBlock(IMyTerminalBlock blk) {
				var fb = blk as IMyFunctionalBlock;
				if (null==fb || !SameGrid(Me,fb)) {}
				else if (fb is IMyShipGrinder) Inc(GR,fb);
				else if (fb is IMyShipWelder) Inc(WL,fb);
				else if (fb is IMyShipDrill) Inc(DR,fb);
				else if (fb is IMyOreDetector) Inc(OD,fb);
				else if (fb is IMyLandingGear && fb.IsWorking) {
					var cnt = GetCounters(LG);
					if ((fb as IMyLandingGear).IsLocked)
						cnt.pri++;
					else
						cnt.sec++;
				}
			}

			class Counters { public int pri; public int sec; }
			Dictionary<string, Counters> namedCounters = new Dictionary<string, Counters>();

			Counters GetCounters(string name) {
				Counters cnt;
				if (!namedCounters.TryGetValue(name, out cnt))
					namedCounters.Add(name, cnt = new Counters());
				return cnt;
			}
			void Inc(string name, IMyFunctionalBlock blk) {
				var cnt = GetCounters(name);
				if (blk.IsWorking)
					cnt.pri++;
				else
					cnt.sec++;
			}

			readonly string[,] labels = new string[,]{{"ON","OFF"},{"ENABLED","DISABLED"},{"LOCKED","UNLOCKED"}};

			public string GetText(string name, int i=0) {
				Counters cnt;
				if (!namedCounters.TryGetValue(name, out cnt))
					return $"{name}: -- / --";
				i = MathHelper.Clamp(i,0,labels.Length-1);
				if (0 == cnt.sec)
					return $"{name}: {labels[i,0]} {cnt.pri} / --";
				if (0 == cnt.pri)
					return $"{name}: -- / {cnt.sec} {labels[i,1]}";
				return $"{name}: {labels[i,0].ToLower()} {cnt.pri} / {cnt.sec} {labels[i,1].ToLower()}";
			}

			public void ToolDistance(string toolName, int dir) {
				var lst = GetBlocksOfType(Pgm,toolName,Me);
				if (1 > lst.Count)
					return;

				try {
					lst[0].GetProperty(BL);
					var blk = lst[0];
					float beamLength = blk.GetValueFloat(BL);
					beamLength = MathHelper.Clamp(beamLength+dir, 1, 20);
					foreach(var b in lst)
						b.SetValueFloat(BL,beamLength);
				} catch(Exception) {
					// Ignored
				}
			}

			#region Sensor
			IMySensorBlock sensor = null;
			public bool HasSensors() {
				return null != NextBlockInGrid(Pgm,Me,sensor,0);
			}

			public bool ToggleSensor() {
				return null == sensor ? false : (sensor.Enabled = !sensor.Enabled);
			}

			public void SensorProp(int propId, int propVal) {
				if (0==propId) {
					sensor = NextBlockInGrid(Pgm,Me,sensor,propVal);
					return;
				}
				if (null==sensor)
					return;

				Func<float,int,float> ext = (v,d) => { return 0==d ? 1 : MathHelper.Clamp(v+d,1,50); };

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
				string mfx="", sfx= (0==propId) ? "(select sensor)" : "???";
				if (null!=sensor) {
					switch(propId) {
					case 0:
						mfx = OnOff(sensor.Enabled);
						sfx = sensor.CustomName;
						break;
					case 1: sfx=$"{sensor.FrontExtend:F0}"; break;
					case 2: sfx=$"{sensor.BackExtend:F0}"; break;
					case 3: sfx=$"{sensor.LeftExtend:F0}"; break;
					case 4: sfx=$"{sensor.RightExtend:F0}"; break;
					case 5: sfx=$"{sensor.TopExtend:F0}"; break;
					case 6: sfx=$"{sensor.BottomExtend:F0}"; break;
					case 11: sfx=YesNo(sensor.DetectLargeShips); break;
					case 12: sfx=YesNo(sensor.DetectSmallShips); break;
					case 13: sfx=YesNo(sensor.DetectStations); break;
					case 14: sfx=YesNo(sensor.DetectSubgrids); break;
					case 15: sfx=YesNo(sensor.DetectPlayers); break;
					case 16: sfx=YesNo(sensor.DetectAsteroids); break;
					case 17: sfx=YesNo(sensor.DetectFloatingObjects); break;
					case 21: sfx=YesNo(sensor.DetectOwner); break;
					case 22: sfx=YesNo(sensor.DetectFriendly); break;
					case 23: sfx=YesNo(sensor.DetectNeutral); break;
					case 24: sfx=YesNo(sensor.DetectEnemy); break;
					}
				}
				string nme=$"{lbl}{mfx}:".PadRight(p);
				return $"{nme}{sfx}";
			}
			#endregion

			#region Projector
			IMyProjector projector = null;

			public bool HasProjectors() {
				return null != NextBlockInGrid(Pgm,Me,projector,0);
			}

			public bool ToggleProjector() { 
				return null == projector ? false : (projector.Enabled = !projector.Enabled) & projector.IsProjecting;
			}

			public void ProjectorProp(int propId, int propVal) {
				if (0 == propId) {
					projector = NextBlockInGrid(Pgm,Me,projector,propVal);
					return;
				}
				if (null == projector)
					return;

				switch (propId) {
				case 1: case 2: case 3:
					projector.ProjectionOffset = UpdDim(projector.ProjectionOffset, propId, propVal, (v, d) => { return MathHelper.Clamp(v + d, -50, 50); });
					projector.UpdateOffsetAndRotation();
					break;
				case 4: case 5: case 6:
					projector.ProjectionRotation = UpdDim(projector.ProjectionRotation, propId - 3, propVal, (v, d) => { return ((v+4) + d) % 4; });
					projector.UpdateOffsetAndRotation();
					break;
				}
			}

			public string ProjectorText(string pfx, int propId) {
				string mfx="", sfx=(0==propId) ? "(select projector)" : "???";
				if (null != projector) {
					switch(propId) {
					case 0:
						mfx = OnOff(projector.Enabled);
						sfx = projector.CustomName;
						break;
					case 1: case 2: case 3:
						sfx = GetDim(projector.ProjectionOffset,propId).ToString();
						break;
					case 4: case 5: case 6:
						sfx = $"{(GetDim(projector.ProjectionRotation,propId-3) * 90)} degrees";
						break;
					}
				}
				return $"{pfx}{mfx}: {sfx}";
			}
			#endregion

			#region Gravity
			IMyGravityGeneratorBase gravityGen = null;
			int factor = 1;

			public bool HasGravityGenerators() {
				return null != NextBlockInGrid(Pgm,Me,gravityGen,0);
			}

			public bool ToggleGravityGen() { 
				return null == gravityGen ? false : (gravityGen.Enabled = !gravityGen.Enabled);
			}

			public void GravityGeneratorProp(int propId, int propVal) {
				if (0 == propId) {
					gravityGen = NextBlockInGrid(Pgm,Me,gravityGen,propVal);
					return;
				}
				if (null == gravityGen)
					return;

				switch (propId) {
				case 1: case 2: case 3:
					if (0 == propVal) {
						factor *= 10;
						if (100 < factor)
							factor = 1;
						break;
					}
					var gravGenBox = gravityGen as IMyGravityGenerator;
					if (null != gravGenBox) {
						gravGenBox.FieldSize = UpdDim(gravGenBox.FieldSize, propId, propVal * factor);
					} else {
						var gravGenSphere = gravityGen as IMyGravityGeneratorSphere;
						if (null != gravGenSphere) {
							gravGenSphere.Radius += propVal;
						}
					}
					break;
				case 4:
					gravityGen.GravityAcceleration += 0 == propVal ? -gravityGen.GravityAcceleration : propVal;
					break;
				}
			}

			public string GravityGeneratorText(string pfx, int propId) {
				string mfx="", sfx=(0==propId) ? "(select gravity-gen.)" : "???";
				if (null != gravityGen) {
					switch(propId) {
					case 0:
						mfx = OnOff(gravityGen.Enabled);
						sfx = gravityGen.CustomName;
						break;
					case 1: case 2: case 3:
						var gravGenBox = gravityGen as IMyGravityGenerator;
						if (null != gravGenBox) {
							sfx = $"{GetDim(gravGenBox.FieldSize,propId):F0}   (+/-{factor})";
						} else {
							var gravGenSphere = gravityGen as IMyGravityGeneratorSphere;
							if (null != gravGenSphere) {
								sfx = $"{gravGenSphere.Radius:F0}   (+/-{factor})";
							}
						}
						break;
					case 4:
						sfx = $"{gravityGen.GravityAcceleration/9.81:F2}";
						break;
					}
				}
				return $"{pfx}{mfx}: {sfx}";
			}
			#endregion

			string YesNo(bool b) {
				return b ? "YES/--" : "--/no";
			}
			string OnOff(bool b) {
				return b ? " [ON]" : " [off]";
			}

			int GetDim(Vector3I vec, int idx) {
				switch (idx) {
				case 1: return vec.X;
				case 2: return vec.Y;
				case 3: return vec.Z;
				}
				return 0;
			}
			float GetDim(Vector3 vec, int idx) {
				switch (idx) {
				case 1: return vec.X;
				case 2: return vec.Y;
				case 3: return vec.Z;
				}
				return 0;
			}

			Vector3I UpdDim(Vector3I vec,int idx,int dir,Func<int,int,int> adj) {
				switch (idx) {
				case 1: vec.X = 0 != dir ? adj(vec.X, dir) : 0; break;
				case 2: vec.Y = 0 != dir ? adj(vec.Y, dir) : 0; break;
				case 3: vec.Z = 0 != dir ? adj(vec.Z, dir) : 0; break;
				}
				return vec;
			}
			Vector3 UpdDim(Vector3 vec, int idx, float dir) {
				switch (idx) {
				case 1: vec.X += dir; break;
				case 2: vec.Y += dir; break;
				case 3: vec.Z += dir; break;
				}
				return vec;
			}

		}
	}
}
