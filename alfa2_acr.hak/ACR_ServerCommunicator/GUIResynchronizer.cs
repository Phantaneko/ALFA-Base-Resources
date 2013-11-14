﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALFA;
using NWScript;

namespace ACR_ServerCommunicator
{
    /// <summary>
    /// This object manages GUI element resynchronization for players that
	 /// enter the server from a server to server portal.
    /// </summary>
    internal static class GUIResynchronizer
    {

        /// <summary>
        /// Resync state for a player.
        /// </summary>
        private class ResyncState
        {
            /// <summary>
            /// Construct a new ResyncState object.
            /// </summary>
            /// <param name="PlayerId">Supplies the player id.</param>
            /// <param name="ResyncFlags">Supplies the resync flags.</param>
            public ResyncState(int PlayerId, uint ResyncFlags)
            {
                this.PlayerId = PlayerId;
                this.ResyncFlags = ResyncFlags;
            }

            /// <summary>
            /// The player id of the player.
            /// </summary>
            public int PlayerId;

            /// <summary>
            /// The resync flags for the request.
            /// </summary>
            public uint ResyncFlags;

            /// <summary>
            /// Convert the resynchronization state to a string.
            /// </summary>
            /// <returns>The serialized string version of the state.</returns>
            public override string ToString()
            {
                return String.Format("{0}:{1}", PlayerId, ResyncFlags);
            }

            /// <summary>
            /// Deserialize the resynchronization state from a string.
            /// </summary>
            /// <param name="ResyncCommand">Supplies the state string that was
            /// constructed by a call to ToString().</param>
            /// <returns>A new ResyncState object containing the deserialized
            /// contents, else null on failure.  An exception may also be
            /// raised on a failure that is the result of an unexpected
            /// protocol violation.</returns>
            public static ResyncState FromString(string ResyncCommand)
            {
                string[] Tokens = ResyncCommand.Split(new char[] { ':' });

                if (Tokens == null || Tokens.Length < 1)
                    return null;

                ResyncState State = new ResyncState(Convert.ToInt32(Tokens[0]), 0);

                if (Tokens.Length >= 1)
                    State.ResyncFlags = Convert.ToUInt32(Tokens[1]);

                return State;
            }
        };

        /// <summary>
        /// This method is called when a cross-server chat select
        /// resynchronization request is received.  Its purpose is to scan the
        /// player list to check whether the argument player is online, and to
        /// activate chat select GUI resynchronization if appropriate, else to
        /// queue the resynchronization until the player does log in.
        /// </summary>
        /// <param name="SourceServerId">Supplies the server id of the server
        /// that requested the GUI resynchronization.</param>
        /// <param name="ResyncCommand">Supplies the GUI resynchronizer command
        /// line as generated by SendGUIStateToServer().</param>
        /// <param name="Script">Supplies the current script object.</param>
        /// <returns>TRUE on success, else FALSE on failure.</returns>
        public static int HandleGUIResync(int SourceServerId, string ResyncCommand, ACR_ServerCommunicator Script)
        {
            try
            {
                ResyncState RemoteResyncInfo;

                //
                // Deserialize the remote resynchronization state.  On null, an
                // obviously invalid request was received.  Otherwise a protocol
                // violation that is atypical was received and an exception is
                // raised.
                //

                if ((RemoteResyncInfo = ResyncState.FromString(ResyncCommand)) == null)
                {
                    Script.WriteTimestampedLogEntry(String.Format(
                        "ACR_ServerCommunicator.GUIResynchronizer.HandleGUIResync({0}, {1}): Invalid request.",
                        SourceServerId,
                        ResyncCommand));
                    return ACR_ServerCommunicator.FALSE;
                }

                //
                // If a request was not already enqueued for this player, then
                // enqueue it.
                //

                var ResyncInfo = (from RS in PlayerResyncStates
                                  where RS.PlayerId == RemoteResyncInfo.PlayerId
                                  select RS).FirstOrDefault();

                if (ResyncInfo == null)
                {
                    //
                    // If the player is logged on, directly enqueue the execute
                    // request now.  Otherwise, wait for the ClientEnter event
                    // as the player might still reside on the remote server.
                    //

                    foreach (uint PCObject in Script.GetPlayers(true))
                    {
                        PlayerState State;

                        if ((State = Script.TryGetPlayerState(PCObject)) == null)
                            continue;

                        if (State.PlayerId != RemoteResyncInfo.PlayerId)
                            continue;

                        ResynchronizePlayerState(RemoteResyncInfo, PCObject, Script);

                        return ACR_ServerCommunicator.TRUE;
                    }

                    //
                    // Enqueue the request so that it can be processed at
                    // ClientEnter time.
                    //

                    PlayerResyncStates.Add(RemoteResyncInfo);
                }
                else
                {
                    //
                    // Update the resync flags to match the new request.
                    //

                    ResyncInfo.ResyncFlags = RemoteResyncInfo.ResyncFlags;
                }
            }
            catch (Exception e)
            {
                Script.WriteTimestampedLogEntry(String.Format(
                    "ACR_ServerCommunicator.GUIResynchronizer.HandleGUIResync({0}, {1}): Exception: {0}",
                    SourceServerId,
                    ResyncCommand,
                    e));
                return ACR_ServerCommunicator.FALSE;
            }

            return ACR_ServerCommunicator.TRUE;
        }

