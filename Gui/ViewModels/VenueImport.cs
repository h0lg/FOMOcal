using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueImport : ObservableObject
{
    [ObservableProperty] public partial string? Conflict { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ActionHint))]
    public partial Actions SelectedAction { get; set; } = Actions.Merge;

    public string ActionHint => SelectedAction switch
    {
        Actions.ReplaceLocal => "Completely replace your current config with the update."
            + " This is the right choice if you haven't changed this venue config yourself and trust the import source.",
        Actions.Merge => "Decide for each config difference whether to keep your current value or replace it from the update.",
        Actions.KeepBoth => "Keep your current config and also save the import, renaming either"
            + " - if necessary to avoid conflicts. With this option you can try both configs side by side.",
        Actions.SkipImport => "Skip this venue and keep your config untouched.",
        _ => string.Empty
    };

    [ObservableProperty] public partial bool ApplySameActionToRemainingConflicts { get; set; }
    public ObservableCollection<string> ValidationErrors { get; } = [];

    private TaskCompletionSource? solvingConflict;

    internal VenueImport()
    {
        RenameTo = GetRenameTemplate(RenameImport);

        PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(SelectedAction)
                || e.PropertyName == nameof(RenameTo)
                || e.PropertyName == nameof(RenameImport)
                || e.PropertyName == nameof(ApplySameActionToRemainingConflicts))
                Revalidate();
        };
    }

    internal async Task<Dictionary<string, string>> ImportAsync(
        Collection<Venue> existing, HashSet<Venue> imported, HashSet<Event> events)
    {
        Dictionary<string, string> renames = [];

        foreach (var import in imported)
        {
            var local = existing.SingleOrDefault(v => v.ProgramUrl == import.ProgramUrl);

            if (local != null)
            {
                var diffs = ObjectDiffer.Diff(local, import, propertiesNotDiffed);
                if (diffs.Count == 0) continue; // safe to skip import
                validationContext = (existing, import, local);
                DetermineRequiredRenaming();
                Revalidate();

                if (!ApplySameActionToRemainingConflicts || !CanContinue())
                {
                    solvingConflict = new();
                    Conflict = "There's an update for " + local.Name;
                    ScrapeStatsDiffs.Clear();
                    Diffs.Clear();

                    foreach (var prop in scrapeStatsProperties)
                        ScrapeStatsDiffs.Add(new PropertyDiff([prop], prop.GetValue(local), prop.GetValue(import)));

                    foreach (var diff in diffs)
                        Diffs.Add(new DiffEditor(diff, [.. events.Where(e => e.Venue == local.Name).Take(5)]));

                    await solvingConflict.Task;
                }

                switch (SelectedAction)
                {
                    case Actions.ReplaceLocal:
                        existing.Remove(local);
                        existing.Add(import);
                        import.LastRefreshed = null;
                        import.LastEventCount = null;
                        import.SaveScrapeLogs = false;

                        if (local.Name != import.Name)
                            renames.Add(local.Name, import.Name);
                        break;

                    case Actions.Merge:
                        var original = local.Name;

                        foreach (var diffEditor in Diffs)
                            diffEditor.Update(local);

                        if (original != local.Name)
                            renames.Add(original, local.Name);
                        break;

                    case Actions.KeepBoth:
                        if (DoRename)
                        {
                            if (RenameImport) RenameVenue(import);
                            else
                            {
                                var before = local.Name;
                                RenameVenue(local);
                                renames.Add(before, local.Name);
                            }
                        }

                        existing.Add(import);
                        import.LastRefreshed = null;
                        import.LastEventCount = null;
                        import.SaveScrapeLogs = false;
                        break;
                }
            }
            else existing.Add(import);
        }

        return renames;
    }

    private (Collection<Venue> existing, Venue import, Venue local)? validationContext;

    private void Revalidate()
    {
        if (validationContext == null) return;

        ValidationErrors.Clear();

        if (SelectedAction == Actions.KeepBoth)
        {
            foreach (var error in GetRenameErrors())
                ValidationErrors.Add(error);
        }

        ContinueCommand.NotifyCanExecuteChanged();
    }

    private bool CanContinue() => ValidationErrors.Count == 0;

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue() => solvingConflict?.SetResult();

    public enum Actions { ReplaceLocal, Merge, KeepBoth, SkipImport }

    public partial class Page : ContentPage
    {
        internal Page(VenueImport model)
        {
            BindingContext = model;
            Title = "Importing venues...";

            // for more focus, prevent navigation in Shell during import
            if (Shell.Current != null) Shell.SetTabBarIsVisible(this, false);

            var actions = HStack(0,
                ActionOption(Actions.ReplaceLocal, "import"),
                ActionOption(Actions.Merge, "merged"),
                ActionOption(Actions.KeepBoth, "both"),
                ActionOption(Actions.SkipImport, "yours"));

            actions.BindRadioButtonGroupSelectedValue(nameof(SelectedAction));

            var sameForRemainingConflicts = LbldView("do the same for remaining conflicts", Check(nameof(ApplySameActionToRemainingConflicts)))
                .Wrapper.BindVisible(nameof(SelectedAction), converter: Converters.Predicate<Actions>(action => action != Actions.Merge));

            DataTemplate validationErrorTemplate = new(() => new Border()
            {
                StyleClass = [Styles.Border.Error],
                Content = BndLbl().Wrap()
            });

            var validationErrors = new CollectionView
            {
                ItemTemplate = validationErrorTemplate,
                ItemsSource = model.ValidationErrors,
                ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 5 }
            };

            Button continueButton = Btn("Continue", nameof(ContinueCommand));
            CollectionView scrapeStatsDiffs = BuildScrapeStatsDiffs(model);
            var (relevantDiffs, mergeInfo, lastScrapedInfo, diffs) = BuildMergeUi(model);
            var (renamed, renameTo, renameInfo) = RenamingControls();

            var layout = VStack(5,
                BndLbl(nameof(Conflict)).StyleClass(Styles.Label.Headline),
                Info("showing differences between the incoming \"import\" and your current config (\"yours\")"),
                scrapeStatsDiffs.CenterHorizontal(),
                Lbl("Which version do you want to keep?").StyleClass(Styles.Label.SubHeadline),
                actions.CenterHorizontal(),
                BndLbl(nameof(ActionHint)).StyleClass(Styles.Label.Demoted).Wrap().Padding(5).CenterHorizontal(),
                relevantDiffs,
                mergeInfo,
                lastScrapedInfo,
                diffs.CenterHorizontal(),
                renamed.CenterHorizontal(),
                renameTo.CenterHorizontal(),
                renameInfo.CenterHorizontal(),
                sameForRemainingConflicts.CenterHorizontal(),
                validationErrors.CenterHorizontal(),
                continueButton.CenterHorizontal());

            Content = new ScrollView { Content = layout.Padding(5) };
        }

        private static RadioButton ActionOption(Actions action, string text)
            => new() { Value = action, Content = text, StyleClass = [Styles.RadioButton.SingleSelectToggleButton] };

        private static Label Info(string text) => Lbl(text).StyleClass(Styles.Label.Demoted).Wrap().Padding(5).CenterHorizontal();
    }
}
