// DoorsAutoClose Script - by Decker_MMIV

/* -- Instructions ---------------------------------------------

__ IMPORTANT! __
The timer-block MUST ONLY execute this Programmable Block, AND NOTHING ELSE!
DO NOT add any self-triggering of timer-block!

This script will _itself_ issue either the 'StartTimer'
or 'TriggerNow' action, depending on the script's needs.
Made to keep CPU usage down, and sim-speed high.


__ SETUP __
These 2 blocks should have 'DOORSAUTOCLOSE' in their custom-name:
- Programmable block
- Timer block    (which ONLY runs this PB with no argument)

*/