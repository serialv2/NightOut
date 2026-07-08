using System.Collections;
using System.Windows.Input;

namespace NightOut.Views.Bar.Components;

public partial class RewardCardView : ContentView
{
    public static readonly BindableProperty RewardsProperty =
        BindableProperty.Create(nameof(Rewards), typeof(IEnumerable), typeof(RewardCardView));

    public static readonly BindableProperty HasRewardIntentProperty =
        BindableProperty.Create(nameof(HasRewardIntent), typeof(bool), typeof(RewardCardView), false);

    public static readonly BindableProperty RewardIntentTitleProperty =
        BindableProperty.Create(nameof(RewardIntentTitle), typeof(string), typeof(RewardCardView), string.Empty);

    public static readonly BindableProperty RewardIntentCostLabelProperty =
        BindableProperty.Create(nameof(RewardIntentCostLabel), typeof(string), typeof(RewardCardView), string.Empty);

    public static readonly BindableProperty RewardIntentQrPayloadProperty =
        BindableProperty.Create(nameof(RewardIntentQrPayload), typeof(string), typeof(RewardCardView), string.Empty);

    public static readonly BindableProperty RewardIntentShortCodeProperty =
        BindableProperty.Create(nameof(RewardIntentShortCode), typeof(string), typeof(RewardCardView), string.Empty);

    public static readonly BindableProperty RewardIntentExpiresLabelProperty =
        BindableProperty.Create(nameof(RewardIntentExpiresLabel), typeof(string), typeof(RewardCardView), string.Empty);

    public static readonly BindableProperty PrepareRewardCommandProperty =
        BindableProperty.Create(nameof(PrepareRewardCommand), typeof(ICommand), typeof(RewardCardView));

    public static readonly BindableProperty CloseRewardIntentCommandProperty =
        BindableProperty.Create(nameof(CloseRewardIntentCommand), typeof(ICommand), typeof(RewardCardView));

    public IEnumerable? Rewards { get => (IEnumerable?)GetValue(RewardsProperty); set => SetValue(RewardsProperty, value); }
    public bool HasRewardIntent { get => (bool)GetValue(HasRewardIntentProperty); set => SetValue(HasRewardIntentProperty, value); }
    public string RewardIntentTitle { get => (string)GetValue(RewardIntentTitleProperty); set => SetValue(RewardIntentTitleProperty, value); }
    public string RewardIntentCostLabel { get => (string)GetValue(RewardIntentCostLabelProperty); set => SetValue(RewardIntentCostLabelProperty, value); }
    public string RewardIntentQrPayload { get => (string)GetValue(RewardIntentQrPayloadProperty); set => SetValue(RewardIntentQrPayloadProperty, value); }
    public string RewardIntentShortCode { get => (string)GetValue(RewardIntentShortCodeProperty); set => SetValue(RewardIntentShortCodeProperty, value); }
    public string RewardIntentExpiresLabel { get => (string)GetValue(RewardIntentExpiresLabelProperty); set => SetValue(RewardIntentExpiresLabelProperty, value); }
    public ICommand? PrepareRewardCommand { get => (ICommand?)GetValue(PrepareRewardCommandProperty); set => SetValue(PrepareRewardCommandProperty, value); }
    public ICommand? CloseRewardIntentCommand { get => (ICommand?)GetValue(CloseRewardIntentCommandProperty); set => SetValue(CloseRewardIntentCommandProperty, value); }

    public RewardCardView()
    {
        InitializeComponent();
    }
}
