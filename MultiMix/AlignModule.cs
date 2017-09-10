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
		AlignModule alignMgr = null;
		class AlignModule : TickBase {
			public AlignModule(Program p) : base(p) { }

			public void AddMenu(MenuManager menuMgr) {
				menuMgr.Add(
					Menu("Alignment settings").Add(
						Menu(() => LabelOnOff(Active, "State:", "ENABLED", "OFF"))
							.Enter("toggleAlign", () => Active = !Active),
						Menu(() => LabelOnOff(!Inverted, "Inverted:", "NO", "YES"))
							.Enter("toggleAlignInvert", () => Inverted = !Inverted),
						Menu(() => LabelOnOff(!IgnoreGravity, "Against:", "Gravity or velocity", "Only velocity"))
							.Enter("toggleAlignVelocity", () => IgnoreGravity = !IgnoreGravity),
						Menu(() => LabelOnOff(!RocketMode, "Mode:", "Ship", "Rocket"))
							.Enter("toggleAlignRocket", () => RocketMode = !RocketMode),
						Menu(() => $"Gyro coeff: {GyroCoeff:F1}")
							.Left(() => GyroCoeff -= 0.1)
							.Right(() => GyroCoeff += 0.1)
							.Back(() => GyroCoeff = 0.5)
					)
				);
			}

			bool alignFastTrigger = false;
			long nextTickGyros;

			override public bool Tick() {
				bool fastTrigger = isActive & alignFastTrigger;

				if (isActive && nextTickGyros < Pgm.totalTicks) {
					fastTrigger |= (alignFastTrigger = AdjustGyros());
					nextTickGyros = Pgm.totalTicks + TimeSpan.FromMilliseconds(alignFastTrigger ? 100 : 1000).Ticks;
				}
				return fastTrigger;
			}

			AscendDecendModule asc = null;
			IMyShipController sc = null;
			List<IMyGyro> gyros = new List<IMyGyro>();

			public void Refresh(AscendDecendModule _asc, IMyShipController _sc = null) {
				asc = _asc;
				sc = _sc ?? Pgm.GetShipController(MultiMix_UsedBlocks);

				gyros.Clear();

				IMyGyro g = null;
				GatherBlocks(Pgm, new List<Func<IMyTerminalBlock, bool>> {
					(b) => SameGrid(b,Me) && !NameContains(b,MultiMix_IgnoreBlocks),
					(b) => { if (ToType(b, ref g) && g.IsWorking) { gyros.Add(g); return false; } return true; },
				});

				nextTickGyros = 0;
				maxYaw = (gyros.Count > 0 ? gyros[0].GetMaximum<float>("Yaw") : 0);
			}

			public new bool Active {
				get { return isActive; }
				set { SetActive(value); }
			}
			bool isActive = false;
			void SetActive(bool enable) {
				if ((isActive = enable) && null != sc) {
					nextTickGyros = 0;
				} else {
					isActive = false;
					alignDifference = 0;
					gyros.ForEach(g => SetGyro(g));
				}
			}

			public bool IgnoreGravity { 
				get { return ignoreGravity; }
				set { ignoreGravity=value; }
			}
			bool ignoreGravity = false;

			public bool Inverted { 
				get { return isInverted; } 
				set { SetInverted(value); }
			}
			bool isInverted = false;
			void SetInverted(bool invert) {
				isInverted = (!ignoreGravity && sc.GetNaturalGravity().Length() > 0) ? false : invert;
			}

			public bool RocketMode { 
				get { return isRocket; } 
				set { SetRocket(value); }
			}
			bool isRocket = false;
			void SetRocket(bool enable) {
				isRocket = enable;
				nextTickGyros = 0;
				if (null != asc) {
					asc.ThrustZero();
					asc.RefreshThrustsList(isRocket);
				}
			}

			public bool IsAligned { get { return alignDifference < 0.01; } }
			public double AlignDifference { get { return alignDifference; } }

			bool needFastTrigger;
			Matrix or;
			double maxYaw;
			double ctrl_vel;
			double ang;
			double forceRotation;
			double alignDifference = 0;
			Vector3D down;
			Vector3D alignVec;
			Vector3D localDown;
			Vector3D localGrav;
			Vector3D rot;
			double ctrl_Coeff = 0.8; //Set lower if overshooting, set higher to respond quicker
			public double GyroCoeff {
				get { return ctrl_Coeff; }
				set { ctrl_Coeff = Math.Max(0.0, Math.Min(1.0, value)); }
			}

			bool AdjustGyros() {
				// Credits to JoeTheDestroyer
				// http://forums.keenswh.com/threads/aligning-ship-to-planet-gravity.7373513/#post-1286885461 
				
				if (!ignoreGravity) {
					alignVec = sc.GetNaturalGravity();
				}
				if (ignoreGravity || Vector3D.IsZero(alignVec)) {
					// When in zero-gravity, then use direction of ship-velocity instead
					alignVec = sc.GetShipVelocities().LinearVelocity;
					if (Vector3D.IsZero(alignVec)) {
						// No usable velocity, reset all gyros
						foreach(var g in gyros) { SetGyro(g); }
						return false;
					}
				}
				if (isInverted) {
					alignVec = Vector3D.Negate(alignVec);
				}
				// Naive attempt to avoid "Gimbal lock"
				forceRotation = (alignVec.Dot(sc.WorldMatrix.GetOrientation().Down) < 0) ? 1 : 0;

				alignVec.Normalize();

				sc.Orientation.GetMatrix(out or); //Get orientation from reference-block
				down = (isRocket ? or.Backward : or.Down);

				needFastTrigger = false;
				alignDifference = 0;
				foreach(var g in gyros) {
					g.Orientation.GetMatrix(out or);
					localDown = Vector3D.Transform(down, MatrixD.Transpose(or));
					localGrav = Vector3D.Transform(alignVec, MatrixD.Transpose(g.WorldMatrix.GetOrientation()));

					//Since the gyro ui lies, we are not trying to control yaw,pitch,roll 
					//but rather we need a rotation vector (axis around which to rotate)
					rot = Vector3D.Cross(localDown, localGrav);
					ang = rot.Length() + forceRotation; // Naive fix for "Gimbal lock"
					ang = Math.Atan2(ang, Math.Sqrt(Math.Max(0.0, 1.0 - ang * ang))); //More numerically stable than: ang=Math.Asin(ang)

					if (ang >= 0.01) {
						//Control speed to be proportional to distance (angle) we have left
						ctrl_vel = Math.Max(0.01, Math.Min(maxYaw, (maxYaw * (ang / Math.PI) * ctrl_Coeff)));

						rot.Normalize();
						rot *= -ctrl_vel;

						SetGyro(g, 1, true, (float)rot.GetDim(0), (float)rot.GetDim(1), (float)rot.GetDim(2));

						alignDifference += ang;
						needFastTrigger = true;
					} else {
						SetGyro(g);
					}
				}
				return needFastTrigger;
			}
		}
	}
}
