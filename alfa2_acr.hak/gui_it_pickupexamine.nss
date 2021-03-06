////////////////////////////////////////////////////////////////////////////////
//
//  System Name : ACR GUI Script
//     Filename : gui_it_pickupexamine
//    $Revision:: 505        $ current version of the file
//        $Date:: 2008-04-10#$ date the file was created or modified
//       Author : Cipher
//
//    Var Prefix: ACR_GUI
//  Dependencies: None
//
//  Description
//  This script is called from the "Pick Up and Equip" option on the context menu.
//
//  Revision History
//  2008/04/08  Cipher  Inception
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Includes ////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Constants ///////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Structures //////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Global Variables ////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Function Prototypes /////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Function Definitions ////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

void main(string sResRef)
{
    object oPC = OBJECT_SELF;
    object oTarget = GetPlayerCurrentTarget(oPC);
	
    // no security required - anyone can use this script

    // pickup and equip the targetted item
    ActionPickUpItem(oTarget);
	ActionExamine(oTarget);
}