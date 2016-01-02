﻿namespace EssentialsPlugin.ChatHandlers
{
	using System.Linq;
	using EssentialsPlugin.Utility;
	using SEModAPIInternal.API.Common;

	public class HandleLastSeen : ChatHandlerBase
	{
		public override string GetHelp()
		{
			return "Shows the last time a user has been seen.  Usage: /lastseen \"<playername>\"";
		}

		public override string GetCommandText()
		{
			return "/lastseen";
		}

        public override Communication.ServerDialogItem GetHelpDialog( )
        {
            Communication.ServerDialogItem DialogItem = new Communication.ServerDialogItem( );
            DialogItem.title = "Help";
            DialogItem.header = "";
            DialogItem.content = GetHelp( );
            DialogItem.buttonText = "close";
            return DialogItem;
        }

        public override bool IsAdminCommand()
		{
			return false;
		}

		public override bool AllowedInConsole()
		{
			return true;
		}

		public override bool HandleCommand(ulong userId, string[] words)
		{
			if (words.Count() != 1)
			{
				Communication.SendPrivateInformation(userId, GetHelp());
				return true;
			}

			string userName = words[0];
			ulong steamId = PlayerMap.Instance.GetSteamIdFromPlayerName(userName, true);
			if (steamId == 0)
			{
				Communication.SendPrivateInformation(userId, string.Format("Unable to find player '{0}", userName));
				return true;
			}

			long playerId = PlayerMap.Instance.GetFastPlayerIdFromSteamId(steamId);			
			PlayerItem item = Players.Instance.GetPlayerById(playerId);
			if (item != null)
			{
				Communication.SendPrivateInformation(userId, string.Format("Player '{0}' last seen: {1}", PlayerMap.Instance.GetPlayerItemFromPlayerId(playerId).Name, item.LastLogin.ToString("g")));
			}
			else
			{
				Communication.SendPrivateInformation(userId, string.Format("No login information for user '{0}'", userName));
			}

			return true;
		}
	}
}
