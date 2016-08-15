﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EssentialsPlugin.NetworkHandlers
{
    using System.Reflection;
    using System.Timers;
    using Sandbox.Engine.Multiplayer;
    using Sandbox.Game.Entities;
    using Sandbox.Game.World;
    using Sandbox.ModAPI;
    using Settings;
    using SEModAPIInternal.API.Common;
    using SEModAPIInternal.API.Server;
    using Utility;
    using VRage.Game;
    using VRage.Library.Collections;
    using VRage.Network;
    using VRageMath;

    public class BuildBlockHandler : NetworkHandlerBase
    {
        private static Dictionary<string, bool> _unitTestResults = new Dictionary<string, bool>();
        private const string BuildBlockName = "BuildBlockRequest";
        private const string BuildBlocksName = "BuildBlocksRequest";
        private const string BuildAreaName = "BuildBlocksAreaRequest";
        public override bool CanHandle( CallSite site )
        {
            //okay, there's three distinct methods that build blocks, we need to handle all of them
            if ( site.MethodInfo.Name == BuildBlockName )
            {
                if ( !_unitTestResults.ContainsKey( BuildBlockName ) )
                {
                    //public void BuildBlockRequest(uint colorMaskHsv, MyBlockLocation location, [DynamicObjectBuilder] MyObjectBuilder_CubeBlock blockObjectBuilder, long builderEntityId, bool instantBuild, long ownerId)
                    var parameters = site.MethodInfo.GetParameters();
                    if ( parameters.Length != 6 )
                    {
                        _unitTestResults[BuildBlockName] = false;
                        return false;
                    }

                    if ( parameters[0].ParameterType != typeof(uint)
                         || parameters[1].ParameterType != typeof(MyCubeGrid.MyBlockLocation)
                         || parameters[2].ParameterType != typeof(MyObjectBuilder_CubeBlock)
                         || parameters[3].ParameterType != typeof(long)
                         || parameters[4].ParameterType != typeof(bool)
                         || parameters[5].ParameterType != typeof(long) )
                    {
                        _unitTestResults[BuildBlockName] = false;
                        return false;
                    }

                    _unitTestResults[BuildBlockName] = true;
                }

                return _unitTestResults[BuildBlockName];
            }
            else if (site.MethodInfo.Name == BuildBlocksName)
            {
                if (!_unitTestResults.ContainsKey(BuildBlocksName))
                {
                    //void BuildBlocksRequest(uint colorMaskHsv, HashSet<MyBlockLocation> locations, long builderEntityId, bool instantBuild, long ownerId)
                    var parameters = site.MethodInfo.GetParameters();
                    if (parameters.Length != 5)
                    {
                        _unitTestResults[BuildBlocksName] = false;
                        return false;
                    }

                    if (parameters[0].ParameterType != typeof(uint)
                         || parameters[1].ParameterType != typeof(HashSet<MyCubeGrid.MyBlockLocation>)
                         || parameters[2].ParameterType != typeof(long)
                         || parameters[3].ParameterType != typeof(bool)
                         || parameters[4].ParameterType != typeof(long))
                    {
                        _unitTestResults[BuildBlocksName] = false;
                        return false;
                    }

                    _unitTestResults[BuildBlocksName] = true;
                }

                return _unitTestResults[BuildBlocksName];
            }
            else if (site.MethodInfo.Name == BuildAreaName)
            {
                if (!_unitTestResults.ContainsKey(BuildAreaName))
                {
                    //void BuildBlocksRequest(uint colorMaskHsv, HashSet<MyBlockLocation> locations, long builderEntityId, bool instantBuild, long ownerId)
                    var parameters = site.MethodInfo.GetParameters();
                    if (parameters.Length != 5)
                    {
                        _unitTestResults[BuildAreaName] = false;
                        return false;
                    }

                    if ( parameters[0].ParameterType != typeof(HashSet<MyCubeGrid.MyBlockLocation>)
                         || parameters[1].ParameterType != typeof(long)
                         || parameters[2].ParameterType != typeof(bool)
                         || parameters[3].ParameterType != typeof(long))
                    {
                        _unitTestResults[BuildAreaName] = false;
                        return false;
                    }

                    _unitTestResults[BuildAreaName] = true;
                }

                return _unitTestResults[BuildAreaName];
            }
            return false;
        }

        Timer _kickTimer = new Timer(30000);
        public override bool Handle( ulong remoteUserId, CallSite site, BitStream stream, object obj )
        {
            if ( !PluginSettings.Instance.ProtectedEnabled )
                return false;

            var grid = obj as MyCubeGrid;
            if ( grid == null )
            {
                Essentials.Log.Debug( "Null grid in BuildBlockHandler" );
                return false;
            }

            bool found = false;
            foreach ( var item in PluginSettings.Instance.ProtectedItems )
            {
                if ( !item.Enabled )
                    continue;

                if ( item.EntityId != grid.EntityId )
                    continue;

                if(!item.ProtectionSettingsDict.Dictionary.ContainsKey( ProtectedItem.ProtectionModeEnum.BlockAdd ))
                    continue;

                var settings = item.ProtectionSettingsDict[ProtectedItem.ProtectionModeEnum.BlockAdd];

                if ( Protection.Instance.CheckPlayerExempt( settings, grid, remoteUserId ) )
                    continue;
                
                if (item.LogOnly)
                {
                    Essentials.Log.Info($"Recieved block add request from user {PlayerMap.Instance.GetFastPlayerNameFromSteamId(remoteUserId)}:{remoteUserId} for grid {grid.DisplayNameText ?? "ID"}:{item.EntityId}");
                    continue;
                }

                if (!string.IsNullOrEmpty(settings.PrivateWarningMessage))
                    Communication.Notification(remoteUserId, MyFontEnum.Red, 5000, settings.PrivateWarningMessage);

                if(!string.IsNullOrEmpty( settings.PublicWarningMessage ))
                    Communication.SendPublicInformation( settings.PublicWarningMessage.Replace( "%player%",PlayerMap.Instance.GetFastPlayerNameFromSteamId( remoteUserId ) ) );

                if ( settings.BroadcastGPS )
                {
                    var player = MySession.Static.Players.GetPlayerById( new MyPlayer.PlayerId( remoteUserId, 0 ) );
                    var pos = player.GetPosition();
                    MyAPIGateway.Utilities.SendMessage($"GPS:{player.DisplayName}:{pos.X}:{pos.Y}:{pos.Z}:");
                }

                Essentials.Log.Info($"Intercepted block add request from user {PlayerMap.Instance.GetFastPlayerNameFromSteamId(remoteUserId)}:{remoteUserId} for grid {grid.DisplayNameText ?? "ID"}:{item.EntityId}");

                if (settings.PunishmentType == ProtectedItem.PunishmentEnum.Kick)
                {
                    _kickTimer.Elapsed += (sender, e) =>
                                         {
                                             Essentials.Log.Info($"Kicked user {PlayerMap.Instance.GetFastPlayerNameFromSteamId(remoteUserId)}:{remoteUserId} for adding blocks to protected grid {grid.DisplayNameText ?? "ID"}:{item.EntityId}");
                                             MyMultiplayer.Static.KickClient(remoteUserId);
                                         };
                    _kickTimer.Start();
                }
                else if (settings.PunishmentType == ProtectedItem.PunishmentEnum.Ban)
                {
                    _kickTimer.Elapsed += (sender, e) =>
                                         {
                                             Essentials.Log.Info($"Banned user {PlayerMap.Instance.GetFastPlayerNameFromSteamId(remoteUserId)}:{remoteUserId} for adding blocks to protected grid {grid.DisplayNameText ?? "ID"}:{item.EntityId}");
                                             MyMultiplayer.Static.BanClient(remoteUserId, true);
                                         };
                    _kickTimer.Start();
                }

                found = true;
            }
            
            return found;
        }
    }
}