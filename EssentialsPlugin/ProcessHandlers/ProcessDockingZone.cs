﻿namespace EssentialsPlugin.ProcessHandlers
{
	using System;
	using System.Collections.Generic;
	using EssentialsPlugin.Utility;
	using Sandbox.Common;
	using Sandbox.ModAPI;
	using SEModAPIInternal.API.Common;
	using VRage.Game;
	using VRage.Game.ModAPI;
	using VRage.ModAPI;
	using VRageMath;

	public class ProcessDockingZone : ProcessHandlerBase
	{
		private DateTime m_lastZoneUpdate;
		private static HashSet<IMyCubeGrid> m_zoneCache;
		private List<long> m_playersInside;

		public static HashSet<IMyCubeGrid> ZoneCache
		{
			get { return m_zoneCache; }
			set { m_zoneCache = value; }
		}

		public ProcessDockingZone()
		{
			m_lastZoneUpdate = DateTime.Now.AddMinutes(-1);
			m_playersInside = new List<long>();
			m_zoneCache = new HashSet<IMyCubeGrid>();
		}

		public override int GetUpdateResolution()
		{
			return 1000;
		}

		public override void Handle()
		{
			if (!PluginSettings.Instance.DockingEnabled)
				return;

			// ZoneCache can be used elsewhere.  I need to move this out of here.
			if (DateTime.Now - m_lastZoneUpdate > TimeSpan.FromSeconds(30))
			{
				m_lastZoneUpdate = DateTime.Now;
				PopulateZoneCache();
			}

			List<IMyPlayer> players = new List<IMyPlayer>();
			//Wrapper.GameAction(() =>
			//{
				MyAPIGateway.Players.GetPlayers(players);
			//});

			foreach (IMyPlayer player in players)
			{
				CheckPlayerInDockingZone(player);
			}

			base.Handle();
		}

		private void CheckPlayerInDockingZone(IMyPlayer player)
		{

			if(player.Controller == null || player.Controller.ControlledEntity == null || player.Controller.ControlledEntity.Entity == null)
				return;

			IMyEntity entity = player.Controller.ControlledEntity.Entity;
			long playerId = player.PlayerID;
			IMyEntity parent = entity.GetTopMostParent();

			// Not a ship?  let's not process
			if (!(parent is IMyCubeGrid))
			{
				if (m_playersInside.Contains(playerId))
				{
					m_playersInside.Remove(playerId);
					ulong steamId = PlayerMap.Instance.GetSteamIdFromPlayerId(playerId);
					Communication.Notification(steamId, MyFontEnum.DarkBlue, 7, "You have exited a ship in a docking zone");
				}

				return;
			}

			// Get All ships with 500m
			BoundingSphereD sphere = new BoundingSphereD(parent.GetPosition(), 500);
			List<IMyEntity> nearByEntities = null;

			// Live dangerously (no wrapper for speed!)
            //actually it causes crashes :(
			try
			{
                Wrapper.GameAction( ( ) =>
                 {
                     nearByEntities = MyAPIGateway.Entities.GetEntitiesInSphere( ref sphere );
                 } );
			}
			catch(Exception ex)			
			{
				Essentials.Log.Error( ex );
				return;
			}
			
			if (nearByEntities == null)
				return;

			List<IMyEntity> nearByGrids = nearByEntities.FindAll(x => x is IMyCubeGrid);

			// See if player's ship is inside a docking zone
			foreach (IMyEntity nearByEntity in nearByGrids)
			{
				// Sanity Check
				if (!(nearByEntity is IMyCubeGrid))
					return;

				IMyCubeGrid cubeGrid = (IMyCubeGrid)nearByEntity;
				// Does this grid contain a docking zone?
				if (m_zoneCache.Contains(cubeGrid))
				{
					Dictionary<String, List<IMyCubeBlock>> zoneList = DockingZone.GetZonesInGrid(cubeGrid);
					if (zoneList == null)
						continue;

					// Get zones
					foreach (KeyValuePair<string, List<IMyCubeBlock>> p in zoneList)
					{
						// Check if we're inside
						if (DockingZone.IsGridInside((IMyCubeGrid)parent, p.Value))
						{
							if (!m_playersInside.Contains(playerId))
							{
								m_playersInside.Add(playerId);
								ulong steamId = PlayerMap.Instance.GetSteamIdFromPlayerId(playerId);
								Communication.Notification(steamId, MyFontEnum.Green, 7, $"You are inside a valid docking zone: {p.Key}" );
							}

							return;
						}
					}
				}
			}

			// We've left
			if (m_playersInside.Contains(playerId))
			{
				m_playersInside.Remove(playerId);
				ulong steamId = PlayerMap.Instance.GetSteamIdFromPlayerId(playerId);
				Communication.Notification(steamId, MyFontEnum.Red, 7, "You have left a docking zone");
			}
		}

		private void PopulateZoneCache()
		{
			HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
			Wrapper.GameAction(() =>
			{
			    MyAPIGateway.Entities.GetEntities(entities, x => x is IMyCubeGrid);
			});

			m_zoneCache.Clear();
			foreach (IMyEntity entity in entities)
			{
				IMyCubeGrid grid = (IMyCubeGrid)entity;
				if (DockingZone.DoesGridContainZone(grid))
				{
					m_zoneCache.Add(grid);
				}
			}
		}
	}
}

