﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using CLRScriptFramework;
using ALFA;
using NWScript;
using NWScript.ManagedInterfaceLayer.NWScriptManagedInterface;

using NWEffect = NWScript.NWScriptEngineStructure0;
using NWEvent = NWScript.NWScriptEngineStructure1;
using NWLocation = NWScript.NWScriptEngineStructure2;
using NWTalent = NWScript.NWScriptEngineStructure3;
using NWItemProperty = NWScript.NWScriptEngineStructure4;

namespace ACR_CreatureBehavior
{
    /// <summary>
    /// This class represents a creature within the game.
    /// </summary>
    public class CreatureObject : GameObject
    {

        /// <summary>
        /// Construct a creature object and insert it into the object table.
        /// </summary>
        /// <param name="ObjectId">Supplies the creature object id.</param>
        /// <param name="ObjectManager">Supplies the object manager.</param>
        public CreatureObject(uint ObjectId, GameObjectManager ObjectManager) : base(ObjectId, GameObjectType.Creature, ObjectManager)
        {
            //
            // Cache state that doesn't change over the lifetime of the object
            // so as to avoid a need to call down into the engine for these
            // fields.
            //

            CreatureIsPC = Script.GetIsPC(ObjectId) != CLRScriptBase.FALSE ? true : false;
            CreatureIsDM = Script.GetIsDM(ObjectId) != CLRScriptBase.FALSE ? true : false;
            PerceivedObjects = new List<PerceptionNode>();
        }

        /// <summary>
        ///  Called when the creature is spawned, immediately after the class' constructors are run.
        ///  Responsible for establishing party membership.
        /// </summary>
        public void OnSpawn()
        {
            bool PartyAssigned = false;
            uint NearbyCreatureId = Script.GetFirstObjectInShape(CLRScriptBase.SHAPE_SPHERE, 25.0f, Script.GetLocation(this.ObjectId), CLRScriptBase.TRUE, CLRScriptBase.OBJECT_TYPE_CREATURE, Script.Vector(0.0f,0.0f,0.0f));
            if(NearbyCreatureId != OBJECT_INVALID && PartyAssigned == false)
            {
                if (Script.GetFactionEqual(this.ObjectId, NearbyCreatureId) == CLRScriptBase.TRUE)
                {
                    CreatureObject NearbyCreature = Server.ObjectManager.GetCreatureObject(NearbyCreatureId);
                    if (NearbyCreature != null)
                    {
                        AIParty Party = NearbyCreature.Party;
                        if(Party == null)
                            throw new ApplicationException(String.Format("Creature {0} has spawned without being added to a party.", NearbyCreature));
                        else
                        {
                            Party.AddPartyMember(this);
                        }
                    }
                    else
                    {
                        // This is an error-- an existing creature slipped by us?
                        throw new ApplicationException(String.Format("Creature with ID {0} has spawned, but has not been indexed by ACR_CreatureBehavior.", NearbyCreatureId));
                    }
                }
                Script.GetFirstObjectInShape(CLRScriptBase.SHAPE_SPHERE, 25.0f, Script.GetLocation(this.ObjectId), CLRScriptBase.TRUE, CLRScriptBase.OBJECT_TYPE_CREATURE, Script.Vector(0.0f,0.0f,0.0f));
            }
        }

        /// <summary>
        /// Called when the C# object state is being run down because the game
        /// engine half of the object has been deleted.  This might occur after
        /// the fact.
        /// </summary>
        public override void OnRundown()
        {
            if (Party != null)
                Party.RemovePartyMember(this);
        }

        /// <summary>
        /// Called when a spell is cast on the creature.
        /// </summary>
        /// <param name="CasterObjectId">Supplies the spell caster object id.
        /// </param>
        /// <param name="SpellId">Supplies the spell id.</param>
        public void OnSpellCastAt(uint CasterObjectId, int SpellId)
        {
            if (!IsAIControlled)
                return;

            int nSpell = Script.GetLastSpell();
            int nHostile = Script.StringToInt(Script.Get2DAString("spells","HostileSetting", nSpell));

//===================================================================================================================================
//=======================  Handling for harmful spells ==============================================================================
//===================================================================================================================================
            if (nHostile == 1)
            {
                // As this is a harmful spell, the target is probably going to be unhappy about it.
                bool bAngry = true;
                uint CasterId =  Script.GetLastSpellCaster();
                int nReputation = Script.GetReputation(this.ObjectId, CasterId);

                // If the creature is -already- hostile, we don't need to make it any -more- angry.
                if (nReputation <= 10)
                    bAngry = false;

                // If this is the caster hitting him or herself, no doubt that's angering, but we don't need to change behavior because of it.
                if (CasterId == this.ObjectId)
                    bAngry = false;

                // Maybe mind-controlling magic is in play?
                if (bAngry)
                {
                    if (_IsMindMagiced(this.ObjectId)) bAngry = false;
                    if (_IsMindMagiced(CasterId)) bAngry = false;
                }

                // If mind magics didn't motivate the spell, we check to see if it's plausibly friendly fire.
                if (bAngry)
                {
                    int nTargetArea = Script.StringToInt(Script.Get2DAString("spells", "TargetingUI", nSpell));
                    if (_IsFriendlyFire(nTargetArea)) bAngry = false;
                }

                // Is the target a friend to the caster?
                if (bAngry)
                {
                    CreatureObject Caster = Server.ObjectManager.GetCreatureObject(CasterId, true);
                    AIParty Party = this.Party;

                    // This is the fault of a bug in the AI; best not to compound it.
                    if (Party.PartyMembers.Contains(Caster))
                    {
                        bAngry = false;
                    }

                    // These two creatures are friends.
                    else if (nReputation > 89)
                    {
                        Script.SetLocalInt(this.ObjectId, "FRIENDLY_FIRED", Script.GetLocalInt(this.ObjectId, "FRIENDLY_FIRED") + 1);
                    }

                    // Neutral creatures take direct attacks personally. Your friends try to trust you, but will snap if abused too much.
                    else if (nReputation > 10 || Script.GetLocalInt(this.ObjectId, "FRIENDLY_FIRED") > Script.d4(3))
                    {
                        // And all of them are going to get angry.
                        foreach (CreatureObject CurrentPartyMember in this.Party.PartyMembers)
                        {
                            _SetMutualEnemies(CurrentPartyMember.ObjectId, CasterId);
                            if (!CurrentPartyMember.HasCombatRoundProcess)
                                 CurrentPartyMember.SelectCombatRoundAction();
                        }
                        this.Party.AddPartyEnemy(Caster);
                        this.Party.EnemySpellcasters.Add(Caster);
                    }
                }
            }

//===================================================================================================================================
//=======================  Handling for non-harmful spells ==========================================================================
//===================================================================================================================================
            else
            {
            }
        }

