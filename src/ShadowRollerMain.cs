using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Nixill.CalcLib.Modules;
using Nixill.DiceLib;
using Nixill.Discord.ShadowRoller.Commands;
using Nixill.Discord.ShadowRoller.Variables;

namespace Nixill.Discord.ShadowRoller
{
  public class ShadowRollerMain
  {
    internal static DiscordClient Discord;
    internal static SlashCommandsExtension Commands;

    internal static ulong Owner;

    static void Main(string[] args) => MainAsync().GetAwaiter().GetResult();

    public static async Task MainAsync()
    {
      // Let's load CalcLib modules
      MainModule.Load();
      DiceModule.Load();

      // Let's get the bot set up
#if DEBUG
      string botToken = File.ReadAllLines("cfg/debug_token.cfg")[0];
#else
      string botToken = File.ReadAllLines("cfg/token.cfg")[0];
#endif

      Owner = ulong.Parse(File.ReadAllLines("cfg/owner.txt")[0]);

      Discord = new DiscordClient(new DiscordConfiguration()
      {
        Token = botToken,
        TokenType = TokenType.Bot
      });

      Commands = Discord.UseSlashCommands();

      await Discord.ConnectAsync();

#if DEBUG
      Commands.RegisterCommands<RollCommand>(608847976554692611L);
      Commands.RegisterCommands<VarCommands>(608847976554692611L);
#else
      Commands.RegisterCommands<RollCommand>();
      Commands.RegisterCommands<VarCommands>();
#endif

      await using var io = new VarIO();
      await io.CreateHandlers();

      await Task.Delay(-1);
    }
  }
}