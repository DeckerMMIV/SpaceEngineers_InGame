#if DEBUG
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;

namespace SolarPanelAlignment
{
    public sealed class Program : MyGridProgram
    {
#endif
/* version 1.4.0 (date 2017-02-26)

Planetary Solar Tracking Script (PSTS) by Decker_MMIV
============================================

Instructions on how to use   (YES you should read these!)
------------------------------------

1) Timer Block
	- Set to trigger once per 1 second.
	- Set to run this script (Program Block) without arguments.
	- Set to start itself (Timer Block).

2) Build a T-shaped 'solar tower' consisting of the following:
	a) A horizontally placed Rotor Block
	- where its 0-degree angle points towards 'north' (whatever you designate as 'north')
	- append the following tag to its custom name:
		PSTS-YAW
	b) Place a normal block on top of the horizontal Rotor Block.
	c) Two vertically placed Rotor Blocks, onto the left and right side of the normal block
	- where their 0-degree angle points upwards (up towards the sky)
	- the left-side Rotor Block must have this tag in its custom name:
		PSTS-PITCH-LEFT
	- the right-side Rotor Block must have this tag in its custom name:
		PSTS-PITCH-RIGHT
	d) Build whatever solar-panels onto those two 'PSTS-PITCH-...' Rotor Blocks
	- Suggestion: Start by only building ONE onto each of the pitch rotors,
		to get an idea of how this script works
	- these solar-panels should not need to have any special tags in their custom name
		- However/optional: If you want a/some specific solar-panel(s) to be the one(s) that
		is used for the pitch alignment algorithm, then tag it/them with the following:
			PSTS-PITCH
		- Optional: If you have a/some other solar-panel(s) that should never be considered
		by this script, you can tag it/them with the following:
			PSTS-IGNORE
	e) Only needed for the "primary tower"
	- build a perpendicular placed solar-panel on top of the normal block (from step b)
	- append the following tag to its custom name:
		PSTS-YAW
	- Why? Because this particular solar-panel is used to detect when
		the yaw-rotation is aligned towards the sun.
	f) Optional:
	- Build more secondary 'solar towers', by following steps a through d.
	g) Optional:
	- set up text/lcd-panels containing the following tag in their custom name:
		PSTS-INFO


Troubleshooting
------------------

- The programming block writes out 'ERROR: ...'

You most likely have not set up the "tags" in the required blocks custom names.


- One of my secondary towers have its yaw-rotation offset by 90, 180 or 270 degrees.

You most likely did not build the horizontal rotor-block so its 0-degree
is pointing in the same direction as the primary tower's horizontal rotor-block.

Or you placed the pitch rotors at the wrong side, compared to how the primary tower
has its pitch rotors.


- One of my towers pitch-rotation does not correctly point towards the sun.

You most likely did not build the vertical/pitch rotor-block so its 0-degree
is pointing upwards.

Or you placed the pitch rotors at the wrong side, compared to how the primary tower
has its pitch rotors.


- My towers are not rotating towards the sun, when it passes through the day.

Check that your timer-block is actually triggering the program-block every second.
Check that the program-block is functional, and not writing out errors.
Check that the rotor-blocks and the 'PSTS-YAW' solar-panel are functional
and accessible by the program-block.


- My towers are rotating in the wrong direction. I.e. clockwise, but they should be anti-clockwise.

Edit this script, and set the variable YAW_ROTATE_ANTICLOCKWISE to 'true'.


Change Log
------------
1.4.0
- Updated for SE 1.176-DEV
- Changed to non-deprecated properties, for setting funtional-block's values.

1.3.1
- Batteries discharge/recharge management.

1.3.0
- Ability to adjust rotors that is placed differently than the master,
  by adding "ANGLE:<degrees>" to the rotor's custom name, where
  <degrees> is the integer value to adjust rotation with.
- Removed requirement of needing pitch-left/right rotors.
- Only examine batteries on the same-grid as this programmable block.

1.2.0
- Updated for SE 1.144-STABLE
- Misc. code changes to the detection "algorithm".

1.1.0
- Updated for SE 1.142-DEV

1.0.4
- Misc. code clean-up.

1.0.3
- Changes due to update 1.125
	- Using better accessors, instead of the DetailedInfo workaround.
	- Thus optimizing the speed of this in-game script.

1.0.2
- Removed Echo(), as it causes huge "FPS-drops".
	- Actually it caused an increased 'game-logic' time, just to Echo some text. Very odd?

1.0.1
- Fix due to update 1.123
	The 'MyCubeSize' enum was moved to another namespace.

1.0.0
- Two new tags added, which can also be used in a group's name;
	PTST-DAYLIGHT-ON  : Enables the block when there is daylight, and disables when not.
	PTST-DAYLIGHT-OFF : Disables the block when there is daylight, and enables when not.
	The definition of 'daylight' is when solar-panels receive 80% or more of they maximum potential.
	NOTE: This disable/enable is only triggered when solar-input switches above/below the 80%,
	so it is NOT continuously disabling/enabling.

0.9.10
- Optional tags for solar-panels added;
	PSTS-PITCH  : When at least one found, pitch alignment algorithm will only use this/these.
	PSTS-IGNORE : Completely ignore this solar-panel in calculating total power production.
- Tiny optimization in "compare"-method for GetBlocksOfType().

0.9.9
- Using <block>.RequestEnable(true|false) instead of <block>.ApplyAction("OnOff_On"|"OnOff_Off"), as it seems to not generate any 'EnableMsg' in SHIFT+F11 info.
- Now also resets the FSM-state to 0, when yaw-align is below 80%.
- Use of Math.Pow() in `getPower` method, instead of the multiple ternary-operators.
- Minor refacturing of text-panel output.

0.9.5
- First publish at Steam workshop.
*/


const bool YAW_ROTATE_ANTICLOCKWISE = true;

const string TAG_PREFIX = "PSTS-";
const string TAG_SOLARPANEL_YAW = TAG_PREFIX + "YAW";
const string TAG_SOLARPANEL_PITCH = TAG_PREFIX + "PITCH";
const string TAG_SOLARPANEL_IGNORE = TAG_PREFIX + "IGNORE";
const string TAG_ROTOR_YAW = TAG_PREFIX + "YAW";
const string TAG_ROTOR_PITCH_LEFT = TAG_PREFIX + "PITCH-L";
const string TAG_ROTOR_PITCH_RIGHT = TAG_PREFIX + "PITCH-R";
const string TAG_LCDPANEL_INFO = TAG_PREFIX + "INFO";
const string TAG_LCDPANEL_IMAGE = TAG_PREFIX + "IMAGE";
const string TAG_DAYLIGHT_ENABLE = TAG_PREFIX + "DAYLIGHT-ON";
const string TAG_DAYLIGHT_DISABLE = TAG_PREFIX + "DAYLIGHT-OFF";

const int MIN_ROTOR_TORQUE = 200000;










// ---------------------------------------------------------------------------------------------
bool debugOn = true;

const uint MAJOR_VERSION = 1;
const uint MINOR_VERSION = 4;
const uint PATCH_VERSION = 0;

//
List<IMyTerminalBlock> blockList = new List<IMyTerminalBlock>();
IMySolarPanel solarYaw = null;
List<IMySolarPanel> solarYaws = new List<IMySolarPanel>();
List<IMyMotorStator> rotorYaw = new List<IMyMotorStator>();
List<IMyMotorStator> rotorPitchL = new List<IMyMotorStator>();
List<IMyMotorStator> rotorPitchR = new List<IMyMotorStator>();
List<IMyTextPanel> lcdPanel = new List<IMyTextPanel>();
List<IMyTextPanel> lcdImage = new List<IMyTextPanel>();
List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
StringBuilder _log = new StringBuilder();
StringBuilder _debug = new StringBuilder();

Memory memory = new Memory();
PowerHistory powerHistory = new PowerHistory();
Queue<float> pitchSearchAngles = new Queue<float>();
int pitchState = 0;

/*
StringBuilder _profileSB = new StringBuilder();
DateTime _profilePrev;
void profileStart() {
	_profilePrev = DateTime.Now;
}
void profileStep(string stepName) {
	DateTime now = DateTime.Now;
	_profileSB.Append(stepName).Append(":").Append((now - _profilePrev).Ticks).Append("\n");
	_profilePrev = now;
}
string profileEnd() {
	string result = _profileSB.ToString();
	_profileSB.Clear();
	return result;
}
*/

float batMax,batCur,batIn,batOut,otherBatIn,otherBatOut;
float slrMax,slrCur,slrOut,slrPnls,slrPitchMax,slrPitchCur;

void Main(string arg) {
	Echo(DateTime.Now.ToString("HH\\:mm\\:ss\\.fff"));

	//profileStart();
	_log.Clear();
	_debug.Clear();

	//
	batMax=batCur=batIn=batOut=otherBatIn=otherBatOut=0;
	slrMax=slrCur=slrOut=slrPitchMax=slrPitchCur=0;
	uint slrPnls = 0;
	uint slrPitchPnls = 0;

	float tmpMax = 0;
	float tmpCur = 0;
	float tmpOut = 0;
	int tagIdx;

	//
	Action<IMySolarPanel, bool> sumSolar = (IMySolarPanel slrPnl, bool useTagged) => {
		slrPnls += 1;
		slrMax += (tmpMax = getSolarPanelMaximumPower(slrPnl));
		slrCur += (tmpCur = Watt(slrPnl.MaxOutput));
		slrOut += (tmpOut = Watt(slrPnl.CurrentOutput));
		if (useTagged) {
			slrPitchPnls += 1;
			slrPitchMax += tmpMax;
			slrPitchCur += tmpCur;
		}
	};
	Action<IMyBatteryBlock> sumBattery = (IMyBatteryBlock btyBlk) => {
		batIn  += Watt(btyBlk.CurrentInput);
		batOut += Watt(btyBlk.CurrentOutput);
		batCur += Watt(btyBlk.CurrentStoredPower); 
		batMax += Watt(btyBlk.MaxStoredPower);
	};

	//profileStep("a2");
	IMyMotorStator rtr;
	IMyBatteryBlock bb;
	IMySolarPanel pnl;
	IMyTextPanel tp;
	// Loop through the grid's block-list "only once" per program-run.
	GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blockList, blk => {
		if (blk.IsFunctional) {
			if (null != (pnl = blk as IMySolarPanel)) {
				tagIdx = blk.CustomName.IndexOf(TAG_PREFIX);
				if (tagIdx > -1 && NameContains(blk, TAG_SOLARPANEL_IGNORE)) {
					// Ignore it
				} else if (tagIdx > -1 && NameContains(blk, TAG_SOLARPANEL_YAW)) {
					solarYaws.Add(pnl);
				} else {
					sumSolar(pnl, (tagIdx > -1 && NameContains(blk, TAG_SOLARPANEL_PITCH)));
				}
			} else if (null != (bb = blk as IMyBatteryBlock)) {
				if (SameGrid(Me,blk)) {
					sumBattery(bb);
					batteries.Add(bb);
				} else {
					otherBatIn  += Watt(bb.CurrentInput);
					otherBatOut += Watt(bb.CurrentOutput);
				}
			} else if (null != (rtr = blk as IMyMotorStator)) {
				if (blk.CustomName.IndexOf(TAG_PREFIX) > -1) {
					if (NameContains(blk, TAG_ROTOR_YAW)) {
						rotorYaw.Add(rtr);
					} else if (NameContains(blk, TAG_ROTOR_PITCH_LEFT)) {
						rotorPitchL.Add(rtr);
					} else if (NameContains(blk, TAG_ROTOR_PITCH_RIGHT)) {
						rotorPitchR.Add(rtr);
					}
				}
			} else if (null != (tp = blk as IMyTextPanel)) {
				if (NameContains(blk, TAG_LCDPANEL_INFO)) {
					lcdPanel.Add(tp);
				} else if (NameContains(blk, TAG_LCDPANEL_IMAGE)) {
					lcdImage.Add(tp);
				}
			}
		}
		return false; // Do not add any blocks to `blockList`, as we do not use it anyway.
	});
	//profileStep("a3");