        /// <summary>
        /// Called when the creature is damaged.
        /// </summary>
        /// <param name="DamagerObjectId">Supplies the damager object id.
        /// </param>
        /// <param name="TotalDamageDealt">Supplies the total damage dealt.
        /// </param>
        public void OnDamaged(uint DamagerObjectId, int TotalDamageDealt)
        {
            if (!IsAIControlled)
                return;

            CreatureObject Damager = Server.ObjectManager.GetCreatureObject(DamagerObjectId, true);
            AIParty Party = this.Party;

            int nReputation = Script.GetReputation(this.ObjectId, DamagerObjectId);

            // The friendly fire functionality lives in the spell cast method
            if (Script.GetLastSpellCaster() == DamagerObjectId)
                return;


            // This is the fault of a bug in the AI; best not to compound it.
            if (Party.PartyMembers.Contains(Damager))
                return;

            // These two creatures are friends.
            else if (nReputation > 89)
            {
                Script.SetLocalInt(this.ObjectId, "FRIENDLY_FIRED", Script.GetLocalInt(this.ObjectId, "FRIENDLY_FIRED") + 1);
            }

            // Neutral creatures take direct attacks personally. Your friends try to trust you, but will snap if abused too much.
            else if (nReputation > 10 || Script.GetLocalInt(this.ObjectId, "FRIENDLY_FIRED") > Script.d4(3))
            {
                // And all of them are going to get angry.
                foreach (CreatureObject CurrentPartyMember in this.Party.PartyMembers)
                {
                    _SetMutualEnemies(CurrentPartyMember.ObjectId, DamagerObjectId);
                    if (!CurrentPartyMember.HasCombatRoundProcess)
                        CurrentPartyMember.SelectCombatRoundAction();
                }
                this.Party.AddPartyEnemy(Damager);
                this.Party.EnemySpellcasters.Add(Damager);
            }            
        }

        /// <summary>
        /// Called when the creature dies.
        /// </summary>
        /// <param name="KillerObjectId">Supplies the killer object id.</param>
        public void OnDeath(uint KillerObjectId)
        {
            if (!IsAIControlled)
                return;

            this.Party.RemovePartyMember(this);
        }

        /// <summary>
        /// Called when the creature's movement is blocked.
        /// </summary>
        /// <param name="BlockerObjectId">Supplies the blocker object
        /// id.</param>
        public void OnBlocked(uint BlockerObjectId)
        {
            if (!IsAIControlled)
                return;
            if (Script.GetObjectType(BlockerObjectId) == CLRScriptBase.OBJECT_TYPE_CREATURE)
            {
                if (Script.GetReputation(this.ObjectId, BlockerObjectId) < 11)
                {
                }
                else
                {
                    _TemporaryRangedCombat(BlockerObjectId);
                }
            }
            else if (Script.GetObjectType(BlockerObjectId) == CLRScriptBase.OBJECT_TYPE_DOOR)
            {
                if (Script.GetUseableFlag(BlockerObjectId) == CLRScriptBase.TRUE)
                {
                    if (Script.GetPlotFlag(BlockerObjectId) == CLRScriptBase.TRUE)
                    {
                        if (Script.GetLocked(BlockerObjectId) == CLRScriptBase.TRUE)
                        {
                            // Well, it's a closed locked plot door. I think we're screwed.
                            return;
                        }
                        else
                            Script.ActionOpenDoor(BlockerObjectId);
                    }
                    else
                    {
                        if (Script.GetLocked(BlockerObjectId) == CLRScriptBase.TRUE)
                            _BashObject(BlockerObjectId);
                        else
                            Script.ActionOpenDoor(BlockerObjectId);
                    }
                }
            }
            else if (Script.GetObjectType(BlockerObjectId) == CLRScriptBase.OBJECT_TYPE_PLACEABLE)
            {
                if (Script.GetPlotFlag(BlockerObjectId) == CLRScriptBase.TRUE)
                {
                    // Well, thatps a plot placeable. We're probably not getting through.
                    return;
                }
                else
                    _BashObject(BlockerObjectId);
            }
        }

