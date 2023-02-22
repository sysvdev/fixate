/*
 *      This file is part of Fixate distribution (https://github.com/vortex1409/fixate).
 *      Copyright (c) 2023 contributors
 *
 *      Fixate is free software: you can redistribute it and/or modify
 *      it under the terms of the GNU General Public License as published by
 *      the Free Software Foundation, either version 3 of the License, or
 *      (at your option) any later version.
 *
 *      Fixate is distributed in the hope that it will be useful,
 *      but WITHOUT ANY WARRANTY; without even the implied warranty of
 *      MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *      GNU General Public License for more details.
 *
 *      You should have received a copy of the GNU General Public License
 *      along with Fixate.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace Fixate;

internal class Program
{
    private static Logger? logger;

    public static Logger Logger
    { get { return logger ?? throw new Exception("Logger Is Null"); } set => logger = value; }

    public static string CurrentDir { get => Environment.CurrentDirectory; }
    public static string BossDataPath { get; set; } = Path.Combine(CurrentDir, @"bosses");
    public static string SettingPath { get; set; } = Path.Combine(CurrentDir, "config.json");

    public static Settings Config { get; set; } = new();
    public static Fixate.Datas.Localization Localization { get; set; }

    public static Dictionary<string, BossData> bossDatas = new();

    public readonly EventId BotEventId = new(42, "Fixate");

    public static DiscordClient? Client { get; set; }

    public VoiceNextExtension Voice { get; set; }

    public static bool stop = true;

    public static DiscordChannel? lastdiscordChannel = null;

    private static void Main(string[] args)
    {
        Thread.CurrentThread.Name = "MainThread";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Config.LoadSettings();
        }
        catch (Exception ex) { Logger.Error(ex, "Error loading {SettingPath}", SettingPath); }
        finally { logger.Information("Settings loaded"); }

        Localization = GetLocalization();

        var prog = new Program();

        try { prog.RunBotAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) { Logger.Error(ex, ex.Message); }
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public Program()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        Config.Discord.Token = Environment.GetEnvironmentVariable("Discord_Token") ?? Config.Discord.Token;
        Config.Discord.DefaultActivity = Environment.GetEnvironmentVariable("Discord_DefaultActivity") ?? Config.Discord.DefaultActivity;

        if (Enum.TryParse<ActivityType>(Environment.GetEnvironmentVariable("Discord_DefaultActivityType"), out ActivityType at))
        {
            Config.Discord.DefaultActivityType = at;
        }
        else
        {
            Config.Discord.DefaultActivityType = ActivityType.ListeningTo;
        }

        Config.Voice.APIToken = Environment.GetEnvironmentVariable("Voice_APIToken") ?? Config.Voice.APIToken;
        Config.Voice.APIRegion = Environment.GetEnvironmentVariable("Voice_APIRegion") ?? Config.Voice.APIRegion;
        Config.Voice.Language = Environment.GetEnvironmentVariable("Voice_Language") ?? Config.Voice.Language;
        Config.Voice.Name = Environment.GetEnvironmentVariable("Voice_Name") ?? Config.Voice.Name;
        Config.Voice.Style = Environment.GetEnvironmentVariable("Voice_Style") ?? Config.Voice.Style;

        Config.SaveSetting();

        if (!Directory.Exists(BossDataPath))
        {
            Directory.CreateDirectory(BossDataPath);
        }

        foreach (string file in Directory.GetFiles(BossDataPath))
        {
            if (file.Contains(".json"))
            {
                if (!file.Contains(".deps.json") || !file.Contains(".runtimeconfig.json"))
                {
                    string shortfilename = file.Replace(CurrentDir, "");

                    Logger.Information("Loading {ShortFileName}", shortfilename);
                    string json_string = File.ReadAllText(file);

                    if (Json.IsValid(json_string))
                    {
                        JsonSerializerOptions options = new()
                        {
                            WriteIndented = true
                        };

                        try
                        {
                            BossData bd = JsonSerializer.Deserialize<BossData>(json_string, options) ?? throw new Exception("Error deserializing data");
                            bossDatas.Add(bd.Name, bd);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Problem with deserializing {ShortFileName}", shortfilename);
                        }
                    }
                    Logger.Information("Loaded");
                }
            }
        }
    }

    public async Task RunBotAsync()
    {
        ILoggerFactory logFactory = new LoggerFactory().AddSerilog(logger);

        var cfg = new DiscordConfiguration
        {
            Token = Config.Discord.Token,
            TokenType = TokenType.Bot,

            AutoReconnect = true,
            LoggerFactory = logFactory,

            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
        };

        Client = new DiscordClient(cfg);

        Client.Ready += this.Client_Ready;
        Client.GuildAvailable += this.Client_GuildAvailable;
        Client.ClientErrored += this.Client_ClientError;

        var slash = Client.UseSlashCommands();

        slash.SlashCommandExecuted += Slash_SlashCommandExecuted;
        slash.SlashCommandErrored += Slash_SlashCommandErrored;
        slash.SlashCommandInvoked += Slash_SlashCommandInvoked;

        slash.RegisterCommands<BotSlashCommands>();

        this.Voice = Client.UseVoiceNext();

        await Client.ConnectAsync();

        await Task.Delay(-1);
    }

    private async Task Slash_SlashCommandInvoked(SlashCommandsExtension sender, DSharpPlus.SlashCommands.EventArgs.SlashCommandInvokedEventArgs e)
    {
        e.Context.Client.Logger.LogInformation(BotEventId, "{Username} successfully invoked '{CommandName}'", e.Context.User.Username, e.Context.CommandName);

        await e.Context.Client.UpdateStatusAsync(new DiscordActivity()
        {
            ActivityType = ActivityType.Playing,
            Name = "Running commands..."
        }, UserStatus.Idle);
    }

#nullable disable

    private async Task Slash_SlashCommandErrored(SlashCommandsExtension sender, DSharpPlus.SlashCommands.EventArgs.SlashCommandErrorEventArgs e)
    {
        e.Context.Client.Logger.LogError(BotEventId, "{Username} tried executing '{CommandName}' but it errored: {Type}: {Message}", e.Context.User.Username, e.Context?.CommandName ?? "<unknown command>", e.Exception.GetType(), e.Exception.Message ?? "<no message>");

        if (e.Exception is ChecksFailedException)
        {
            var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

            var embed = new DiscordEmbedBuilder
            {
                Title = "Access denied",
                Description = $"{emoji} You do not have the permissions required to execute this command.",
                Color = new DiscordColor(0xFF0000)
            };
            await e.Context.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent($"{embed}"));
        }

        await e.Context.Client.UpdateStatusAsync(new DiscordActivity()
        {
            ActivityType = ActivityType.Watching,
            Name = "Errors!"
        }, UserStatus.DoNotDisturb);
    }

#nullable enable

    private async Task Slash_SlashCommandExecuted(SlashCommandsExtension sender, DSharpPlus.SlashCommands.EventArgs.SlashCommandExecutedEventArgs e)
    {
        e.Context.Client.Logger.LogInformation(BotEventId, "{Username} successfully executed '{CommandName}'", e.Context.User.Username, e.Context.CommandName);

        if (Program.stop)
        {
            await e.Context.Client.UpdateStatusAsync(new DiscordActivity()
            {
                ActivityType = Config.Discord.DefaultActivityType,
                Name = Config.Discord.DefaultActivity
            }, UserStatus.Online);
        }
        else
        {
            await e.Context.Client.UpdateStatusAsync(new DiscordActivity()
            {
                ActivityType = ActivityType.Playing,
                Name = "Mechanics..."
            }, UserStatus.DoNotDisturb);
        }
    }

    private async Task Client_Ready(DiscordClient sender, ReadyEventArgs e)
    {
        sender.Logger.LogInformation(BotEventId, "Client is ready to process events.");

        await sender.UpdateStatusAsync(new DiscordActivity()
        {
            ActivityType = Config.Discord.DefaultActivityType,
            Name = Config.Discord.DefaultActivity
        }, UserStatus.Online);
    }

    private Task Client_GuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
    {
        sender.Logger.LogInformation(BotEventId, "Guild available: {GuildName}", e.Guild.Name);

        return Task.CompletedTask;
    }

    private Task Client_ClientError(DiscordClient sender, ClientErrorEventArgs e)
    {
        sender.Logger.LogError(BotEventId, e.Exception, "Exception occured");

        return Task.CompletedTask;
    }

    public static async Task<byte[]?> GetTTSStream(string text)
    {
        string ssml = GenerateSsml(Config.Voice.Language, Config.Voice.Name, text, Config.Voice.Style);

        var config = SpeechConfig.FromSubscription(Config.Voice.APIToken, Config.Voice.APIRegion);
        config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm);

        using (var synthesizer = new SpeechSynthesizer(config, null))
        {
            using var result = await synthesizer.SpeakSsmlAsync(ssml);
            if (result.Reason is ResultReason.SynthesizingAudioCompleted)
            {
                Logger.Warning("Speech synthesized for text [{Text}], and the audio was written to output stream.", text);
                return AudioConverter.Resample(result.AudioData, 48000, 48000, 1, 2);
            }
            else if (result.Reason is ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Logger.Warning("CANCELED: Reason={Reason}", cancellation.Reason);

                if (cancellation.Reason is CancellationReason.Error)
                {
                    Logger.Warning("CANCELED: ErrorCode={ErrorCode}", cancellation.ErrorCode);
                    Logger.Warning("CANCELED: ErrorDetails=[{ErrorDetails}]", cancellation.ErrorDetails);
                    Logger.Warning("CANCELED: Did you update the subscription info?");
                }

                return null;
            }
        }

        return null;
    }

    private static string GenerateSsml(string locale, string name, string text, string? style = null)
    {
        string ssmlbody;

        if (style is not null)
        {
            if (!style.Equals("none")) { ssmlbody = $"<mstts:express-as style=\"{style}\" >" + text + "</mstts:express-as>"; }
            else { ssmlbody = text; }
        }
        else
        {
            ssmlbody = text;
        }

        string ssmlDoc = $"<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" version=\"1.0\" xml:lang=\"{locale}\">" +
                         $"<voice name=\"{name}\">" +
                            ssmlbody +
                         "</voice>" +
                         "</speak>";

        return ssmlDoc;
    }

    public static Datas.Localization GetLocalization()
    {
        Fixate.Datas.Localization output = new();
        string path = Path.Combine("localizations", $"{Config.Voice.Language.ToLower()}.json");

        Logger.Information("Loading {Path}", path);

        string json_string = File.ReadAllText(Path.Combine(CurrentDir, path));

        try
        {
            if (Json.IsValid(json_string))
            {
                JsonSerializerOptions options = new()
                {
                    WriteIndented = true
                };

                output = JsonSerializer.Deserialize<Fixate.Datas.Localization>(json_string, options);
            }
            else
            {
                Logger.Error("Localization file isn't valid!!");
            }
        }
        catch (Exception ex) { Logger.Error(ex, "Localization file isn't valid!!"); }
        finally
        {
            Logger.Information("Localization Loaded");
        }

        return output;
    }

    public static void RunMechanic(BossData bossData, string[] players, VoiceNextConnection vnc)
    {
        stop = false;

        for (int bmIndex = 0; bmIndex <= bossData.Mechanics.Length - 1; bmIndex++)
        {
            BossMechanics bossMechanics = bossData.Mechanics[bmIndex];
            Logger.Information("Starting boss mechanics thread for Index: {BossMIndex}", bmIndex);

            Thread thread = new(() =>
            {
                Thread.CurrentThread.Name = $"BossMechanics{bmIndex}";

                Stopwatch starttime = new();
                Stopwatch intervaltime = new();

                int index = 0;

                int playerIndex = 1;

                Datas.Localization localization = GetLocalization();

                Say(localization.TimerCountDown, vnc);

                Say(localization.TimerStart, vnc);

                starttime.Start();

                bool startit = false;
                bool stopit = false;

                while (true)
                {
                    if (starttime.Elapsed.TotalSeconds == bossMechanics.StartTime)
                    {
                        startit = true;
                    }

                    if ((intervaltime.Elapsed.TotalSeconds >= bossMechanics.Interval - bossMechanics.WarnTime) || startit)
                    {
                        if (bossMechanics.PlayersInvolved is not 0)
                        {
                            if ((index + 1) > bossMechanics.MechanicNames.Length)
                            {
                                index = 0;
                            }

                            if (playerIndex > bossMechanics.PlayersInvolved)
                            {
                                playerIndex = 1;
                            }

                            MechanicExe(bossMechanics, index, playerIndex, players, vnc, localization);

                            playerIndex++;
                            index++;
                        }
                        else
                        {
                            MechanicExe(bossMechanics, index, playerIndex, players, vnc, localization);
                        }

                        intervaltime.Restart();
                        startit = false;
                    }

                    if (starttime.Elapsed.TotalSeconds >= bossData.TimeLimit + 1) { stopit = true; }

                    if (stop || stopit) { starttime.Stop(); intervaltime.Stop(); break; }
                }

                Logger.Information("Seconds: {Seconds}", starttime.Elapsed.TotalSeconds);

                if (Client is not null)
                {
                    _ = Client.SendMessageAsync(lastdiscordChannel, $"End of the Mechanic Index: {bmIndex}");
                }
            })
            {
                IsBackground = true
            };

            thread.Start();
            Thread.Sleep(500);
        }
    }

    public static string LastSay = "";

    public static void MechanicExe(BossMechanics bossMechanics, int index, int playerIndex, string[] players, VoiceNextConnection vnc, Datas.Localization localization)
    {
        if (bossMechanics.PlayersInvolved is not 0)
        {
            string MechanicName = bossMechanics.MechanicNames[index];
            string PlayerName = localization.DefaultPlayerNameFormat.Replace("%(name)", players[(playerIndex - 1)]).Replace("%(index)", playerIndex.ToString());

            Say(localization.MechanicsFormat.Replace("%(playerName)", PlayerName).Replace("%(mechanicName)", MechanicName), vnc);
        }
        else
        {
            Say(bossMechanics.MechanicNames[0], vnc);
        }
    }

    public static void Say(string text, VoiceNextConnection vnc)
    {
        if (!LastSay.Equals(text))
        {
            LastSay = text;
            Logger.Information("Saying {Text}", text);

            if (vnc is null)
            {
                if (Client is not null)
                {
                    _ = Client.SendMessageAsync(lastdiscordChannel, $"**[{DateTime.Now:HH:mm:ss}]** {text}");
                }
            }
            else
            {
                try
                {
                    vnc.SendSpeakingAsync(true);
                    var ffout = new MemoryStream(Program.GetTTSStream(text).GetAwaiter().GetResult() ?? Array.Empty<byte>());

                    var txStream = vnc.GetTransmitSink();
                    ffout.CopyToAsync(txStream);
                    txStream.FlushAsync();
                    vnc.WaitForPlaybackFinishAsync();
                }
                catch (Exception ex) { Logger.Error(ex, "Failed saying {Text}", text); }
            }
        }
    }
}