	// Simple sanity check
	bool allOk = true;
	if (solarYaws.Count != 1) {
		allOk = false;
		Echo("ERROR: No exact '" + TAG_SOLARPANEL_YAW + "' solar-panel found!");
	} else {
		solarYaw = solarYaws[0];
	}
	solarYaws.Clear();
	if (rotorYaw.Count <= 0) {
		allOk = false;
		Echo("ERROR: No '" + TAG_ROTOR_YAW + "' rotor found!");
	}
	if (!allOk) {
		return;
	}
	if (rotorPitchL.Count <= 0 && rotorPitchR.Count <= 0) {
		//allOk = false;
		Echo("WARNING: No '" + TAG_ROTOR_PITCH_LEFT + "' nor '" + TAG_ROTOR_PITCH_RIGHT + "' rotors found!");
	}

	memory.load(this);

	if (arg == "reset") {
		Storage = "";
		SetYawSpeed(0);
		UpdatePitchAngle(0);
		powerHistory.clear();
		return;
	}

	// If no explicit named 'PITCH' solar-panels, then use all that was detected
	if (slrPitchPnls <= 0) {
		slrPitchMax = slrMax;
		slrPitchCur = slrCur;
	}

	//profileStep("a4");

	bool hasDayLight = false;
	if (slrPitchMax > 0f) {
		hasDayLight = (slrPitchCur / slrPitchMax > 0.8f);
	}
	if (hasDayLight != memory.getBool("hasDayLight", !hasDayLight)) {
		const string ON = "OnOff_On";
		const string OFF = "OnOff_Off";

		GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blockList, blk => {
			if (NameContains(blk, TAG_DAYLIGHT_ENABLE)) {
				blk.ApplyAction(hasDayLight ? ON : OFF);
			} else if (NameContains(blk, TAG_DAYLIGHT_DISABLE)) {
				blk.ApplyAction(!hasDayLight ? ON : OFF);
			}
			return false;
		});

