namespace KakiMoni.Core.Protocol;

public static class HostCallbacks
{
    public const string FullState = "FullState";
    public const string StrokeStart = "StrokeStart";
    public const string StrokePoint = "StrokePoint";
    public const string StrokeEnd = "StrokeEnd";
    public const string SeatStrokesUpdated = "SeatStrokesUpdated";
    public const string SeatLocked = "SeatLocked";
    public const string SeatUnlocked = "SeatUnlocked";
    public const string AllLocked = "AllLocked";
    public const string AllUnlocked = "AllUnlocked";
    public const string CanvasCleared = "CanvasCleared";
    public const string ClientRegistered = "ClientRegistered";
    public const string ClientDisconnected = "ClientDisconnected";
    public const string ChoiceChanged = "ChoiceChanged";
    public const string SeatRevealed = "SeatRevealed";
    public const string SeatHidden = "SeatHidden";
    public const string JudgeResult = "JudgeResult";
    public const string SeatWritingBlackout = "SeatWritingBlackout";
    public const string AssetsCatalogChanged = "AssetsCatalogChanged";
}
