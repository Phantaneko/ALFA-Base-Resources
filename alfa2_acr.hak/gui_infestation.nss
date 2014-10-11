////////////////////////////////////////////////////////////////////////////////
//
//  System Name : ACR GUI Script
//     Filename : gui_infestation
//        $Date:: 2014-10-11#$ date the file was created or modified
//       Author : Zelknolf
//
//    Var Prefix: ACR_GUI
//  Dependencies: None
//
//  Description
//  This script is called from the infestation GUI.
//
//  Revision History
////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// Includes ////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

#include "acr_quest_i"

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

void main(string Command, string Param1)
{
    if(!GetIsDM(OBJECT_SELF))
    {
        SendMessageToPC(OBJECT_SELF, "Only DMs may use this script.");
        return;
    }
    
    if(Command == "1")
    {
        PopulateInfestationGui();
    }
}