		List<IMyBlockGroup> blkGrps = new List<IMyBlockGroup>();
		List<IMyTerminalBlock> blks = new List<IMyTerminalBlock>();
		GridTerminalSystem.GetBlockGroups(blkGrps, x => x.Name.Contains(TAG_DAYLIGHT_ENABLE));
		blkGrps.ForEach(g => {
			g.GetBlocks(blks);
			blks.ForEach(blk => blk.ApplyAction(hasDayLight ? ON : OFF));
		});
		GridTerminalSystem.GetBlockGroups(blkGrps, x => x.Name.Contains(TAG_DAYLIGHT_DISABLE));
		blkGrps.ForEach(g => {
			g.GetBlocks(blks);
			blks.ForEach(blk => blk.ApplyAction(!hasDayLight ? ON : OFF));
		});

		memory.setBool("hasDayLight", hasDayLight);
	}

	//
	float slrYawMax = getSolarPanelMaximumPower(solarYaw);
	float slrYawCur = solarYaw.MaxOutput * 1000000f;

	float factor = (slrYawMax > 0 ? slrYawCur / slrYawMax : 0f); // avoid division-by-zero
	float yawAlignPct = (1f - factor);
	float multiply = 1 + MyMath.FastSin(factor);
	float yawSpeed = Math.Min(factor * multiply, 1.0f) * (YAW_ROTATE_ANTICLOCKWISE ? -1f : 1f);
	Debug("YawFactor="+factor);
	Debug("YawSpeed="+yawSpeed);
	if (Math.Abs(yawSpeed) < 0.01f) {
		yawSpeed = 0;
	}