        /// <summary>
        /// Called when the perception state of the creature changes.
        /// </summary>
        /// <param name="PerceivedObjectId">Supplies the perceived object id.
        /// </param>
        /// <param name="Heard">True if the object is now heard.</param>
        /// <param name="Inaudible">True if the object is now not
        /// heard.</param>
        /// <param name="Seen">True if the object is now seen.</param>
        /// <param name="Vanished">True if the object is now not seen.</param>
        public void OnPerception(uint PerceivedObjectId, bool Heard, bool Inaudible, bool Seen, bool Vanished)
        {
            //
            // No need to manage perception tracking for PC or DM avatars as
            // these will never be AI controlled.
            //

            if (IsPC || IsDM)
                return;

            //
            // Update the perception list.  This is done even if we are not yet
            // AI controlled (as control could be temporarily suspended e.g. by
            // DM command).
            //

            PerceptionNode Node = (from PN in PerceivedObjects
                                   where PN.PerceivedObjectId == PerceivedObjectId
                                   select PN).FirstOrDefault();
            bool NewNode = false;
            bool RemoveNode = false;

            if (Node == null)
            {
                if (Heard || Seen)
                {
                    Node = new PerceptionNode(PerceivedObjectId);
                    NewNode = true;
                    PerceivedObjects.Add(Node);
                }
                else
                {
                    return;
                }
            }

            if (Heard)
            {
                Node.Heard = true;
                OnPerceptionSeenObject(PerceivedObjectId, NewNode);
            }

            if (Seen)
            {
                Node.Seen = true;
                OnPerceptionHeardObject(PerceivedObjectId, NewNode);
            }

            if (Vanished)
            {
                Node.Seen = false;

                if (!Node.Heard)
                {
                    RemoveNode = true;
                    PerceivedObjects.Remove(Node);
                }

                OnPerceptionLostSightObject(PerceivedObjectId, RemoveNode);
            }

            if (Inaudible)
            {
                Node.Heard = false;

                if (!Node.Seen)
                {
                    RemoveNode = true;
                    PerceivedObjects.Remove(Node);
                }

                OnPerceptionLostHearingObject(PerceivedObjectId, RemoveNode);
            }
        }

        /// <summary>
        /// Called when an object first sees an object.
        /// </summary>
        /// <param name="PerceivedObjectId">Supplies the perceived object id.
        /// </param>
        /// <param name="InitialDetection">True if this is the first detection
        /// event for the perceived object since it last became undetected.
        /// </param>
        private void OnPerceptionSeenObject(uint PerceivedObjectId,
            bool InitialDetection)
        {
            CreatureObject SeenObject = Server.ObjectManager.GetCreatureObject(PerceivedObjectId);

//===== If we just started seeing this person again, we need to process memberships. ====//
            if (InitialDetection)
            {
                int nReputation = Script.GetReputation(this.ObjectId, PerceivedObjectId);

                if (nReputation < 11)
                {
                    if (Party.EnemiesLost.Contains(SeenObject))
                    {
                        Party.EnemiesLost.Remove(SeenObject);
                        Party.AddPartyEnemy(SeenObject);
                    }
                }
                else if (Script.GetFactionEqual(this.ObjectId, PerceivedObjectId) == CLRScriptBase.TRUE)
                {
                    // Might as well try to recover if we've messed up party memberships.
                    if (SeenObject.Party == null && Script.GetIsPC(SeenObject.ObjectId) == CLRScriptBase.FALSE)
                        SeenObject.Party.AddPartyMember(SeenObject);
                }
            }
        }

        /// <summary>
        /// Called when an object first hears an object.
        /// </summary>
        /// <param name="PerceivedObjectId">Supplies the perceived object id.
        /// </param>
        /// <param name="InitialDetection">True if this is the first detection
        /// event for the perceived object since it last became undetected.
        /// </param>
        private void OnPerceptionHeardObject(uint PerceivedObjectId,
            bool InitialDetection)
        {
            CreatureObject SeenObject = Server.ObjectManager.GetCreatureObject(PerceivedObjectId);

//===== If we just started hearing this person again, we need to process memberships. ====//
            if (InitialDetection)
            {
                int nReputation = Script.GetReputation(this.ObjectId, PerceivedObjectId);

                if (nReputation < 11)
                {
                    if (Party.EnemiesLost.Contains(SeenObject))
                    {
                        Party.EnemiesLost.Remove(SeenObject);
                        Party.AddPartyEnemy(SeenObject);
                    }
                }
                else if (Script.GetFactionEqual(this.ObjectId, PerceivedObjectId) == CLRScriptBase.TRUE)
                {
                    // Might as well try to recover if we've messed up party memberships.
                    if (SeenObject.Party == null && Script.GetIsPC(SeenObject.ObjectId) == CLRScriptBase.FALSE)
                        SeenObject.Party.AddPartyMember(SeenObject);
                }
            }      
        }

        /// <summary>
        /// Called when an object loses sight of another object.
        /// </summary>
        /// <param name="PerceivedObjectId">Supplies the perceived object id.
        /// </param>
        /// <param name="LastDetection">True if this is the last detection
        /// method for the perceived object.
        /// </param>
        private void OnPerceptionLostSightObject(uint PerceivedObjectId,
            bool LastDetection)
        {
            CreatureObject SeenObject = Server.ObjectManager.GetCreatureObject(PerceivedObjectId);

//===== If we've actually lost this creature, we need to populate missing lists. ====//
            if (LastDetection)
            {
                int nReputation = Script.GetReputation(this.ObjectId, PerceivedObjectId);

                if (nReputation < 11)
                {
                    if (!Party.EnemiesLost.Contains(SeenObject))
                    {
                        if (!Party.CanPartyHear(SeenObject) && !Party.CanPartySee(SeenObject))
                        {
                            Party.RemovePartyEnemy(SeenObject);
                            Party.EnemiesLost.Add(SeenObject);
                        }
                    }
                }
            }          
        }

