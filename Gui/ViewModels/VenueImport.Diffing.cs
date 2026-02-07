using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueImport : ObservableObject
{
    private static readonly string[] scrapeStats = [nameof(Venue.LastRefreshed), nameof(Venue.LastEventCount)];

    private static readonly PropertyInfo[] scrapeStatsProperties = [.. typeof(Venue)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => scrapeStats.Contains(p.Name))];

    private static readonly ILookup<Type, string> propertiesNotDiffed =
        scrapeStats.Concat([nameof(Venue.ProgramUrl), nameof(Venue.SaveScrapeLogs)])
            .ToLookup(_ => typeof(Venue), n => n);

    public ObservableCollection<PropertyDiff> ScrapeStatsDiffs { get; } = [];
    public ObservableCollection<DiffEditor> Diffs { get; } = [];

    public partial class DiffEditor : ObservableObject
    {
        private readonly PropertyDiff diff;
        private readonly string diffingPropertyName;

        public string Path { get; }
        public object? OldValue => diff.OldValue;
        public object? NewValue => diff.NewValue;
        public string?[]? LastScrapedValues { get; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(OtherValue))]
        public partial bool UseNewValue { get; set; } = true;

        public bool CanPutOtherValueInComment { get; }
        public string OtherValue => UseNewValue ? "yours" : "import";
        public bool PutOtherValueInComment { get; set; } = true;

        internal DiffEditor(PropertyDiff diff, Event[] events)
        {
            this.diff = diff;
            var path = diff.Path;
            diffingPropertyName = path[^1].Name;
            Path = diff.DisplayPath;
            CanPutOtherValueInComment = diffingPropertyName != nameof(IHaveAComment.Comment);
            if (path.Count < 1) return;
            var first = path[0];
            if (first.Name != nameof(Venue.Event)) return;
            var fieldName = path[1].Name;
            var field = Event.Fields.SingleOrDefault(p => p.Name == fieldName);
            if (field == null) return;
            LastScrapedValues = [.. events.Select(e => field.GetValue(e)?.ToString())];
        }

        internal void Update(Venue venue)
        {
            bool setValue = false;
            object? value = null;
            string? otherValueForComment = null;

            if (UseNewValue)
            {
                setValue = true;
                value = NewValue;

                if (CanPutOtherValueInComment && PutOtherValueInComment)
                    otherValueForComment = $"{diffingPropertyName} before import on {DateTime.Now:d MMM H:mm} : {OldValue}";
            }
            else if (CanPutOtherValueInComment && PutOtherValueInComment)
                otherValueForComment = $"{diffingPropertyName} imported on {DateTime.Now:d MMM H:mm} : {NewValue}";

            object owner = venue; // the property owner, changes as we navigate the path
            var lastIndex = diff.Path.Count - 1;

            for (int i = 0; i <= lastIndex; i++)
            {
                var property = diff.Path[i];

                // update comment, which usually lives on the same owner as the diffing property
                if (otherValueForComment != null && i == lastIndex)
                {
                    // except for Venue properties - put comments for them into Venue.Event.Comment
                    object commentOwner = lastIndex == 0 ? venue.Event : owner;
                    var comment = commentOwner.GetType().GetProperty(nameof(IHaveAComment.Comment));

                    if (comment != null)
                    {
                        var commentValue = comment.GetValue(commentOwner) as string;
                        commentValue += (commentValue.IsSignificant() ? " | " : null) + otherValueForComment;
                        comment.SetValue(commentOwner, commentValue.Trim());
                    }
                    else throw new MissingMemberException("no comment property on " + property.DeclaringType?.FullName);
                }

                // not the last property in the path, reset owner and continue
                if (i < lastIndex)
                {
                    var nextOwner = property.GetValue(owner);

                    if (nextOwner == null) // importing a new scrape job
                    {
                        nextOwner = Activator.CreateInstance(property.PropertyType); // create it
                        property.SetValue(owner, nextOwner); // set it on the owner
                    } // because continuing with a null owner wouldn't work

                    owner = nextOwner!;
                }
                // end of path; update diffing property if required
                else if (setValue) property.SetValue(owner, value);
            }
        }
    }

    public partial class Page
    {
        private static CollectionView BuildScrapeStatsDiffs(VenueImport model)
        {
            DataTemplate scrapeStatsDiffTemplate = new(() =>
            {
                var layout = Grd(cols: [Auto, Star], rows: [Auto, Auto, Auto], spacing: 5,
                    BndLbl(nameof(PropertyDiff.DisplayPath)).ColumnSpan(2),
                    Lbl("import").StyleClass(Styles.Label.Demoted).Row(1),
                    BndLbl(nameof(PropertyDiff.NewValue)).Row(1).Column(1),
                    Lbl("yours").StyleClass(Styles.Label.Demoted).Row(2),
                    BndLbl(nameof(PropertyDiff.OldValue)).Row(2).Column(1));

                return new Border { StyleClass = [Styles.Border.RoundedSection], Content = layout };
            });

            return new CollectionView
            {
                ItemsSource = model.ScrapeStatsDiffs,
                ItemTemplate = scrapeStatsDiffTemplate,
                ItemsLayout = LinearItemsLayout.Horizontal
            };
        }

        private static (Label relevantDiffs, Label mergeInfo, Label lastScrapedInfo, CollectionView diffs) BuildMergeUi(VenueImport model)
        {
            var ifMerge = Converters.Predicate<Actions>(action => action == Actions.Merge);

            var relevantDiffs = Lbl("Relevant differences").StyleClass(Styles.Label.SubHeadline)
                .BindVisible(nameof(SelectedAction), converter: ifMerge);

            var mergeInfo = Info("Choose the value to ◉ use in your future config" +
                $" - and whether to store the other in the {Glyphs.Comment} comment" +
                " of the corresponding config field, from where you can copy and use it.")
                .BindVisible(nameof(SelectedAction), converter: ifMerge);

            var lastScrapedInfo = Info($"Samples of the last {Glyphs.Scrape} scraped values for your current config"
                + " are displayed to give you a reference and identify possible problems with it.")
                .BindVisible(nameof(SelectedAction), converter: ifMerge);

            DataTemplate lastScrapedValueTemplate = new(() => new Border()
            {
                StyleClass = [Styles.Border.ScrapedValue],
                Content = BndLbl().Wrap()
            });

            DataTemplate diffTemplate = new(() =>
            {
                var lastScraped = new CollectionView()
                {
                    ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Horizontal) { ItemSpacing = 5 },
                    ItemTemplate = lastScrapedValueTemplate,
                    VerticalOptions = LayoutOptions.Center
                }.Bind(ItemsView.ItemsSourceProperty, nameof(DiffEditor.LastScrapedValues));

                var layout = Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Auto, Auto, Auto], spacing: 5,
                    BndLbl(nameof(DiffEditor.Path)).CenterVertical().ColumnSpan(2),
                    BndLbl(nameof(DiffEditor.OtherValue), "{0} to " + Glyphs.Comment).CenterVertical()
                        .BindVisible(nameof(DiffEditor.CanPutOtherValueInComment)).Column(2),
                    Check(nameof(DiffEditor.PutOtherValueInComment)).CenterVertical()
                        .BindVisible(nameof(DiffEditor.CanPutOtherValueInComment)).Column(3),
                    ValueLabel("import").Row(1),
                    Radio(nameof(DiffEditor.NewValue), true).Row(1).Column(1).ColumnSpan(3),
                    ValueLabel("yours").Row(2),
                    Radio(nameof(DiffEditor.OldValue), false).Row(2).Column(1).ColumnSpan(3),
                    ValueLabel($"last {Glyphs.Scrape}").Row(3),
                    lastScraped.Row(3).Column(1).ColumnSpan(3));

                layout.BindRadioButtonGroupSelectedValue(nameof(DiffEditor.UseNewValue),
                    // bind group name from unique string property to avoid sharing the same group across diffs
                    pathToGroupName: nameof(DiffEditor.Path));

                return new Border { StyleClass = [Styles.Border.RoundedSection], Content = layout };
            });

            var diffs = new CollectionView
            {
                ItemsSource = model.Diffs,
                ItemTemplate = diffTemplate,
                ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 5 }
            }.BindVisible(nameof(SelectedAction), converter: ifMerge);

            return (relevantDiffs, mergeInfo, lastScrapedInfo, diffs);
        }

        private static RadioButton Radio(string contentPropertyPath, bool value)
            => new RadioButton() { Value = value }.Bind(RadioButton.ContentProperty, contentPropertyPath).CenterVertical();

        private static Label ValueLabel(string text) => Lbl(text).StyleClass(Styles.Label.Demoted).TextCenterVertical();
    }
}