	SetYawSpeed(yawSpeed);
	//profileStep("a5");

	//
	float pitchAlignPct = (slrPitchMax > 0 ? (slrPitchCur / slrPitchMax) : 1f); // avoid division-by-zero
	float pitchWantedAngle = memory.getFloat("pitchWantedAngle", 0f);

	// If yaw alignment less than 80% optimal, then reset pitch.
	if (yawAlignPct < 0.8f) {
		pitchWantedAngle = 0f;
		pitchState = 0;
	}
	//profileStep("a5b");

	float pitchAnglePrecision = UpdatePitchAngle(MathHelper.ToRadians(pitchWantedAngle), true);
	//profileStep("a6");

	if (pitchAnglePrecision > 0.99f) {
		float pitchPrevAngle = pitchWantedAngle;

		if (pitchAlignPct < 0.01f) {
			pitchState = 0;
			pitchWantedAngle = 0f;
			powerHistory.clear();
		} else if (yawAlignPct > 0.85f) {
			/*
				float newAngle = Math.Max(1f, 5f * (1f - pitchAlignPct));
				switch (pitchState++) {
				case 0: // Initiate "search"
					pitchSearchAngles.Enqueue(Math.Max(-90f, pitchWantedAngle - newAngle));
					pitchSearchAngles.Enqueue(Math.Min( 90f, pitchWantedAngle + newAngle));
					pitchWantedAngle = pitchSearchAngles.Dequeue();
					break;
				case 1:
					pitchWantedAngle = pitchSearchAngles.Dequeue();
					break;
				case 2:
					pitchWantedAngle = powerHistory.getBestAngle();
					pitchState = 0;
					break;
				}
			*/
			switch (pitchState++) {
			case 0: // Initiate "search"
				pitchWantedAngle = powerHistory.getBestAngle();
				pitchSearchAngles.Enqueue(Math.Max(-90f, pitchWantedAngle - 2f));
				pitchSearchAngles.Enqueue(Math.Min(90f, pitchWantedAngle + 2f));
				break;
			case 1:
				pitchWantedAngle = pitchSearchAngles.Dequeue();
				break;
			case 2:
				pitchWantedAngle = pitchSearchAngles.Dequeue();
				break;
			case 3:
				pitchWantedAngle = powerHistory.getBestAngle();
				pitchSearchAngles.Enqueue(Math.Max(-90f, pitchWantedAngle - 1f));
				pitchSearchAngles.Enqueue(Math.Min(90f, pitchWantedAngle + 1f));
				pitchState = 1;
				break;
			}
		}

		powerHistory.append(pitchPrevAngle, slrPitchCur);

		memory.setInt("pitchState", pitchState);
	}

