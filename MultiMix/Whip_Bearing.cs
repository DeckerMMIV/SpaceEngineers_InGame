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
			// Based off:
			// // Whip's Compass & Bearing Code v14 - 11/22/17 //
			// http://steamcommunity.com/sharedfiles/filedetails/?id=616627882 
			public Whip_Bearing(Program p) : base(p) {}

			IMyShipController sc = null;
			OutputPanel lcds;

			public void Refresh(OutputPanel _lcds, IMyShipController _sc = null, bool _writeBearingOnCompass = false)
			{
				sc = _sc ?? Pgm.GetShipController(MultiMix_UsedBlocks);
				lcds = _lcds;
				writeBearingOnCompass = _writeBearingOnCompass;

				lcds.SetFont("Monospace", 1.25f);
			}

			public bool Active
			{
				get { return isActive; }
				set {
					if (value && null != sc) {
						if (!isActive)
							Pgm.yieldMgr?.Add(Update());
					} else {
						isActive = false;
					}
				}
			}
			bool isActive = false;

			int instanceNum = 0;
			IEnumerable<int> Update()
			{
				int thisInstance = ++instanceNum;
				isActive = true;
				absNorthVec = absNorthVecPlanetWorlds;
				while (isActive && thisInstance == instanceNum) {
					yield return UpdateBearing() ? 200 : 5000;
				}
			}

			Vector3D absNorthVecPlanetWorlds = new Vector3D(0, -1, 0);
			Vector3D absNorthVecNotPlanetWorlds = new Vector3D(0.342063708833718, -0.704407897782847, -0.621934025954579);

			const string compassStr = "N.W>----"
				+ "< N >----<N.E>----< E >----<S.E>----< S >----<S.W>----< W >----<N.W>----"
				+ "< N >----<N.E>----< E >----<S.E>----< S >----<S.W>----< W >----<N.W>----";
			bool writeBearingOnCompass = false;
			Vector3D absNorthVec;
			string bearingStr = "";

			bool UpdateBearing()
			{
				if (GetBearing()) {
					lcds.ShowPublicText(bearingStr); 
					return true;
				}
				lcds.ShowPublicText("Error: No natural gravity");
				return false;
			}

			bool GetBearing()
			{
				var forwardVec = sc.WorldMatrix.Forward;
				var gravityVec = sc.GetNaturalGravity();

				//check if grav vector exists  
				double gravMag = gravityVec.LengthSquared();
				if (double.IsNaN(gravMag) || gravMag == 0) {
					return false;
				}

				//get east vector  
				var relativeEastVec = gravityVec.Cross(absNorthVec);

				//get relative north vector  
				var relativeNorthVec = relativeEastVec.Cross(gravityVec);

				//project forward vector onto a plane comprised of the north and east vectors  
				var forwardProjPlaneVec = VectorProjection(forwardVec, relativeEastVec) + VectorProjection(forwardVec, relativeNorthVec);

				//find angle from abs north to projected forward vector measured clockwise  
				double bearingAng = Math.Acos(forwardProjPlaneVec.Dot(relativeNorthVec) / forwardProjPlaneVec.Length() / relativeNorthVec.Length()) * (180 / Math.PI);

				//check direction of angle  
				if (forwardVec.Dot(relativeEastVec) < 0) {
					bearingAng = 360 - bearingAng; //because of how the angle is measured  
				}

				if (!writeBearingOnCompass) {
					string cardinalDir;
					if (bearingAng < 22.5) { cardinalDir = "N"; }
					else if (bearingAng < 67.5) { cardinalDir = "NE"; }
					else if (bearingAng < 112.5) { cardinalDir = "E"; }
					else if (bearingAng < 157.5) { cardinalDir = "SE"; }
					else if (bearingAng < 202.5) { cardinalDir = "S"; }
					else if (bearingAng < 247.5) { cardinalDir = "SW"; }
					else if (bearingAng < 292.5) { cardinalDir = "W"; }
					else if (bearingAng < 337.5) { cardinalDir = "NW"; }
					else { cardinalDir = "N"; }

					bearingStr = $"Bearing: {string.Format("{0:000}", Math.Round(bearingAng))} {cardinalDir}\n"
						+ compassStr.Substring(MathHelper.Clamp((int)Math.Round(bearingAng / 5), 0, 359), 21)
						+ "\n          ^";
				} else {
					bearingStr = compassStr.Substring(MathHelper.Clamp((int)Math.Round(bearingAng / 5), 0, 359), 21) + "\n          ^";
					bearingStr = bearingStr.Remove(8, 5).Insert(8, $"<{string.Format("{0:000}", Math.Round(bearingAng))}>");
				}

				return true;
			}

			Vector3D VectorProjection(Vector3D a, Vector3D b) {
				return (a.Dot(b) / b.Length() / b.Length() * b);
			}
		}
	}
}
