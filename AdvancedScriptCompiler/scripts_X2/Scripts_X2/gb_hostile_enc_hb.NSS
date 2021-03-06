//	gb_hostile_enc_hb
/*
	Heartbeat script for overland map hostile encounters.
*/
// JH/EF-OEI: 01/16/08
// NLC: 2/28/08 - rewritten using FSM
#include "nw_i0_generic"
#include "ginc_debug"
#include "ginc_overland"
#include "ginc_overland_ai"

void main()
{
	object oPC = GetFactionLeader(GetFirstPC());
	
	//	Pause lifespan timer if I or PC is talking to someone	*/
	if (IsInConversation(OBJECT_SELF) || IsInConversation(oPC) || GetGlobalInt(VAR_ENC_IGNORE) || GetLocalInt(oPC, "bLoaded") != TRUE)
	{
		ClearAllActions();
		return;
	}	
	
	//	Pause lifespan timer if the party isn't on the Overland Map.	*/	
	if(GetArea(oPC) != GetArea(OBJECT_SELF))
	{
		return;
	}
	
	RunStateManager(oPC);
	IncrementLifespanTimer();
	DecrementSearchCooldown();
	PrettyDebug(GetName(OBJECT_SELF) + " Lifespan:" + FloatToString(GetLocalFloat(OBJECT_SELF, VAR_LIFESPAN_TIMER)));
    if(GetSpawnInCondition(NW_FLAG_HEARTBEAT_EVENT))
    {
        SignalEvent(OBJECT_SELF, EventUserDefined(EVENT_HEARTBEAT));
    }
}