	UpdatePitchAngle(MathHelper.ToRadians(pitchWantedAngle));

	memory.setFloat("pitchWantedAngle", pitchWantedAngle);

	Debug("pitchState=" + pitchState);

	// Batteries discharge/recharge management
	if (true) {
		bool allRecharging = true;
		bool allDischarging = true;
		batteries.ForEach(b=>{
			if (b.Enabled) {
				allRecharging  &= b.OnlyRecharge;
				allDischarging &= b.OnlyDischarge;
			}
		});

		bool change = false;
		bool doRecharge = false;
		bool doDischarge = false;
		if (allDischarging && slrCur > batOut) {
			change = doRecharge = true;
		//} else if (allRecharging && ((slrCur - batIn)/slrCur > 0.5f)) {
		} else if (allRecharging && batIn < slrCur*0.1f) {
			change = doDischarge = true;
		}
		if (change) {
			batteries.ForEach(b=>{
				if (b.Enabled) {
					if (doRecharge  != b.OnlyRecharge ) { b.ApplyAction("Recharge"); }
					if (doDischarge != b.OnlyDischarge) { b.ApplyAction("Discharge"); }
				}
			});
		}
	}

	//
	//profileStep("a7");
	memory.save(this);

	//
	//profileStep("a8");
	Log("Batteries: " + powerAsString(batCur, 3) + "h of " + powerAsString(batMax, 3) + "h");
	Log("    Grid In/Out: +" + powerAsString(batIn, 3) + " / -" + powerAsString(batOut, 3));
	Log("  Other In/Out: +" + powerAsString(otherBatIn, 3) + " / -" + powerAsString(otherBatOut, 3));
	Log("SolarPower: " + powerAsString(slrOut, 3) + " of " + powerAsString(slrCur, 3));
	Log("  Yaw-rotation is: " + (YAW_ROTATE_ANTICLOCKWISE ? "Anti-clockwise" : "Clockwise"));
	Log("  Alignment yaw : " + (yawAlignPct * 100f).ToString("N1") + "% (" + powerAsString(slrYawCur) + ")");
	Log("  Alignment pitch: " + (pitchAlignPct * 100f).ToString("N1") + "% (" + powerAsString(slrPitchCur) + ")");
	if (slrPitchPnls > 0) {
		Log("  Using " + slrPitchPnls + " tagged panel" + (slrPitchPnls != 1 ? "s" : "") + " for pitch alignment");
	}
	Log("  SolarPanels optimum max: " + powerAsString(slrMax, 3));

