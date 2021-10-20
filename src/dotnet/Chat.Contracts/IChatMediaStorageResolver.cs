namespace ActualChat.Chat;

public interface IChatMediaStorageResolver
{
    public Uri GetAudioBlobAddress(ChatEntry audioEntry);
    public Uri GetVideoBlobAddress(ChatEntry videoChatEntry);
}