        /// <summary>
        /// Called when an object loses hearing of another object.
        /// </summary>
        /// <param name="PerceivedObjectId">Supplies the perceived object id.
        /// </param>
        /// <param name="LastDetection">True if this is the last detection
        /// method for the perceived object.
        /// </param>
        private void OnPerceptionLostHearingObject(uint PerceivedObjectId,
            bool LastDetection)
        {
            CreatureObject SeenObject = Server.ObjectManager.GetCreatureObject(PerceivedObjectId);

            //===== If we've actually lost this creature, we need to populate missing lists. ====//
            if (LastDetection)
            {
                int nReputation = Script.GetReputation(this.ObjectId, PerceivedObjectId);

                if (nReputation < 11)
                {
                    if (!Party.EnemiesLost.Contains(SeenObject))
                    {
                        if (!Party.CanPartyHear(SeenObject) && !Party.CanPartySee(SeenObject))
                        {
                            Party.RemovePartyEnemy(SeenObject);
                            Party.EnemiesLost.Add(SeenObject);
                        }
                    }
                }
            }        
        }

        /// <summary>
        /// Called at the end of a combat round, when direction for the next round of combat 
        /// is necessary. This managed the bulk of action in combat.
        /// </summary>
        public void OnEndCombatRound()
        {
            // If this event has fired, we assume that the creature is in combat.
            HasCombatRoundProcess = true;
            UsingEndCombatRound = true;
            SelectCombatRoundAction();
        }

        /// <summary>
        /// Called when someone attempts to open a static conversation with the creature.
        /// </summary>
        public void OnConversation()
        {
            Script.BeginConversation("", CLRScriptBase.OBJECT_INVALID, CLRScriptBase.FALSE);
        }

        /// <summary>
        /// Called when a creature picks up or loses an item.
        /// </summary>
        public void OnInventoryDisturbed()
        {

        }

        /// <summary>
        /// Called approximately every six seconds on an NPC, regardless of whether or not the NPC is in combat.
        /// </summary>
        public void OnHeartbeat()
        {
            if (HasCombatRoundProcess == true &&
                UsingEndCombatRound == true)
            {
                // We're in a fight, but End Combat Round is firing, so we don't want to do anything here. Overloading
                // with commands is what causes the stutter walk.
                return;
            }
            if (HasCombatRoundProcess == true &&
               UsingEndCombatRound == false)
            {
                // In this state, we've decided that the creature would like to start a fight, but combat rounds
                // haven't started yet. We'll start the fight, and update once the creature gets there.
                SelectCombatRoundAction();
                return;
            }
            if (HasCombatRoundProcess == false &&
                UsingEndCombatRound == false)
            {
                // We're not in a fight, but maybe our leader is.
                if (this.Party.PartyLeader.HasCombatRoundProcess)
                {
                    // And nearby-- we should go find him.
                    if (this.Area == Party.PartyLeader.Area)
                        Script.ActionMoveToObject(Party.PartyLeader.ObjectId, CLRScriptBase.TRUE, 1.0f);

                    // Seems Glorious Leader isn't nearby. Bordering area, maybe?
                    else
                    {
                        foreach (AreaObject.AreaTransition Transition in Area.AreaTransitions)
                        {
                            if (Transition.TargetArea == PartyLeader.Area)
                            {
                                Script.ActionInteractObject(Transition.ObjectId);
                            }
                        }
                    }
                }
                
                // Guess no one we know is in trouble. Carry on.
                else _AmbientBehavior();
            }
        }

        /// <summary>
        /// Called after a creature rests.
        /// </summary>
        public void OnRested()
        {

        }

        /// <summary>
        /// Called whenever a builder invokes an event by script.
        /// </summary>
        /// <param name="UserDefinedEventNumber">Supplies the user-defined
        /// event number associated with the event request.</param>
        public void OnUserDefined(int UserDefinedEventNumber)
        {

        }

        /// <summary>
        /// This function is the primary means by which actions are selected for a given combat round.
        /// </summary>
        public void SelectCombatRoundAction()
        {
            RefreshNegativeStatuses();

            // Can't do anything. Might as well give up early and pray there's help on the way.
            if (!CanAct())
                return;

            // We're mindless, and thus too stupid to do anything special. Hit the nearest bad guy.
            if (TacticsType == (int)AIParty.AIType.BEHAVIOR_TYPE_MINDLESS)
            {
                CreatureObject AttackTarget = Party.GetNearest(this, Party.Enemies);
                Script.ActionAttack(AttackTarget.ObjectId, CLRScriptBase.FALSE);
                return;
            }

            int nWellBeing = Script.GetCurrentHitPoints(ObjectId) * 100 / Script.GetMaxHitPoints(ObjectId);
            int nMissingHitPoints = Script.GetCurrentHitPoints(ObjectId) - Script.GetMaxHitPoints(ObjectId);

            // Are we in critical condition?
            if (nWellBeing < 10)
            {
                CreatureObject Healer = Party.GetNearest(this, Party.PartyMedics);
                if (Healer == null)
                    Healer = Party.GetNearest(this, Party.PartyBuffs);
                if (Healer != null && Healer != this)
                {
                    if(Script.GetDistanceBetween(Healer.ObjectId, this.ObjectId) > 5.0f)
                        Script.ActionMoveToObject(Healer.ObjectId, CLRScriptBase.TRUE, 1.0f);
                    if(TryToHeal(ObjectId, nMissingHitPoints))
                        return;
                }
                else if (Party.PartyLeader != null && Party.PartyLeader != this)
                {
                    Healer = Party.PartyLeader;
                    if (Script.GetDistanceBetween(Healer.ObjectId, this.ObjectId) > 5.0f)
                        Script.ActionMoveToObject(Healer.ObjectId, CLRScriptBase.TRUE, 1.0f);
                    if(TryToHeal(ObjectId, nMissingHitPoints))
                        return;
                }
                else
                {
                    if (TryToHeal(ObjectId, nMissingHitPoints))
                        return;
                }

                if (TryToHeal(ObjectId, nMissingHitPoints)) return;
            }

            // Bad enough to self heal?
            else if (nWellBeing < 50)
            {
                if (TryToHeal(ObjectId, nMissingHitPoints))
                    return;
            }
        }