	lcdImage.ForEach(p => {
		p.ClearImagesFromSelection();
		int pct = (int)(pitchAlignPct * 100);
		pct = pct - (pct % 5);
		p.AddImageToSelection(string.Format("Incline {0}", pct.ToString("D3")));
	});
	lcdImage.Clear();

	//
	if (debugOn) {
		Log("\n<<-- DebugInfo -->>");
		Log(_debug.ToString(), false);
		_debug.Clear();
	}
	string txt = _log.ToString();
	_log.Clear();
	//-- Echo is now nerfed, as it causes huge FPS-drops apparently.
	//Echo(txt);

	//profileStep("a9");

	// Clean up
	solarYaw = null;
	rotorYaw.Clear();
	rotorPitchL.Clear();
	rotorPitchR.Clear();
	batteries.Clear();
	//profileStep("a0");

	//txt = profileEnd();

	lcdPanel.ForEach(p => p.WritePublicText(txt));
	lcdPanel.Clear();
}

//
void Log(string txt, bool newLine = true) {
	_log.Append(txt);
	if (newLine)
		_log.Append("\n");
}
void Debug(string txt, bool newLine = true) {
	if (debugOn) {
		_debug.Append(txt);
		if (newLine)
			_debug.Append("\n");
	}
}

//
public static bool SameGrid(IMyTerminalBlock me, IMyTerminalBlock blk) {
	return me.CubeGrid==blk.CubeGrid;
}

public static float Watt(float value) {
	// Apparently IMyBatteryBlock values are in MW, but we want it just in W.
	return value * 1000000f;
}

//
public readonly char[] CHARS_SPACE = { ' ' };
class Memory {
	private Dictionary<string, string> dict = new Dictionary<string, string>();
	private readonly char[] CHARS_COLON = { ';' };
	private readonly char[] CHARS_EQUAL = { '=' };

	public void load(Program me) {
		var prevData = me.Storage.Split(CHARS_COLON, StringSplitOptions.RemoveEmptyEntries);
		dict.Clear();
		for (int i = 0; i < prevData.Length; i++) {
			var kv = prevData[i].Split(CHARS_EQUAL, StringSplitOptions.RemoveEmptyEntries);
			if (kv.Length == 2) {
				dict.Add(kv[0], kv[1]);
			}
		}
	}

	private StringBuilder _sb = new StringBuilder();
	public void save(Program me) {
		_sb.Clear();
		var x = dict.GetEnumerator();
		while (x.MoveNext()) {
			_sb.Append(x.Current.Key).Append("=").Append(x.Current.Value).Append(";");
		}
		me.Storage = _sb.ToString();
		_sb.Clear();
	}

