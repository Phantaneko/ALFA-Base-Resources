﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ALFA.Shared
{
    /// <summary>
    /// This interface defines the contract between the information store
    /// module and other client modules.
    /// </summary>
    public interface IInformationStore : IBackgroundLoadedResource
    {
        Dictionary<string, ItemResource> ModuleItems { get; set; }
        Dictionary<string, CreatureResource> ModuleCreatures { get; set; }
        Dictionary<string, PlaceableResource> ModulePlaceables { get; set; }
        Dictionary<string, WaypointResource> ModuleWaypoints { get; set; }
        Dictionary<string, VisualEffectResource> ModuleVisualEffects { get; set; }
        Dictionary<int, Faction> ModuleFactions { get; set; }
    }
}