        /// <summary>
        /// This method is called when the ClientEnter event fires for a
        /// player.  Its purpose is to check whether the player has a pending
        /// resync request, and, if so, to execute the resync operation.
        /// </summary>
        /// <param name="State">Supplies the player object of the incoming
        /// player.</param>
        /// <param name="Script">Supplies the script object.</param>
        public static void OnClientEnter(uint PCObject, ACR_ServerCommunicator Script)
        {
            PlayerState State = Script.TryGetPlayerState(PCObject);

            if (State == null)
                return;

            var ResyncInfo = (from RS in PlayerResyncStates
                              where RS.PlayerId == State.PlayerId
                              select RS).FirstOrDefault();

            if (ResyncInfo == null)
                return;

            //
            // Dequeue the resync state and start attempting to apply it to the
            // player object once the player has loaded.
            //

            PlayerResyncStates.Remove(ResyncInfo);
            ResynchronizePlayerState(ResyncInfo, PCObject, Script);
        }

        /// <summary>
        /// This method is called when a portal transition has been committed
        /// to send a player to a remote server.
        /// </summary>
        /// <param name="State">Supplies the local state for the player that is
        /// committed to a portal transition.
        /// </param>
        /// <param name="Server">Supplies the destination server for the portal
        /// event.</param>
        /// <param name="Script">Supplies the script object.</param>
        public static void SendGUIStateToServer(PlayerState State, GameServer Server, ACR_ServerCommunicator Script)
        {
            //
            // Build the resynchronization command.  The resynchronization
            // command is parsed by HandleGUIResync().  Note that the remote
            // and local servers may not be of the same version, i.e. the
            // remote server may not understand fields that are created by the
            // local server if it is of a newer version.
            //

            ResyncState ResyncInfo = new ResyncState(State.PlayerId, 0);

            if (State.ChatSelectGUIExpanded)
                ResyncInfo.ResyncFlags |= RESYNC_FLAG_CHAT_SELECT_EXPANDED;

            //
            // Dispatch the resync script execution request to the remote
            // server.  The remote server will process it when the request has
            // been received.
            //

            Script.RunScriptOnServer(Server.ServerId, "acr_resync_gui", ResyncInfo.ToString());
        }

        /// <summary>
        /// This method is called to resynchronize the GUI state of a player
        /// that executed a server to server portal, if the player is in an
        /// area.
        /// </summary>
        /// <param name="ResyncInfo">Supplies the resync block for the
        /// player.  This contains the deserialized resynchronization command
        /// data.</param>
        /// <param name="PCObject">Supplies the player object id.</param>
        /// <param name="Script">Supplies the script object.</param>
        /// <param name="Tries">Supplies the count of retries.</param>
        private static void ResynchronizePlayerState(ResyncState ResyncInfo, uint PCObject, ACR_ServerCommunicator Script, int Tries = 0)
        {
            if (Script.GetIsObjectValid(PCObject) == ACR_ServerCommunicator.FALSE)
            {
                //
                // The player logged out while a resync request was pending.
                // Throw away the state as it is no longer needed on a full log
                // out and log in sequence.
                //

                return;
            }

            if ((Script.GetArea(PCObject) == ACR_ServerCommunicator.OBJECT_INVALID) ||
                (Script.GetScriptHidden(PCObject) != ACR_ServerCommunicator.FALSE))
            {
                //
                // The player may still be in transition or is not yet loaded.
                // Queue the request.
                //

                if (Tries < MAX_RESYNC_RETRIES)
                {
                    Script.DelayCommand(RESYNC_RETRY_INTERVAL, delegate()
                    {
                        ResynchronizePlayerState(ResyncInfo, PCObject, Script, Tries + 1);
                    });
                }

                return;
            }

            PlayerState State = Script.TryGetPlayerState(PCObject);

            if (State == null)
                return;

            //
            // Area transition has finished.  Apply the GUI state now.
            //

            State.ChatSelectGUIExpanded = ((ResyncInfo.ResyncFlags & RESYNC_FLAG_CHAT_SELECT_EXPANDED) != 0);
            State.UpdateChatSelectGUIHeaders();

            Script.SendMessageToPC(PCObject, "Server to server portal completed.");
            Script.WriteTimestampedLogEntry(String.Format(
                "ACR_ServerCommunicator.GUIResynchronizer.ResynchronizePlayerState: Resynchronized player GUI state for player {0} after server-to-server portal.",
                Script.GetName(PCObject)));
        }

        /// <summary>
        /// The list of player ids that need to be resynchronized.
        /// </summary>
        private static List<ResyncState> PlayerResyncStates = new List<ResyncState>();

        /// <summary>
        /// The interval between resync polling requests waiting for a player
        /// to complete area loading.
        /// </summary>
        private const float RESYNC_RETRY_INTERVAL = 3.0f;

        /// <summary>
        /// The maximum retries before a resync attempt is abandoned for a
        /// player id.
        /// </summary>
        private const int MAX_RESYNC_RETRIES = 100;

        /// <summary>
        /// The chat select GUI is expanded.
        /// </summary>
        private const uint RESYNC_FLAG_CHAT_SELECT_EXPANDED = 0x00000001;

    }
}