        /// <summary>
        /// This will search for an ability that will directly restore about HitPoints of hit points and use it on HealTarget
        /// </summary>
        /// <param name="HealTarget">The one to receive healing.</param>
        /// <param name="HitPoints">The amount to heal.</param>
        /// <returns>True if an ability is found.</returns>
        public bool TryToHeal(uint HealTarget, int HitPoints)
        {
            int SpellId = -1;
            NWTalent Healing = null;
            if (HitPoints > 50 && Script.GetHasSpell(CLRScriptBase.SPELL_HEAL, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_HEAL;
            else if(HitPoints > 50 && Script.GetHasSpell(CLRScriptBase.SPELL_MASS_HEAL, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_MASS_HEAL;
            else if (HitPoints > 21 && Script.GetHasSpell(CLRScriptBase.SPELL_CURE_CRITICAL_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_CURE_CRITICAL_WOUNDS;
            else if (HitPoints > 21 && Script.GetHasSpell(CLRScriptBase.SPELL_MASS_CURE_CRITICAL_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_MASS_CURE_CRITICAL_WOUNDS;
            else if (HitPoints > 16 && Script.GetHasSpell(CLRScriptBase.SPELL_CURE_SERIOUS_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_CURE_SERIOUS_WOUNDS;
            else if (HitPoints > 16 && Script.GetHasSpell(CLRScriptBase.SPELL_MASS_CURE_SERIOUS_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_MASS_CURE_SERIOUS_WOUNDS;
            else if (HitPoints > 10 && Script.GetHasSpell(CLRScriptBase.SPELL_CURE_MODERATE_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_CURE_MODERATE_WOUNDS;
            else if (HitPoints > 10 && Script.GetHasSpell(CLRScriptBase.SPELL_MASS_CURE_MODERATE_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_MASS_CURE_MODERATE_WOUNDS;
            else if (Script.GetHasSpell(CLRScriptBase.SPELL_CURE_LIGHT_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_CURE_LIGHT_WOUNDS;
            else if (Script.GetHasSpell(CLRScriptBase.SPELL_MASS_CURE_LIGHT_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_MASS_CURE_LIGHT_WOUNDS;
            else if (Script.GetHasSpell(CLRScriptBase.SPELL_CURE_MODERATE_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_CURE_MODERATE_WOUNDS;
            else if (Script.GetHasSpell(CLRScriptBase.SPELL_CURE_SERIOUS_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_CURE_SERIOUS_WOUNDS;
            else if (Script.GetHasSpell(CLRScriptBase.SPELL_CURE_CRITICAL_WOUNDS, ObjectId) == CLRScriptBase.TRUE) SpellId = CLRScriptBase.SPELL_CURE_CRITICAL_WOUNDS;

            if (SpellId != -1)
            {
                Healing = Script.TalentSpell(SpellId);
                Script.ActionUseTalentOnObject(Healing, HealTarget);
                return true;
            }
            else
            {
                if (HealTarget == ObjectId)
                {
                    Healing = Script.GetCreatureTalentBest(CLRScriptBase.TALENT_CATEGORY_BENEFICIAL_HEALING_POTION, 20, ObjectId, 0);
                    if (Script.GetIsTalentValid(Healing) == CLRScriptBase.TRUE)
                    {
                        Script.ActionUseTalentOnObject(Healing, HealTarget);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// This refreshes the public variables which track status afflictions on the creature.
        /// </summary>
        public void RefreshNegativeStatuses()
        {
            foreach (NWEffect Effect in Script.GetObjectEffects(this.ObjectId))
            {
                int nEffectType = Script.GetEffectType(Effect);
                int nEffectSubType = Script.GetEffectSubType(Effect);
                if (nEffectType == CLRScriptBase.EFFECT_TYPE_ABILITY_DECREASE)
                {
                    if (nEffectSubType == CLRScriptBase.SUBTYPE_MAGICAL || nEffectSubType == CLRScriptBase.SUBTYPE_EXTRAORDINARY)
                        AbilityDamaged = true;
                    else
                        AbilityDrained = true;
                }

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_BLINDNESS)
                    Blinded = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_CHARMED)
                    Charmed = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_CONFUSED)
                    Confused = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_CURSE)
                    Cursed = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_DARKNESS)
                    Darkness = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_DAZED)
                    Dazed = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_DEAF)
                    Deaf = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_DISEASE)
                    Diseased = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_DOMINATED)
                    Dominated = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_ENTANGLE)
                    Entangled = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_FRIGHTENED)
                    Frightened = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_INSANE)
                    Insane = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_MOVEMENT_SPEED_DECREASE)
                    MoveDown = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_NEGATIVELEVEL)
                    LevelDrain = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_PARALYZE)
                    Paralyzed = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_PETRIFY)
                    Petrified = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_POISON)
                    Poisoned = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_SILENCE)
                    Silenced = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_SLEEP)
                    Sleeping = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_SLOW)
                    Slowed = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_STUNNED)
                    Stunned = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_TURNED)
                    Turned = true;

                else if (nEffectType == CLRScriptBase.EFFECT_TYPE_WOUNDING)
                    Wounded = true;

            }
        }

        /// <summary>
        /// This determines if the creature has any negative status effects which prevent actions in combat.
        /// </summary>
        /// <returns>false if the creature is incapable of acting</returns>
        public bool CanAct()
        {
            if (Charmed) return false;
            if (Confused) return false;
            if (Dominated) return false;
            if (Frightened) return false;
            if (Insane) return false;
            if (Paralyzed) return false;
            if (Petrified) return false;
            if (Sleeping) return false;
            if (Stunned) return false;
            if (Turned) return false;
            return true;
        }

        /// <summary>
        /// This orders this creature to perform whatever it's supposed to perform when not engaged in something
        /// else.
        /// </summary>
        private void _AmbientBehavior()
        {

        }

        /// <summary>
        /// This orders Creature to bash Target until it is destroyed.
        /// </summary>
        /// <param name="Target">The target to be bashed</param>
        private void _BashObject(uint Target)
        {
            OverrideTarget = Target;
            Script.ActionAttack(Target, CLRScriptBase.FALSE);
        }

        /// <summary>
        /// This function causes Creature to switch to change to ranged weapons temporarily. It should be used when
        /// ranged combat is only a good idea for a moment, because of battlefield conditions.
        /// </summary>
        /// <param name="Reason">The object we should wait to move or go away before changing back</param>
        /// <param name="WaitForDeath">If set to false, the creature will wait until Reason is destroyed before switching back to melee</param>
        private void _TemporaryRangedCombat(uint Reason, bool WaitForDeath = false)
        {

        }

        /// <summary>
        /// This function causes Creature1 and Creature2 to be flagged as temporary enemies of one another, with no specified decay
        /// time. They, thus, will fight until killed or separated, but will not break the rest of their factions.
        /// </summary>
        /// <param name="Creature1"></param>
        /// <param name="Creature2"></param>
        private void _SetMutualEnemies(uint Creature1, uint Creature2)
        {
            Script.SetIsTemporaryEnemy(Creature1, Creature2, CLRScriptBase.FALSE, 0.0f);
            Script.SetIsTemporaryEnemy(Creature2, Creature1, CLRScriptBase.FALSE, 0.0f);
        }

        /// <summary>
        /// This function attempts to determine if the spell just cast on this creature was plausibly friendly fire, based on the
        /// locations of nearby enemies.
        /// </summary>
        /// <param name="nTargetArea">Spell Target AOE as defined in spells.2da in the targeting UI</param>
        /// <returns></returns>
        private bool _IsFriendlyFire(int nTargetArea)
        {
            // Check for friendly fire.
            switch ((SpellTargetAOE)nTargetArea)
            {
                case SpellTargetAOE.SPELL_TARGET_COLOSSAL_AOE:
                    // With a colossal AOE, no sense in checking. That as probably friendly fire.
                    return true;
                case SpellTargetAOE.SPELL_TARGET_HUGE_AOE:
                case SpellTargetAOE.SPELL_TARGET_HUGE_AOE_A:
                case SpellTargetAOE.SPELL_TARGET_HUGE_AOE_B:
                    {
                        foreach (uint TargetId in Script.GetObjectsInShape(CLRScriptBase.SHAPE_SPHERE, CLRScriptBase.RADIUS_SIZE_HUGE * 2.0f, Script.GetLocation(this.ObjectId), false, CLRScriptBase.OBJECT_TYPE_CREATURE, Script.Vector(0.0f, 0.0f, 0.0f)))
                        {
                            // We found an enemy in sight; this is probably friendly fire.
                            if (Script.GetReputation(this.ObjectId, TargetId) < 11)
                                return true;
                        }
                    }
                    break;
                case SpellTargetAOE.SPELL_TARGET_LARGE_AOE:
                case SpellTargetAOE.SPELL_TARGET_PURPLE_LARGE:
                case SpellTargetAOE.SPELL_TARGET_LINE:
                    {
                        foreach (uint TargetId in Script.GetObjectsInShape(CLRScriptBase.SHAPE_SPHERE, CLRScriptBase.RADIUS_SIZE_LARGE * 2.0f, Script.GetLocation(this.ObjectId), false, CLRScriptBase.OBJECT_TYPE_CREATURE, Script.Vector(0.0f, 0.0f, 0.0f)))
                        {
                            // We found an enemy nearby; this is probably friendly fire.
                            if (Script.GetReputation(this.ObjectId, TargetId) < 11)
                                return true;
                        }
                    }
                    break;
                case SpellTargetAOE.SPELL_TARGET_PURPLE_MEDIUM:
                case SpellTargetAOE.SPELL_TARGET_RECTANGLE_A:
                case SpellTargetAOE.SPELL_TARGET_RECTANGLE_B:
                    {
                        foreach (uint TargetId in Script.GetObjectsInShape(CLRScriptBase.SHAPE_SPHERE, CLRScriptBase.RADIUS_SIZE_MEDIUM * 2.0f, Script.GetLocation(this.ObjectId), false, CLRScriptBase.OBJECT_TYPE_CREATURE, Script.Vector(0.0f, 0.0f, 0.0f)))
                        {
                            // We found an enemy nearby; this is probably friendly fire.
                            if (Script.GetReputation(this.ObjectId, TargetId) < 11)
                                return true;
                        }
                    }
                    break;
                case SpellTargetAOE.SPELL_TARGET_PURPLE_SMALL:
                case SpellTargetAOE.SPELL_TARGET_SHORT_CONE_A:
                case SpellTargetAOE.SPELL_TARGET_SHORT_CONE_B:
                case SpellTargetAOE.SPELL_TARGET_SHORT_CONE_C:
                case SpellTargetAOE.SPELL_TARGET_SHORT_CONE_D:
                    {
                        foreach (uint TargetId in Script.GetObjectsInShape(CLRScriptBase.SHAPE_SPHERE, CLRScriptBase.RADIUS_SIZE_SMALL * 2.0f, Script.GetLocation(this.ObjectId), false, CLRScriptBase.OBJECT_TYPE_CREATURE, Script.Vector(0.0f, 0.0f, 0.0f)))
                        {
                            // We found an enemy nearby; this is probably friendly fire.
                            if (Script.GetReputation(this.ObjectId, TargetId) < 11)
                                return true;
                        }
                    }
                    break;
            }
            return false;
        }

        /// <summary>
        /// Determines if Creature has effects which would typically cause targeting problems, and might
        ///  result in attacking a friendly person.
        /// </summary>
        /// <param name="CreatureId"></param>
        /// <returns></returns>
        private bool _IsMindMagiced(uint CreatureId)
        {
            foreach (NWEffect eEffect in Script.GetObjectEffects(CreatureId))
            {
                if (Script.GetEffectType(eEffect) == CLRScriptBase.EFFECT_TYPE_BLINDNESS ||
                    Script.GetEffectType(eEffect) == CLRScriptBase.EFFECT_TYPE_CHARMED ||
                    Script.GetEffectType(eEffect) == CLRScriptBase.EFFECT_TYPE_CONFUSED ||
                    Script.GetEffectType(eEffect) == CLRScriptBase.EFFECT_TYPE_DOMINATED ||
                    Script.GetEffectType(eEffect) == CLRScriptBase.EFFECT_TYPE_INSANE)
                {
                    // These are all obvious sources of misbehavior.
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Called when the creature is attacked.
        /// </summary>
        /// <param name="AttackerObjectId">Supplies the attacker object id.
        /// </param>
        public void OnAttacked(uint AttackerObjectId)
        {
            if (!IsAIControlled)
                return;

            uint Attacker = Script.GetLastAttacker(this.ObjectId);

            // Attacker is under the influence of mind-affecting stuff; we don't need to get angry.
            if (_IsMindMagiced(Attacker))
                return;

            // This creature is under the influence of mind-affecting stuff. It isn't cognizant enough to be angry.
            if (_IsMindMagiced(this.ObjectId))
                return;
        }


        /// <summary>
        /// This contains whether a target is charmed, and thus is unwilling to fight enemies
        /// </summary>
        public bool Charmed = false;

        /// <summary>
        /// This contains whether a target is confused, and thus acting randomly.
        /// </summary>
        public bool Confused = false;

        /// <summary>
        /// This contains whether a target is cursed, and in need of a remove curse spell.
        /// </summary>
        public bool Cursed = false;

        /// <summary>
        /// This contains whether a target is afflicted by a Darkness spell, and thus needs
        /// true seeing or to move.
        /// </summary>
        public bool Darkness = false;

        /// <summary>
        /// This contains whether a target is dazed, and thus incapable of fighting.
        /// </summary>
        public bool Dazed = false;

        /// <summary>
        /// This contains whether a target is deaf, and in need of healing.
        /// </summary>
        public bool Deaf = false;

        /// <summary>
        /// This contains whether a target is diseased.
        /// </summary>
        public bool Diseased = false;

        /// <summary>
        /// This contains whether a target is following the orders of an enemy spellcaster.
        /// </summary>
        public bool Dominated = false;

        /// <summary>
        /// This contains whether a target has been trapped by webs, ropes, or the like.
        /// </summary>
        public bool Entangled = false;

        /// <summary>
        /// This contains whether a target is "frightened" by the NWN2 engine, which is mechanically
        /// "panicked"
        /// </summary>
        public bool Frightened = false;

        /// <summary>
        /// This contains whether a target is permanently confused, and thus in desparate need of healing.
        /// </summary>
        public bool Insane = false;

        /// <summary>
        /// This contains whether some effect is reducing the creature's movement speed.
        /// </summary>
        public bool MoveDown = false;

        /// <summary>
        /// This contains whether a target has negative levels.
        /// </summary>
        public bool LevelDrain = false;

        /// <summary>
        /// This contains whether a target is paralyzed.
        /// </summary>
        public bool Paralyzed = false;

        /// <summary>
        /// This contains whether a target has been turned to stone.
        /// </summary>
        public bool Petrified = false;

        /// <summary>
        /// This contains whether a target has been poisoned.
        /// </summary>
        public bool Poisoned = false;

        /// <summary>
        /// This contains whether a target has been silenced.
        /// </summary>
        public bool Silenced = false;

        /// <summary>
        /// This contains whether a target is sleeping.
        /// </summary>
        public bool Sleeping = false;

        /// <summary>
        /// This contains whether a target is slowed.
        /// </summary>
        public bool Slowed = false;

        /// <summary>
        /// This contains whether a target is stunned.
        /// </summary>
        public bool Stunned = false;

        /// <summary>
        /// This contains whether a target has been turned.
        /// </summary>
        public bool Turned = false;

        /// <summary>
        /// This contains whether a target is bleeding, and in need of a cure spell.
        /// </summary>
        public bool Wounded = false;

        /// <summary>
        /// This contains whether or not this creature is blinded, and thus needs a remove blindness
        /// spell.
        /// </summary>
        public bool Blinded = false;

        /// <summary>
        /// This contains whether or not this creature has ability drain, and thus needs help from a
        /// restoration or greater restoration.
        /// </summary>
        public bool AbilityDrained = false;

        /// <summary>
        /// This contains whether or not this creature has ability damage, and thus needs help from a 
        /// lesser restoration, restoration, or greater restoration.
        /// </summary>
        public bool AbilityDamaged = false;

        /// <summary>
        /// This contains whether or not this creature has an active cycle of combat round processing.
        /// </summary>
        public bool HasCombatRoundProcess = false;

        /// <summary>
        /// This contains whether or not EndCombatRound has begun firing for the NPC.
        /// </summary>
        public bool UsingEndCombatRound = false;

        /// <summary>
        /// Get whether the creature is a player avatar (includes DMs).
        /// </summary>
        public bool IsPC { get { return CreatureIsPC; } }

        /// <summary>
        /// Get whether the creature is a DM avatar.
        /// </summary>
        public bool IsDM { get { return CreatureIsDM; } }

        /// <summary>
        /// Get whether a player or DM is controlling the creature.
        /// </summary>
        public bool IsPlayerControlled { get { return Script.GetControlledCharacter(ObjectId) != OBJECT_INVALID; } }

        /// <summary>
        /// Get or set whether the object is managed by the AI subsystem.  This
        /// should be only set at startup time for the object, generally.
        /// </summary>
        public bool IsAIControlled { get { return AIControlled && !IsPlayerControlled; } set { AIControlled = value; } }

        /// <summary>
        /// The list of perceived objects.
        /// </summary>
        public List<PerceptionNode> PerceivedObjects { get; set; }

        /// <summary>
        /// The associated AI party, if any.
        /// </summary>
        public AIParty Party { get; set; }

        /// <summary>
        /// The leader of the party, if any.
        /// </summary>
        public CreatureObject PartyLeader
        {
            get
            {
                if (Party == null)
                    return null;
                else
                    return Party.PartyLeader;
            }
        }



        /// <summary>
        /// The creature is PC flag (DMs are also PCs).
        /// </summary>
        private bool CreatureIsPC;

        /// <summary>
        /// The creature is DM flag.
        /// </summary>
        private bool CreatureIsDM;

        /// <summary>
        /// True if the object is controlled by the AI subsystem.
        /// </summary>
        private bool AIControlled;


        /// <summary>
        /// This class records what objects are perceived by this object.
        /// </summary>
        public class PerceptionNode
        {
            /// <summary>
            /// Create a new, blank perception node relating to perception of a
            /// given object.
            /// </summary>
            /// <param name="ObjectId">Supplies the object whose perception
            /// state is being tracked.</param>
            public PerceptionNode(uint ObjectId)
            {
                PerceivedObjectId = ObjectId;
            }

            /// <summary>
            /// The object id that is being perceived.
            /// </summary>
            public uint PerceivedObjectId;

            /// <summary>
            /// True if the object is seen.
            /// </summary>
            public bool Seen;

            /// <summary>
            /// True if the object is heard.
            /// </summary>
            public bool Heard;
        }

        public uint OverrideTarget = CLRScriptBase.OBJECT_INVALID;

        public int TacticsType = (int)AIParty.AIType.BEHAVIOR_TYPE_UNDEFINED;

        public enum SpellTargetAOE
        {
            SPELL_TARGET_SINGLE = 0,
            SPELL_TARGET_RECTANGLE_A = 1,
            SPELL_TARGET_HUGE_AOE_A = 2,
            SPELL_TARGET_SHORT_CONE_A = 3,
            SPELL_TARGET_SHORT_CONE_B = 4,
            SPELL_TARGET_SHORT_CONE_C = 5,
            SPELL_TARGET_POINT = 6,
            SPELL_TARGET_HUGE_AOE_B = 7,
            SPELL_TARGET_LARGE_AOE = 8,
            SPELL_TARGET_HUGE_AOE = 9,
            SPELL_TARGET_COLOSSAL_AOE = 10,
            SPELL_TARGET_RECTANGLE_B = 11,
            SPELL_TARGET_LINE = 12,
            SPELL_TARGET_PURPLE_SMALL = 13,
            SPELL_TARGET_PURPLE_MEDIUM = 14,
            SPELL_TARGET_PURPLE_LARGE = 15,
            SPELL_TARGET_SHORT_CONE_D = 16,
        }
        
        const string ON_SPAWN_EVENT = "ACR_VFX_ON_SPAWN";
        const string EFFECT_VISUAL = "_EFFECT_VISUAL";
        const string EFFECT_PHYSICAL = "_EFFECT_PHYSICAL";
        const string EFFECT_DAMAGE = "_EFFECT_DAMAGE";
    }
}
