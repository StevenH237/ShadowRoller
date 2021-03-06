using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using Microsoft.Data.Sqlite;
using Nixill.CalcLib.Objects;
using Nixill.CalcLib.Parsing;
using Nixill.CalcLib.Varaibles;

namespace Nixill.Discord.ShadowRoller.Variables
{
  public class VarIO : IAsyncDisposable
  {
    private SqliteConnection conn;

    private const string LoadCmd =
    @"SELECT varObject
      FROM variables
      WHERE varName = $name
        AND varGuild = $guild;";

    private const string SaveCmd =
    @"INSERT OR REPLACE INTO variables
      (varName, varGuild, varObject)
      VALUES ($name, $guild, $object);";

    private const string DelCmd =
    @"DELETE FROM variables
      WHERE varName = $name
        AND varGuild = $guild;";

    private const string ListCmd =
    @"SELECT varName, varObject
      FROM variables
      WHERE varGuild = $guild;";

    internal static VarIO Handler;

    public async Task CreateHandlers()
    {
      conn = new SqliteConnection("Data Source=cfg/variables.db");
      await conn.OpenAsync();

      CLVariables.VariableLoaded += LoadHandler;
      CLVariables.VariableSaved += SaveHandler;
      CLVariables.VariableDeleted += DelHandler;

      Handler = this;
    }

    public void LoadHandler(object sender, CLVariableLoad loadData)
    {
      if (sender is CLContextProvider context)
      {
        if (context.ContainsDerived(typeof(DiscordGuild), out Type derType))
        {
          DiscordGuild guild = (DiscordGuild)context.Get(derType);

          ulong guildId = 0;
          if (!loadData.Name.StartsWith('$')) guildId = guild.Id;

          var cmd = conn.CreateCommand();
          cmd.CommandText = LoadCmd;
          cmd.Parameters.AddWithValue("$name", loadData.Name);
          cmd.Parameters.AddWithValue("$guild", guildId);

          using (var reader = cmd.ExecuteReader())
          {
            if (reader.Read())
            {
              loadData.Value = CLInterpreter.Interpret(reader.GetString(0));
            }
          }
        }
      }
    }

    public void SaveHandler(object sender, CLVariableSave saveData)
    {
      if (sender is CLContextProvider context)
      {
        if (context.ContainsDerived(typeof(DiscordGuild), out Type guildType))
        {
          // we'll just always do this so we don't ever save to internal storage
          saveData.Saved = true;

          DiscordGuild guild = (DiscordGuild)context.Get(guildType);

          ulong guildId = 0;
          if (!saveData.Name.StartsWith('$')) guildId = guild.Id;

          var cmd = conn.CreateCommand();
          cmd.CommandText = SaveCmd;
          cmd.Parameters.AddWithValue("$name", saveData.Name);
          cmd.Parameters.AddWithValue("$guild", guildId);
          cmd.Parameters.AddWithValue("$object", saveData.Value.ToCode());

          cmd.ExecuteNonQuery();
        }
      }
    }

    public void DelHandler(object sender, CLVariableDelete delData)
    {
      if (sender is CLContextProvider context)
      {
        if (context.ContainsDerived(typeof(DiscordGuild), out Type guildType))
        {
          delData.Deleted = true;

          DiscordGuild guild = (DiscordGuild)context.Get(guildType);

          ulong guildId = 0;
          if (!delData.Name.StartsWith('$')) guildId = guild.Id;

          var cmd = conn.CreateCommand();
          cmd.CommandText = DelCmd;
          cmd.Parameters.AddWithValue("$name", delData.Name);
          cmd.Parameters.AddWithValue("$guild", guildId);

          cmd.ExecuteNonQuery();
        }
      }
    }

    public IDictionary<string, string> ListVariables(ulong guildId)
    {
      Dictionary<string, string> ret = new();

      var cmd = conn.CreateCommand();
      cmd.CommandText = ListCmd;
      cmd.Parameters.AddWithValue("$guild", guildId);

      var reader = cmd.ExecuteReader();

      while (reader.Read())
      {
        ret.Add(reader.GetString(0), reader.GetString(1));
      }

      return ret;
    }

    public async ValueTask DisposeAsync()
    {
      await conn.CloseAsync();
    }
  }
}