	public string getString(string key, string defValue) {
		return dict.GetValueOrDefault(key, defValue);
	}
	public int getInt(string key, int defValue) {
		return (dict.ContainsKey(key) ? int.Parse(dict[key]) : defValue);
	}
	public float getFloat(string key, float defValue) {
		return (dict.ContainsKey(key) ? float.Parse(dict[key]) : defValue);
	}
	public bool getBool(string key, bool defValue) {
		return (dict.ContainsKey(key) ? bool.Parse(dict[key]) : defValue);
	}

	public void setString(string key, string value) {
		dict[key] = value;
	}
	public void setInt(string key, int value) {
		dict[key] = value.ToString();
	}
	public void setFloat(string key, float value) {
		dict[key] = value.ToString();
	}
	public void setBool(string key, bool value) {
		dict[key] = value.ToString();
	}
}

static bool NameContains(IMyTerminalBlock blk, string txt) {
	return blk.CustomName.Contains(txt);
}

//static int getDetailedInfo(IMyTerminalBlock blk, out string[] lines) {
//    lines = blk.DetailedInfo.Split('\n');
//    return lines.Length;
//}

//float getPower(string detailLine) {
//    if (detailLine == null) { return 0f; }
//    var words = detailLine.Substring(detailLine.IndexOf(':')+1).Split(CHARS_SPACE, StringSplitOptions.RemoveEmptyEntries);
//    float value = float.Parse(words[0]);
//    value *= (float)Math.Pow(1000, "WkMGTPEZY".IndexOf(words[1][0]));
//    return value;
//}

string powerAsString(float value, int factor = 0) {
	if (factor >= 4 || (factor == 0 && value > 1000000000)) {
		return (value / 1000000000).ToString("N2") + " GW";
	}
	if (factor == 3 || (factor == 0 && value > 1000000)) {
		return (value / 1000000).ToString("N2") + " MW";
	}
	if (factor == 2 || (factor == 0 && value > 1000)) {
		return (value / 1000).ToString("N2") + " kW";
	}
	return value.ToString("N0") + " W";
}

//
const float SOLARPANEL_MAXPOWER_LARGEGRID = 120000f / 8; // Large-grid solar-panel consist of  4x2 large-blocks, producing max 120kW total
const float SOLARPANEL_MAXPOWER_SMALLGRID = 30000f / 25; // Small-grid solar-panel consist of 10x5 small-blocks, producing max  30kW total

float getSolarPanelMaximumPower(IMySolarPanel sol) {
	Vector3I s = new Vector3I(1, 1, 1);
	s += sol.Max;
	s -= sol.Min;
	// Counting number of 'cubes' a solarpanel consist of, we use to determine its maximum power potential
	return (s.Size * (sol.CubeGrid.GridSizeEnum == 0 ? SOLARPANEL_MAXPOWER_LARGEGRID : SOLARPANEL_MAXPOWER_SMALLGRID));
}

//
void SetYawSpeed(float yawSpeed) {
	if (rotorYaw.Count <= 0) {
		return;
	}
	yawSpeed = MathHelper.Clamp(yawSpeed, -1f, 1f);
	float masterYawAngle = rotorYaw[0].Angle;
	rotorYaw.ForEach(rotor => {
		if (rotor.Torque < MIN_ROTOR_TORQUE) { rotor.Torque = MIN_ROTOR_TORQUE; }
		if (rotor.BrakingTorque < MIN_ROTOR_TORQUE) { rotor.BrakingTorque = MIN_ROTOR_TORQUE; }

		float angleOff = 0f;
		string name = rotor.CustomName.ToUpper();
		int angleOffset = name.IndexOf("ANGLE:");
		if (angleOffset>-1) {
			var parts = name.Substring(angleOffset + 6).Split(CHARS_SPACE, 2, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 1) {
				if (int.TryParse(parts[0],out angleOffset)) {
					angleOff = MathHelper.ToRadians(angleOffset);
				}
			}
		}

		float angleDiff = MathHelper.WrapAngle((rotor.Angle + angleOff) - masterYawAngle);
		float yawRPM = Math.Max(-1f, Math.Min(yawSpeed - angleDiff, 1f)) / 9.55f;
		if (Math.Abs(yawRPM) >= 0.001f) {
			rotor.Enabled = true;
			rotor.TargetVelocity = yawRPM;
		} else {
			rotor.TargetVelocity = 0;
		}
	});
}

