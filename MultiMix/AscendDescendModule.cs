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
		AscendDecendModule ascDecMgr = null;
		class AscendDecendModule : TickBase {
			public AscendDecendModule(Program p) : base(p) {}

			public void AddMenu(MenuManager menuMgr) {
				menuMgr.Add(
					Menu("Ascend/Descend controller").Add(
						Menu(() => LabelOnOff(OperationMode == Mode.Descend, $"Mode {(IsRunning ? "[ACTIVE]" : "[Idle]")}: ", "Descend-Fall", "Ascend-Lift"))
							.Enter(() => ToggleRunState())
							.Left(() => OperationMode = Mode.Descend)
							.Right(() => OperationMode = Mode.Ascend),
						Menu(() => $"{OperationMode} max speed: {MaxSpeed} m/s")
							.Left(() => MaxSpeed -= 10)
							.Right(() => MaxSpeed += 10)
							.Back(() => MaxSpeed = (Mode.Ascend == OperationMode ? 100 : 110)),
						Menu(() => OperationMode == Mode.Ascend ? "(Not for ascend mode) BrakeDistFact." : $"BrakeDistFact.: {BrakeDistanceFactor:F2}")
							.Left(() => BrakeDistanceFactor -= 0.01)
							.Right(() => BrakeDistanceFactor += 0.01)
							.Back(() => BrakeDistanceFactor = 1.1),
						Menu(() => OperationMode == Mode.Ascend ? "(Not for ascend mode) Use parachutes" : LabelOnOff(UseParachute, "Use parachutes:", $"YES at height {ParachuteHeight}", "NO"))
							.Enter(() => UseParachute = !UseParachute)
							.Back(() => { UseParachute = false; ParachuteHeight = 1000; })
							.Left(() => ParachuteHeight -= 100)
							.Right(() => ParachuteHeight += 100)
					)
				);
			}

			int execCount;
			long nextTick;
			IEnumerator<int> stateMachine = null;
			override public bool Tick() {
				if (null != stateMachine) {
					if (nextTick < Pgm.totalTicks) {
						if (stateMachine.MoveNext()) {
							int i = stateMachine.Current;
							nextTick = Pgm.totalTicks + TimeSpan.FromMilliseconds(i).Ticks;
							StatusDisplay();
							return i<1000;
						}
						SetRunState(false);
						StatusDisplay();
					}
					return true;
				}
				if (nextTick < Pgm.totalTicks) {
					nextTick = Pgm.totalTicks + TimeSpan.TicksPerSecond/2;
					UpdateTelemetry();
					ThrustStatus();
					StatusDisplay();
					execCount++;
				}
				return false;
			}

			IMyShipController sc = null;
			OutputPanel lcd = null;
			IMyCameraBlock downCamera = null;
			IMyParachute parachute = null;
			List<IMyGasTank> hydrogenTanks = new List<IMyGasTank>();
			List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
			AlignModule align = null;

			public void Refresh(OutputPanel _lcd, AlignModule _align, IMyShipController _sc = null) {
				lcd = _lcd;
				align = _align;
				sc = _sc ?? Pgm.GetShipController(MultiMix_UsedBlocks);

				RefreshThrustsList(_align?.RocketMode ?? false);

				hydrogenTanks.Clear();

				if (null != downCamera)
					downCamera.EnableRaycast = false;

				downCamera = null;
				parachute = null;

				IMyGasTank h = null;
				IMyBatteryBlock y = null;
				IMyCameraBlock c = null;
				IMyParachute p = null;
				GatherBlocks(Pgm, new List<Func<IMyTerminalBlock, bool>> {
					b => SameGrid(b,Me) && !NameContains(b,MultiMix_IgnoreBlocks),
					b => { if (ToType(b, ref h) && h.IsWorking && SubtypeContains(h,"Hydrogen")) { hydrogenTanks.Add(h); return false; } return true; },
					b => { if (ToType(b, ref y) && y.IsWorking) { batteries.Add(y); return false; } return true; },
					b => { if (ToType(b, ref c) && c.IsWorking && NameContains(c,"Down")) { downCamera = c; c.EnableRaycast = true; return false; } return true; },
					b => { if (ToType(b, ref p) && p.IsWorking) { parachute=p; return false; } return true; },
				});

				if (null == stateMachine)
					SetRunState(false);
			}

			const int PRIO_ATMOS = 1, PRIO_ION = 2, PRIO_HYDRO = 3;
			readonly Dictionary<int, string> thrustPrioNames = new Dictionary<int, string> { { PRIO_ATMOS, "Atm" }, { PRIO_ION, "Ion" }, { PRIO_HYDRO, "Hyd" } };
			readonly Dictionary<int, float> thrustEffLimit = new Dictionary<int, float> { { PRIO_ATMOS, 0.05f }, { PRIO_ION, 0.05f }, { PRIO_HYDRO, 0 } };
			
			SortedDictionary<int, Dictionary<string, List<IMyThrust>>> thrusts = new SortedDictionary<int, Dictionary<string, List<IMyThrust>>>();

			public void RefreshThrustsList(bool likeRocket) {
				thrusts.Clear();
				int prio;
				Dictionary<string, List<IMyThrust>> thrType;
				List<IMyThrust> thrList;

				var tmpLst = GetThrustBlocks(ThrustFlags.AllEngines | (likeRocket ? ThrustFlags.PushForward : ThrustFlags.PushUpward), Pgm, Me, sc);
				foreach(var b in tmpLst) {
					if (!b.IsFunctional || NameContains(b, MultiMix_IgnoreBlocks))
						continue;

					if (SubtypeContains(b, "Atmos"))
						prio = PRIO_ATMOS;
					else if (SubtypeContains(b, "Hydro"))
						prio = PRIO_HYDRO;
					else
						prio = PRIO_ION;

					if (!thrusts.TryGetValue(prio, out thrType))
						thrusts.Add(prio, thrType = new Dictionary<string, List<IMyThrust>>());

					if (!thrType.TryGetValue(b.BlockDefinition.SubtypeId, out thrList))
						thrType.Add(b.BlockDefinition.SubtypeId, thrList = new List<IMyThrust>());

					thrList.Add(b as IMyThrust);
				}
			}

			public int MaxSpeed {
				get { return maxSpeeds[(int)mode]; }
				set { maxSpeeds[(int)mode] = Math.Max(10, value); }
			}
			int[] maxSpeeds = { 100,110 };

			double prevSpeed = 0;
			double currSpeed = 0;
			double currAccl = 0;
			double gravityStrength = 0;
			double gravity = 0;
			double altitudeSurface = double.NaN;
			double prevAltitude = 0;
			double altitudeDiff = 0;
			string raycastName = "";
			double shipWeight = 0;
			long shipMassUpdateTick = 0;
			MyShipMass shipMass = new MyShipMass(0,0,0);
			float atmosphereDensity = 0;
			void UpdateTelemetry() {
				prevSpeed = currSpeed;
				currAccl = (currSpeed = sc.GetShipSpeed()) - prevSpeed;

				gravity = (gravityStrength = sc.GetNaturalGravity().Length()) / 9.81;

				if (shipMassUpdateTick < Pgm.totalTicks) {
					shipMassUpdateTick = Pgm.totalTicks + TimeSpan.TicksPerSecond;
					shipMass = sc.CalculateShipMass();
				}
				shipWeight = gravityStrength * shipMass.PhysicalMass; // or -> shipMass.TotalMass

				prevAltitude = altitudeSurface;
				if (sc.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitudeSurface)) {
					altitudeDiff = altitudeSurface - prevAltitude;
				} else {
					altitudeSurface = altitudeDiff = double.NaN;
				}

				atmosphereDensity = parachute?.Atmosphere ?? float.NaN;

				if (null != downCamera) {
					if (downCamera.CanScan(1000)) {
						MyDetectedEntityInfo dei = downCamera.Raycast(1000);
						double len = 0;
						if (null != dei.HitPosition) {
							Vector3D hp = (Vector3D)dei.HitPosition;
							len = (hp - downCamera.CubeGrid.GetPosition()).Length();
						}
						raycastName = dei.Name + "/" + len; //downCamera.CubeGrid.GetPosition(); //dei.HitPosition.ToString();
					} else {
						raycastName = downCamera.AvailableScanRange.ToString();
					}
				}
			}

			string NumStr(double value, string unit) {
				double absVal = Math.Abs(value);
				if (1000 > absVal)
					return $"{value:F0} {unit}";
				if (1000000 > absVal)
					return $"{value/1000:F1} k{unit}";
				if (1000000000 > absVal)
					return $"{value/1000000:F2} M{unit}";
				return $"{value/1000000000:F2} G{unit}";
			}

			StringBuilder sb1 = new StringBuilder();
			StringBuilder sb2 = new StringBuilder();
			string curState = "";
			readonly string[] chargeAnim={"      ","  <<"," << ","<<  "};

			long hydroTanksUpdateTick = 0;
			float hydroTanksPctFilled = 0;
			int hydroTanksGiving = 0;

			long batteriesUpdateTick = 0;
			float battStoredPower = 0, battMaxPower = 0;
			int battNumCharging = 0;
			bool battAllCharging = false;

			long displayUpdateTick = 0;
			void StatusDisplay() {
				if (null == lcd || displayUpdateTick > Pgm.totalTicks)
					return;
				displayUpdateTick = Pgm.totalTicks + TimeSpan.TicksPerSecond/10;

				sb1.Append('\u2022',7).Append($" Status Display ").Append('\u2022',7).Append(DateTime.Now.ToString(" HH\\:mm\\:ss\\.fff"));
if (!sc.ControlThrusters)
	sb1.Append("\n")
		.Append(sc.MoveIndicator.ToString()).Append(Pgm.MoveIndicator2Command(sc.MoveIndicator))
		.Append(sc.RollIndicator.ToString());
				sb1.Append($"\n Mode: {OperationMode} \u2022 State: {curState}");
				sb1.Append("\n Alignment: ").Append((align.Active ? "Enabled" : "Off")).Append(" \u2022 AlignMode: ").Append(align.RocketMode ? "Rocket" : "Ship").Append(align.Inverted ? " (Inverted)" : "");
				string alti = (double.IsNaN(altitudeSurface) ? "---" : $"{altitudeSurface:F1}");
				string altiDiff = ((double.IsNaN(altitudeDiff) || Math.Abs(altitudeDiff) < 1) ? "" : ((altitudeDiff > 0) ? " " : " "));
				sb1.Append($"\n Altitude: {alti} m{altiDiff} \u2022 Gravity: {gravity:F3} g");
				sb1.Append($"\n Spd: {currSpeed:F2} m/s \u2022 Acc.: {currAccl:F2} \u2022 Atm.: {atmosphereDensity:F1}");
				if (Mode.Descend == mode) {
					sb1.Append($"\n BrakeDistance: {brakeDistance:F2} \u2022 AlignDiff: {align.AlignDifference:F3}");
					sb1.Append($"\n Raycast: {raycastName}");
				}
				sb1.Append($"\n Mass: {shipMass.PhysicalMass:#,##0} \u2022 Cargo: {shipMass.PhysicalMass - shipMass.BaseMass:#,##0}");
				sb1.Append("\n Controller: ").Append(sc?.CustomName ?? "<NONE>");
				sb1.Append($"\n Requested: ").Append(NumStr(totalThrustWanted, "N")).Append(" \u2022 Current: ").Append(NumStr(sumThrustCurEff, "N"));

				sb1.Append(sb2);

				if (0 < hydrogenTanks.Count) {
					if (hydroTanksUpdateTick < Pgm.totalTicks) {
						hydroTanksUpdateTick = Pgm.totalTicks + TimeSpan.TicksPerSecond/3;
						hydroTanksPctFilled = 0;
						hydroTanksGiving = 0;
						foreach(var h in hydrogenTanks) {
							if (h.IsWorking && !h.Stockpile) {
								hydroTanksPctFilled += h.FilledRatio;
								hydroTanksGiving++;
							} 
						}
						hydroTanksPctFilled /= hydrogenTanks.Count;
					}

					AppendPctBar(sb1, $"\n Hydr:", -hydroTanksPctFilled, 30, false).Append($" {hydroTanksPctFilled * 100:F1}% in {hydroTanksGiving} tanks");
				}

				if (0 < batteries.Count) {
					if (batteriesUpdateTick < Pgm.totalTicks) {
						batteriesUpdateTick = Pgm.totalTicks + TimeSpan.TicksPerSecond;
						battStoredPower = 0;
						battMaxPower = 0;
						battNumCharging = 0;
						foreach(var b in batteries) {
							if (b.IsWorking) {
								battStoredPower += b.CurrentStoredPower;
								battMaxPower += b.MaxStoredPower;
								battNumCharging += b.IsCharging ? 1 : 0;
							} 
						}
						battAllCharging = battNumCharging == batteries.Count;
					}

					AppendPctBar(sb1, "\n Batt:", (battStoredPower / battMaxPower) * (battAllCharging ? 1 : -1), 30, false);
					if (battAllCharging)
						sb1.Append($" {chargeAnim[execCount & 3]} Recharging");
				}

				lcd.WritePublicText(sb1);
			}

			public enum Mode { Ascend, Descend };
			public Mode OperationMode {
				get { return mode; }
				set { if (!IsRunning) { mode = value; } }
			}
			Mode mode = Mode.Ascend;

			public bool IsRunning { get { return null != stateMachine; } }

			public bool ToggleRunState() {
				SetRunState(null == stateMachine);
				return null != stateMachine;
			}
			public void SetRunState(bool enable) {
				if (enable && null == stateMachine) {
					abortStateMachine = false;
					switch (OperationMode) {
					case Mode.Ascend: stateMachine = AscendStateMachine().GetEnumerator(); break;
					case Mode.Descend: stateMachine = DescendStateMachine().GetEnumerator(); break;
					}
					nextTick = 0;
				} else if (!enable) {
					if (null != stateMachine) {
						abortStateMachine = true;
						stateMachine.MoveNext();
						stateMachine.Dispose(); // https://forums.keenswh.com/threads/tutorial-easy-and-powerful-state-machine-using-yield-return.7385411/#post-1287058811
					}
					stateMachine = null;
					ThrustZero();
					align.Active = false;
					curState = "Offline";
				}
			}

			bool abortStateMachine;
			IEnumerable<int> AscendStateMachine() {
				align.Inverted = false;
				align.IgnoreGravity = false;
				align.Active = false;

				// Loop and increase thrust until (upwards) movement detected
				curState = "Pending Liftoff";
				thrustPct = 0.5f;
				while (State_Liftoff()) {
					yield return 200;
					if (abortStateMachine) yield break;
				}

				curState = "GravityAlignEnable";
				align.Active = true;
				sc.DampenersOverride = false;

				// Loop until zero-gravity detected
				curState = "Maintain Lift";
				do {
					yield return (10 > MaxSpeed - currSpeed) ? 1000 : 200;
					if (abortStateMachine) yield break;
				} while (State_MaintainLift());

				// Rotate for retro-thrust
				curState = "VelocityAlignEnable";
				align.Active = true; // Force update
				ThrustZero();
				ThrustStatus();
				yield return 100;
				if (abortStateMachine) yield break;

				// Brake and wait for minimal movement
				curState = "Braking";
				sc.DampenersOverride = true;
				while (State_HasMovement()) {
					yield return 500;
					if (abortStateMachine) yield break;
				}

				curState = "Completed";
				align.Active = false;
				ThrustStatus();
			}

			bool State_Liftoff() {
				UpdateTelemetry();
				AdjustThrustPower(0.01f);
				return 1 > currSpeed;
			}

			bool State_MaintainLift() {
				UpdateTelemetry();
				if (0 >= gravityStrength)
					return false;
				AdjustThrustPower(0.01f);
				return true;
			}

			bool State_HasMovement() {
				UpdateTelemetry();
				ThrustStatus();
				return 0.1 < currSpeed;
			}

			int parachuteHeight = 1000;
			public int ParachuteHeight {
				get { return parachuteHeight; }
				set {
					parachuteHeight = Math.Max(100,value);
					SetProperty(GetBlocksOfType(Pgm,"Parachute",Me),"AutoDeployHeight",parachuteHeight);
				}
			}

			bool useParachute = true;
			public bool UseParachute {
				get { return useParachute; }
				set {
					useParachute = value;
					SetProperty(GetBlocksOfType(Pgm,"Parachute",Me),"AutoDeploy",useParachute);
				}
			}

			double brakeDistanceFactor = 1.1;
			public double BrakeDistanceFactor {
				get { return brakeDistanceFactor; }
				set { brakeDistanceFactor = Math.Max(1.01, value); }
			}

			IEnumerable<int> DescendStateMachine() {
				sb2.Clear();

				align.Inverted = false;
				align.IgnoreGravity = false;
				align.Active = false;

				curState = "Initialize";
				thrustPct = 1.0f;
				sc.DampenersOverride = false;
				yield return 100;
				if (abortStateMachine) yield break;

				curState = "GravityAlignDisable";
				align.Active = false;
				yield return 100;
				if (abortStateMachine) yield break;

				curState = "FreeFalling";
				while (State_AboveBrakeDistance(10)) {
					if (currSpeed > MaxSpeed)
						break;
					yield return 1000;
					if (abortStateMachine) yield break;
				}

				curState = "GravityAlignEnable";
				align.Active = true;
				yield return 100;
				if (abortStateMachine) yield break;

				curState = "MaintainMaxSpeed";
				while (State_AboveBrakeDistance(BrakeDistanceFactor)) {
					if (align.IsAligned)
						ReduceToMaxSpeed();
					yield return 300;
					if (abortStateMachine) yield break;
				}

				curState = "Braking";
				ThrustZero();
				sc.DampenersOverride = true;
				while (State_StillMoving()) {
					yield return 300;
					if (abortStateMachine) yield break;
				}

				curState = "Completed";
				align.Active = false;
				ThrustStatus();
			}

			double brakeDistance;
			void UpdateBrakeDistance() {
				// Requirement: UpdateTelemetry() 
				double maxThrust = GetTotalEffectiveThrust();

				double brakeForce = maxThrust - shipWeight;
				double deceleration = brakeForce / shipMass.PhysicalMass; // or -> shipMass.TotalMass

				brakeDistance = (currSpeed / 2) * (currSpeed / deceleration);
			}

			bool State_AboveBrakeDistance(double altitudeFactor) {
				UpdateTelemetry();
				UpdateBrakeDistance();
				if (!double.IsNaN(altitudeSurface))
					if (brakeDistance * altitudeFactor > altitudeSurface)
						return false;
				return true;
			}

			bool State_StillMoving() {
				UpdateTelemetry();
				UpdateBrakeDistance();
				ThrustStatus();
				return 0.1 < currSpeed;
			}

			float GetRequiredMinimumEffectiveThrust(int thrustType) {
				float minEff = 0;
				thrustEffLimit.TryGetValue(thrustType, out minEff);
				return minEff;
			}

			double GetTotalEffectiveThrust() {
				double totalEffThrust = 0;
				for (var thrPrio = thrusts.GetEnumerator(); thrPrio.MoveNext();) {
					float minEff = GetRequiredMinimumEffectiveThrust(thrPrio.Current.Key);
					for (var thrType = thrPrio.Current.Value.GetEnumerator(); thrType.MoveNext();) {
						var thrsts = thrType.Current.Value;
						if (1 > thrsts.Count)
							continue;

						var thrst = thrsts[0];
						float eff = thrst.MaxEffectiveThrust / thrst.MaxThrust;
						if (eff >= minEff) {
							int cnt = 0;
							foreach(var t in thrsts)
								if (t.IsWorking)
									cnt++;

							totalEffThrust += thrst.MaxEffectiveThrust * cnt;
						}
					}
				}
				return totalEffThrust;
			}

			float thrustPct;
			float remainThrustNeeded, totalThrustWanted;
			float sumThrustCurEff;
			float localThrustCur, localThrustMax;
			const int barLen = 20;

			void AdjustThrustPower(float offset) {
				thrustPct = MathHelper.Clamp(thrustPct + offset, 0, 1f);
				// Requirement: UpdateTelemetry() 
				double neededAcceleration = (MaxSpeed - currSpeed) * thrustPct;
				remainThrustNeeded = totalThrustWanted = Math.Max(0, (float)((shipMass.PhysicalMass * neededAcceleration) + shipWeight));
				ThrustPowerAndStatus();
			}

			void ReduceToMaxSpeed() {
				// Requirement: UpdateTelemetry() 
				double neededAcceleration = Math.Max(0, (currSpeed - MaxSpeed));
				if (0 < neededAcceleration) {
					remainThrustNeeded = totalThrustWanted = (float)((shipMass.PhysicalMass * neededAcceleration) + shipWeight);
					ThrustPowerAndStatus();
				} else {
					ThrustZero();
					ThrustStatus();
				}
			}

			void ThrustPowerAndStatus() {
				sb2.Clear();

				sumThrustCurEff = 0;
				string typeName;
				for (var thrPrio = thrusts.GetEnumerator(); thrPrio.MoveNext();) {
					if (!thrustPrioNames.TryGetValue(thrPrio.Current.Key, out typeName))
						typeName = "???";

					float minEff = GetRequiredMinimumEffectiveThrust(thrPrio.Current.Key);

					for (var thrType = thrPrio.Current.Value.GetEnumerator(); thrType.MoveNext();) {
						var thrsts = thrType.Current.Value;
						if (1 > thrsts.Count)
							continue;

						var thrst = thrsts[0];
						float pct = 0;
						float eff = thrst.MaxEffectiveThrust / thrst.MaxThrust;
						if (eff >= minEff) {
							int cnt = 0;
							foreach(var t in thrsts)
								if (t.IsWorking)
									cnt++;

							localThrustMax = thrst.MaxEffectiveThrust * cnt;
							pct = (0 < localThrustMax ? Math.Min(1f, remainThrustNeeded / localThrustMax) : 0);

							if (0 < currSpeed)
								remainThrustNeeded = Math.Max(0, remainThrustNeeded - (localThrustMax * pct));
						}

						float ovr = thrst.GetMaximum<float>("Override") * pct;
						foreach(var t in thrsts) {
							sumThrustCurEff += t.CurrentThrust;
							t.SetValueFloat("Override", ovr);
						}

						AppendPctBar(sb2, "\n Thr:", pct, barLen, false);
						AppendPctBar(sb2, " Eff:", eff, barLen, false, true);
						sb2.Append($" {typeName}");
					}
				}
			}

			public void ThrustZero() {
				thrustPct = remainThrustNeeded = totalThrustWanted = 0;
				for (var thrPrio = thrusts.GetEnumerator(); thrPrio.MoveNext();)
					for (var thrType = thrPrio.Current.Value.GetEnumerator(); thrType.MoveNext();)
						foreach(var t in thrType.Current.Value)
							if (0 < t.ThrustOverride)
								t.SetValueFloat("Override", 0);
			}

			public void ThrustStatus() {
				sb2.Clear();

				sumThrustCurEff = 0;
				string typeName;
				for (var thrPrio = thrusts.GetEnumerator(); thrPrio.MoveNext();) {
					if (!thrustPrioNames.TryGetValue(thrPrio.Current.Key, out typeName))
						typeName = "???";

					for (var thrType = thrPrio.Current.Value.GetEnumerator(); thrType.MoveNext();) {
						var thrsts = thrType.Current.Value;
						if (1 > thrsts.Count)
							continue;

						var thrst = thrsts[0];
						localThrustCur = 0;
						foreach(var t in thrsts)
							localThrustCur += t.CurrentThrust;
						sumThrustCurEff += localThrustCur;

						localThrustMax = thrst.MaxEffectiveThrust * thrsts.Count;
						float pct = (0 < localThrustMax ? Math.Min(1f, localThrustCur / localThrustMax) : 0);
						AppendPctBar(sb2, "\n Thr:", pct, barLen, false);

						if (0 >= thrst.MaxThrust)
							sb2.Append(" Eff:[---Unknown---]");
						else
							AppendPctBar(sb2, " Eff:", thrst.MaxEffectiveThrust / thrst.MaxThrust, barLen, false, true);

						sb2.Append($" {typeName}");
					}
				}
			}
		}
	}
}
