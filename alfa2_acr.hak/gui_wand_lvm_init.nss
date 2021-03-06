#include "wand_inc"

void main () {
	object oSubject = OBJECT_SELF;

	if (!IsDm(oSubject)) {
		DisplayMessageBox(oSubject, -1, WAND_NO_SPAM);
        return;
    }

	InitAreaList(oSubject);

	//Misc
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "TITLE_TEXT", -1, WAND_UI_LV_TITLE);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "LOC_X", -1, WAND_UI_LV_X);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "LOC_Y", -1, WAND_UI_LV_Y);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "LOC_Z", -1, WAND_UI_LV_Z);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "LOC_AREA", -1, WAND_UI_LV_AREA);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "LOC_ORIENT", -1, WAND_UI_LV_ORIENT);
			
	// Buttons	
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "NEW_INT_BUTTON", -1, WAND_UI_LV_NEW);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "NEW_FLOAT_BUTTON", -1, WAND_UI_LV_NEW);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "NEW_STR_BUTTON", -1, WAND_UI_LV_NEW);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "NEW_LOC_BUTTON", -1, WAND_UI_LV_NEW);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "NEW_OBJ_BUTTON", -1, WAND_UI_LV_NEW);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "ACCEPT_INT", -1, WAND_UI_LV_ACCEPT);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "DELETE_INT", -1, WAND_UI_LV_DELETE);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "CANCEL_INT", -1, WAND_UI_LV_CANCEL);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "ACCEPT_FLOAT", -1, WAND_UI_LV_ACCEPT);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "DELETE_FLOAT", -1, WAND_UI_LV_DELETE);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "CANCEL_FLOAT", -1, WAND_UI_LV_CANCEL);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "ACCEPT_STR", -1, WAND_UI_LV_ACCEPT);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "DELETE_STR", -1, WAND_UI_LV_DELETE);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "CANCEL_STR", -1, WAND_UI_LV_CANCEL);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "ACCEPT_LOC", -1, WAND_UI_LV_ACCEPT);	
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "DELETE_LOC", -1, WAND_UI_LV_DELETE);					
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "CANCEL_LOC", -1, WAND_UI_LV_CANCEL);
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "ACCEPT_OBJ", -1, WAND_UI_LV_ACCEPT);	
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "DELETE_OBJ", -1, WAND_UI_LV_DELETE);					
	SetGUIObjectText(oSubject, WAND_GUI_LV_MANAGER, "CANCEL_OBJ", -1, WAND_UI_LV_CANCEL);

	// Tooltips
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1000, WAND_UI_LV_TT_OBJ_ID);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1001, WAND_UI_LV_TT_NEW_VAR);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1002, WAND_UI_LV_TT_SAVE);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1003, WAND_UI_LV_TT_DELETE);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1004, WAND_UI_LV_TT_CANCEL);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1005, WAND_UI_LV_TT_INTEGER);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1006, WAND_UI_LV_TT_FLOAT);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1007, WAND_UI_LV_TT_STRING);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1008, WAND_UI_LV_TT_LOCATION);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1009, WAND_UI_LV_TT_OBJECT);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1010, WAND_UI_LV_TT_CLICK);
	SetLocalGUIVariable(oSubject, WAND_GUI_LV_MANAGER, 1011, WAND_UI_LV_TT_OBJ_NAME);
	
}