//
float UpdatePitchAngle(float pitchAngle, bool dryRun = false) {
	float precision = 1f;
	precision = Math.Min(precision, UpdatePitchAngle(pitchAngle, rotorPitchL, dryRun));
	precision = Math.Min(precision, UpdatePitchAngle(-pitchAngle, rotorPitchR, dryRun));
	return precision;
}

readonly float MIN_PITCH_ANGLE = MathHelper.ToRadians(-80f);
readonly float MAX_PITCH_ANGLE = MathHelper.ToRadians(80f);
readonly float MAX_PITCH_ANGLE_DIFF = MathHelper.ToRadians(0.5f);
float UpdatePitchAngle(float pitchAngle, List<IMyMotorStator> rotors, bool dryRun = false) {
	float precision = 1f;
	rotors.ForEach(rotor => {
		if (rotor.Torque < MIN_ROTOR_TORQUE) { rotor.Torque = MIN_ROTOR_TORQUE; }
		if (rotor.BrakingTorque < MIN_ROTOR_TORQUE) { rotor.BrakingTorque = MIN_ROTOR_TORQUE; }

		float angleOff = 0f;
		string name = rotor.CustomName.ToUpper();
		//bool angleInv = name.Contains("REVERSE");
		int angleOffset = name.IndexOf("ANGLE:");
		if (angleOffset > -1) {
			var parts = name.Substring(angleOffset + 6).Split(CHARS_SPACE, 2, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 1) {
				if (int.TryParse(parts[0],out angleOffset)) {
					angleOff = MathHelper.ToRadians(angleOffset);
				}
			}
		}

		float angleDiff = MathHelper.WrapAngle(pitchAngle - rotor.Angle);
		if (Math.Abs(angleDiff) > MAX_PITCH_ANGLE_DIFF) {
			precision = Math.Max(0f, precision - Math.Abs(angleDiff));
			float pitchRPM = (angleDiff * 5) / 9.55f;
			if (Math.Abs(pitchRPM) > 0.005f && !dryRun) {
				rotor.Enabled = true;
				if (rotor.LowerLimit < MIN_PITCH_ANGLE) { rotor.LowerLimit = MIN_PITCH_ANGLE; }
				if (rotor.UpperLimit > MAX_PITCH_ANGLE) { rotor.UpperLimit = MAX_PITCH_ANGLE; }
				rotor.TargetVelocity = pitchRPM;
			}
		} else {
			rotor.TargetVelocity = 0;
		}
	});
	return precision;
}

//
class PowerHistory {
	class AnglePower {
		public float Angle { get; }
		public float Power { get; }
		public AnglePower(float angle, float power) {
			Angle = angle;
			Power = power;
		}
	};
	List<AnglePower> history = new List<AnglePower>();
	int maxHistory = 10;

	public void clear() {
		history.Clear();
	}
	public void append(float angle, float power) {
		history.Add(new AnglePower(angle, power));
		while (history.Count > maxHistory) {
			history.RemoveAt(0);
		}
	}
	public float getBestAngle() {
		if (history.Count <= 0)
			return 0;
		int idx = history.Count;
		AnglePower best = history[--idx];
		while (--idx >= 0) {
			float diffPower = best.Power - (history[idx].Power); // * ((100f - (maxHistory - idx)/maxHistory)/100f));
			if (diffPower < 0) {
				best = history[idx];
			}
		}
		return best.Angle;
	}
}

#if DEBUG
} }
#endif
