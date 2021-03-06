using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Nixill.CalcLib.Objects;
using Nixill.CalcLib.Parsing;
using Nixill.CalcLib.Varaibles;
using Nixill.DiceLib;
using Nixill.Discord.Extensions;

namespace Nixill.Discord.ShadowRoller.Commands
{
  public class RollCommand : SlashCommandModule
  {
    private static Regex rgxName = new Regex(@"^\$?[a-z][a-z-_0-9]*[a-z0-9]$");

    [SlashCommand("roll", "Rolls dice.")]
    public async Task RollMethod(InteractionContext ctx,
      [Option("roll_text", "The text of the command to roll")] string roll_text,
      [Option("seed", "The seed to use.")] long? seedLong = null,
      [Option("detailed", "Whether or not to use detailed output.")] bool detailed = false,
      [Option("save_to", "Variable to save the result to.")] string saveTo = null)
    {
      if (roll_text.Contains('"'))
      {
        await ctx.ReplyAsync("Shadow Roller doesn't support strings.");
        return;
      }

      if (saveTo != null)
      {
        saveTo = saveTo.ToLower();

        if (!rgxName.IsMatch(saveTo))
        {
          await ctx.ReplyAsync("Invalid variable name.");
          return;
        }

        if (saveTo.StartsWith('$') && ctx.User.Id != ShadowRollerMain.Owner)
        {
          await ctx.ReplyAsync("Only the bot's owner may save global variables.");
          return;
        }
      }

      await ctx.DeferAsync();

      CalcObject obj = null;

      int seed = 0;

      if (seedLong == null) seed = (int)ctx.InteractionId;
      else seed = (int)seedLong;

      try
      {
        // Get the expression and some intermediate objects ready
        obj = CLInterpreter.Interpret(roll_text);
        CalcValue res;
        CalcNumber num;

        // Get the context and vars ready
        CLContextProvider context = new CLContextProvider();

        context.Add(new DiceContext
        {
          PerFunctionLimit = 100,
          PerRollLimit = 25
        });

        context.Add(new Random((int)seed));

        context.Add(ctx.Guild);
        context.Add(ctx.User);

        List<(string, CalcList)> history = new List<(string, CalcList)>();
        context.Add(history);

        CLLocalStore vars = new CLLocalStore();

        // Get the outputs ready too!
        string input = null;
        string list = null;
        string result = null;

        // And start parsing things.
        if (obj is CalcExpression exp)
        {
          input = exp.ToCode();
        }
        res = obj.GetValue(vars, context);

        if (res is CalcList lst)
        {
          num = lst.Sum();
          list = lst.ToString(2);
        }
        else
        {
          num = (CalcNumber)res;
        }

        result = num.ToString();

        // Save the output to a variable, if necessary.
        if (saveTo != null)
        {
          // we've done the necessary checks above
          CLVariables.Save(saveTo, res, context);
        }

        // Now build the output.
        if (!detailed)
        {
          StringBuilder ret = new StringBuilder();

          if (input != null)
          {
            ret.Append($"Input: `{input}` // Result: ");
          }

          ret.Append($"**{result}**");

          if (list != null)
          {
            ret.Append($" {list}");
          }

          if (history.Count > 0)
          {
            ret.Append($" *(Seed: {seed})*");
          }

          if (saveTo != null)
          {
            ret.Append($" // Saved to `{{{saveTo}}}`");
          }

          await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(ret.ToString()));
        }
        else
        {
          DiscordEmbedBuilder builder = new DiscordEmbedBuilder();

          if (input != null) builder.AddField("Input interpretation", $"`{input}`", true);
          builder.AddField("Output value", $"**{result}**", true);
          if (list != null) builder.AddField("Output list", list, true);
          if (history.Count > 0) builder.AddField("Random seed", seed.ToString(), true);
          if (saveTo != null) builder.AddField("Saved to", $"{{{saveTo}}}", true);

          if (history.Count > 0)
          {
            StringBuilder historyOutput = new StringBuilder();
            foreach (var item in history)
            {
              historyOutput.AppendLine($"`{item.Item1}`: {item.Item2.ToString(1)}");
            }

            builder.AddField("Rolls", historyOutput.ToString(), false);
          }

          await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(builder.Build()));
        }
      }
      catch (Exception ex)
      {
        StringBuilder builder = new StringBuilder();
        if (obj != null) builder.Append($"Input interpretation: `{obj.ToCode()}`\n\n");
        builder.Append("An error occurred");
#if DEBUG
        builder.Append(".\n\nThe stack trace is as follows:\n");
        builder.Append($"{ex}");
#else
        builder.AppendLine($": {ex.GetType().Name}: {ex.Message}");
#endif

        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(builder.ToString()));
      }
    }
  }
}