using System.Collections.Generic;

public enum ChatChannel
{
    Harmony = 0,
    Boss = 1,
    Team = 2,
    Security = 3,
}

public class ChatMessage
{
    public ChatChannel channel;
    public string sender;
    public string text;
    public string time;

    public ChatMessage(ChatChannel channel, string sender, string text, string time)
    {
        this.channel = channel;
        this.sender = sender;
        this.text = text;
        this.time = time;
    }
}

public static class ChatChannels
{
    public static readonly string[] Names = { "ГАРМОНИЯ", "ВИКТОР", "КОМАНДА", "БЕЗОП." };
    public static readonly string[] Senders = { "ГАРМОНИЯ", "ВИКТОР", "КОМАНДА", "КИБЕРБЕЗОПАСНОСТЬ" };
}
