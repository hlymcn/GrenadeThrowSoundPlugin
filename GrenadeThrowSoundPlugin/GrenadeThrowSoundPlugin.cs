using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using static GrenadeThrowSoundPlugin.GrenadeThrowSoundPlugin;

namespace GrenadeThrowSoundPlugin
{
    [MinimumApiVersion(80)]
    public class GrenadeThrowSoundPlugin : BasePlugin, IPluginConfig<GrenadeThrowSoundsConfig>
    {
        public override string ModuleName => "GrenadeThrowSoundPlugin";
        public override string ModuleVersion => "0.0.1";
        public override string ModuleAuthor => "hlymcn";
        public override string ModuleDescription => "Play a custom sound when a player throws a grenade.";
        public required GrenadeThrowSoundsConfig Config { get; set; }
        public readonly IStringLocalizer<GrenadeThrowSoundPlugin> _localizer;
        private Dictionary<CCSPlayerController, (int count, DateTime cooldownEndTime)> playerThrowData = new();

        public GrenadeThrowSoundPlugin(IStringLocalizer<GrenadeThrowSoundPlugin> localizer)
        {
            _localizer = localizer;
        }

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
            OnConfigParsed(Config);
        }

        private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot)
                return HookResult.Continue;

                if (@event.Weapon == "hegrenade")
                {
                    if (!playerThrowData.TryGetValue(player, out var data))
                    {
                        data = (1, DateTime.MinValue);
                        playerThrowData[player] = data;
                        PlayAndBroadcastSound(player);
                    }
                    else
                    {
                        if (DateTime.UtcNow < data.cooldownEndTime)
                        {
                            var remainingSeconds = (data.cooldownEndTime - DateTime.UtcNow).TotalSeconds;
                            Server.NextFrame(() => player.PrintToChat($"{_localizer["lang.chat.cooldown", remainingSeconds]}"));
                            return HookResult.Continue;
                        }

                        data = (data.count + 1, data.cooldownEndTime);
                        playerThrowData[player] = data;

                        if (data.count >= Config.MaxThrows)
                        {
                            playerThrowData[player] = (0, DateTime.UtcNow.AddSeconds(Config.CooldownSeconds));
                            PlayAndBroadcastSound(player);
                        }
                        else
                        {
                            PlayAndBroadcastSound(player);
                        }
                    }
                }
            return HookResult.Continue;
        }

        private void PlayAndBroadcastSound(CCSPlayerController player)
        {
            var sound = Config.GrenadeThrowSounds[Random.Shared.NextDistinct(Config.GrenadeThrowSounds.Count)];
            foreach (var onlinePlayer in Utilities.GetPlayers())
            {
                onlinePlayer.ExecuteClientCommand($"play \"{sound}\"");
            }

            var teamColor = player.Team switch
            {
                CsTeam.CounterTerrorist => ChatColors.Blue,
                CsTeam.Terrorist => ChatColors.Red,
                _ => ChatColors.White
            };

            var playerName = player.PlayerName ?? "Console";
            Server.PrintToChatAll($"{_localizer["lang.chatall.grenade", playerName, teamColor]}");
        }

        public void OnConfigParsed(GrenadeThrowSoundsConfig config)
        {
            Config = config;
            if (config.GrenadeThrowSounds.Count == 0)
            {
                Server.PrintToConsole("音源文件未设置!");
            }
            else
            {
                Server.PrintToConsole($"载入 {config.GrenadeThrowSounds.Count} 音源文件!");
            }
        }

        public class GrenadeThrowSoundsConfig : BasePluginConfig
        {
            public List<string> GrenadeThrowSounds { get; set; } = [];
            public int MaxThrows { get; set; } = 3;
            public int CooldownSeconds { get; set; } = 10;
        }
    }
}
