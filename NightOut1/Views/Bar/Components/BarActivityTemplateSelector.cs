using NightOut.Models;

namespace NightOut.Views.Bar.Components;

public class BarActivityTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? VideoTemplate { get; set; }
    public DataTemplate? CheckinTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is BarActivityItem activity)
        {
            if (activity.IsVideo && VideoTemplate is not null)
                return VideoTemplate;

            if (activity.IsCheckin && CheckinTemplate is not null)
                return CheckinTemplate;
        }

        return DefaultTemplate ?? new DataTemplate(() => new Label { Text = string.Empty });
    }
}
