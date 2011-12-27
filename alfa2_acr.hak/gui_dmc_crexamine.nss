////////////////////////////////////////////////////////////////////////////////
//
//  System Name : ACR GUI Script
//     Filename : gui_dmc_crexamine
//    $Revision:: 307        $ current version of the file
//        $Date:: 2007-04-09#$ date the file was created or modified
//       Author : Cipher
//
//    Var Prefix:
//  Dependencies:
//
//  Description
//  This script is called from the DM Creator in the DM Client. It displays the
//  targetted creature's statistics (sheet) for the DM to view before spawning.
//
//  Revision History
//  2007-02-18  Cipher  Inception
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
	object oDM = OBJECT_SELF;
	
	// limit this function to calls by DMs (DM Creator)
	if (!(GetIsDM(oDM) || GetIsDMPossessed(oDM))) { return; }

	// destroy any creature that's currently being examined
	if (GetIsObjectValid(GetLocalObject(oDM, "ACR_GUI_EXAMINE"))) { DestroyObject(GetLocalObject(oDM, "ACR_GUI_EXAMINE")); }

	// create the creature and the effects to apply to the creature
	effect eEffect = EffectLinkEffects(EffectCutsceneGhost(), EffectCutsceneImmobilize());
	object oCreature = CreateObject(OBJECT_TYPE_CREATURE, sResRef, GetLocation(oDM));
	ApplyEffectToObject(DURATION_TYPE_PERMANENT, eEffect, oCreature); 

	// set the creature's name (strip curly braces) and give the DM feedback
	string sName = GetName(oCreature); sName = GetSubString(sName, 1, GetStringLength(sName)-2);
	SendMessageToPC(oDM, "Retrieving <color=Magenta><i>" + sName + "</i></color> Creature Sheet");
	//SetName(oCreature, sName);
	
	// bring up the examine window - make sure a creature has been targetted first
	DelayCommand(0.3, ActionExamine(oCreature));
	
	// store the creature object so it can be deleted when the examine window is closed
	SetLocalObject(oDM, "ACR_GUI_EXAMINE", oCreature);
}