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
		Whip_Bearing bearingMgr = null;
		class Whip_Bearing : ModuleBase {
			public Whip_Bearing(Program p) : base(p) {
				var m = ">----<";
				var n = $"N.W{m}.N.{m}N.E{m}.E.{m}S.E{m}.S.{m}S.W{m}.W.{m}";
				compassStr = $"{n}{n}";
			}

			IMyShipController sc = null;
			OutputPanel lcds;

			public void Refresh(OutputPanel _lcds, IMyShipController _sc = null)
			{
				sc = _sc ?? Pgm.GetShipController(MultiMix_UsedBlocks);
				lcds = _lcds;

				lcds.SetFont("Monospace", 1.25f);
				lcds.SetAlign(2);
			}

			public bool Active
			{
				get { return isActive; }
				set {
					if (value && null != sc) {
						if (!isActive)
							Pgm.yieldMgr?.Add(Update());
					} else
						isActive = false;
				}
			}
			bool isActive = false;

			int instanceNum = 0;
			IEnumerable<int> Update()
			{
				int thisInstance = ++instanceNum;
				isActive = true;
				absNorthVec = absNorthVecPlanetWorlds;
				while (isActive && thisInstance == instanceNum)
					yield return UpdateBearing() ? 150 : 5000;
			}

			bool UpdateBearing()
			{
				if (GetBearing()) {
					if (lastAng!=ang)
						lcds.ShowPublicText(bearingStr); 
					return true;
				}
				lcds.ShowPublicText("\nError:\nNo natural gravity");
				return false;
			}

			// Based off:
			// // Whip's Compass & Bearing Code v14 - 11/22/17 //
			// http://steamcommunity.com/sharedfiles/filedetails/?id=616627882 

			Vector3D absNorthVecPlanetWorlds = new Vector3D(0, -1, 0);
			Vector3D absNorthVecNotPlanetWorlds = new Vector3D(0.342063708833718, -0.704407897782847, -0.621934025954579);

			readonly string compassStr;

			Vector3D absNorthVec;
			string bearingStr = "";
			int ang=0;
			int lastAng=0;

			bool GetBearing()
			{
				var gravityVec = sc.GetNaturalGravity();
				if (Vector3D.IsZero(gravityVec))
					return false;

				//get east vector  
				var relativeEastVec = gravityVec.Cross(absNorthVec);

				//get relative north vector  
				var relativeNorthVec = relativeEastVec.Cross(gravityVec);

				//project forward vector onto a plane comprised of the north and east vectors  
				var forwardVec = sc.WorldMatrix.Forward;
				var forwardProjPlaneVec = VectorProjection(forwardVec, relativeEastVec) + VectorProjection(forwardVec, relativeNorthVec);

				//find angle from abs north to projected forward vector measured clockwise  
				var bearingAng = Math.Acos(forwardProjPlaneVec.Dot(relativeNorthVec) / forwardProjPlaneVec.Length() / relativeNorthVec.Length()) * (180 / Math.PI);

				//check direction of angle  
				if (forwardVec.Dot(relativeEastVec) < 0)
					bearingAng = 360 - bearingAng; //because of how the angle is measured  

				var lne = compassStr.Substring(MathHelper.Clamp((int)Math.Round(bearingAng / 5), 0, 359), 20);
				lastAng = ang;
				ang = (int)Math.Round(bearingAng);
				bearingStr = $"\n{lne}\nBearing ^ {ang:000}    ";

				return true;
			}

			Vector3D VectorProjection(Vector3D a, Vector3D b) {
				return (a.Dot(b) / b.Length() / b.Length() * b);
			}
		}
	}
}
