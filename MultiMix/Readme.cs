// MultiMix Script - by Decker_MMIV

/* -- Instructions ---------------------------------------------

__ IMPORTANT! __
The timer-block MUST ONLY execute this Programmable Block, AND NOTHING ELSE!
DO NOT add any self-triggering of timer-block!

This script will _itself_ issue either the 'StartTimer'
or 'TriggerNow' action, depending on the script's needs.
Made to keep CPU usage down, and sim-speed high.


__ SETUP __
These 3 blocks should have 'MULTIMIX' in their custom-name:
- Programmable block
- Timer block    (which ONLY runs this PB with no argument)
- Remote control / Flight seat / Cockpit

Also place 3 lcd/text panels (or more), and add to their 
custom-name each of these tags; 'LEFT', 'CENTER', 'RIGHT'


__ RUN ARGUMENTS __ 
The following arguments can be used, when calling the programmable 
block from a cockpit/remote-control's toolbar-menu:

- UP
- DOWN
- LEFT
- RIGHT
- ENTER
- BACK
	To control the menu, and activate/change/increase/decrease
	the value(s) of selected menu-item

- RESET
	Causes the script to reinitialize itself, with what blocks are available.
	Use this command if blocks have been added/removed.


__ OPTIONAL ARGUMENTS __

- HELP
	Gathers a list of 'direct commands', i.e. argument strings which
	this PB can be called with, to directly execute the ENTER command
	on a menu-item.
	The list will be echo'ed in the control terminal for the marked PB.

- TOGGLEALIGN
	Toggles the gravity-/velocity-alignment on/off.
	If in zero-gravity, then the ship's velocity will be used for alignment,
	a quick method to align the "big thrusters" in direction of travel.

- TOGGLEALIGNINVERT
	Inverts the velocity-alignment, so the "big thrusters" either points
	against direction of travel (for braking), or with it (for more speed).
	Difficult to explain, so just try to experiment with it in zero-g.

- TOGGLEALIGNROCKET
	Depending on how the remote-control/flight-seat/cockpit is oriented,
	this will toggle to use either the 'push-up thrusters' (as a ship), 
	or the 'push-forward thrusters' (as a rocket).

*/