using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NightOut.Services;

public sealed class RewardQrScannedMessage(string value) : ValueChangedMessage<string>(value);
