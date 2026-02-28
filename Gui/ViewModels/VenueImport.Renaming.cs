using CommunityToolkit.Mvvm.ComponentModel;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueImport : ObservableObject
{
    private const string NamePlaceholder = "$name";

    [ObservableProperty] public partial bool RenameImport { get; set; }
    [ObservableProperty] public partial string RenameTo { get; set; }
    [ObservableProperty] public partial RequiredRename RenameRequired { get; set; }
    [ObservableProperty] public partial bool DoRename { get; set; }

    partial void OnRenameImportChanged(bool value) => RenameTo = GetRenameTemplate(value);

    private static string GetRenameTemplate(bool renameImport)
        => renameImport ? "$name (imported $now)" : "$name (renamed $now)";

    private void RenameVenue(Venue venue) => venue.Name = GetNewName(venue);

    private string GetNewName(Venue venue)
        => RenameTo
            .Replace(NamePlaceholder, venue.Name)
            .Replace("$today", DateTime.Today.ToString("d MMM"))
            .Replace("$now", DateTime.Now.ToString("d MMM H:mm"));

    // downstream of ApplySameActionToRemainingConflicts, RenameTo and RenameImport
    private IEnumerable<string> GetRenameErrors()
    {
        if (ApplySameActionToRemainingConflicts && !RenameTo.Contains(NamePlaceholder))
            yield return "If you want to apply the same renaming strategy to remaining conflicts, use the "
                + NamePlaceholder + " placeholder somewhere in the template to avoid unrecognizable names.";

        string newName = RenameImport ? GetNewName(validationContext!.Value.import) : GetNewName(validationContext!.Value.local);
        var hasConflicts = validationContext!.Value.existing.Any(v => v != validationContext!.Value.local && v.Name == newName);
        if (hasConflicts) yield return $"There already is a different venue called \"{newName}\" in your configs.";
    }

    private void DetermineRequiredRenaming()
    {
        var importName = validationContext!.Value.import.Name;
        var nameConflicts = validationContext!.Value.existing.Where(v => v.Name == importName).ToArray();

        RenameRequired = nameConflicts.Length == 0 ? RequiredRename.None
            : nameConflicts.Length == 1 && nameConflicts[0] == validationContext!.Value.local ? RequiredRename.LocalOrImport
            : RequiredRename.Import;

        DoRename = DoRename || RenameRequired != RequiredRename.None;
    }

    public enum RequiredRename { None, LocalOrImport, Import }

    public partial class Page
    {
        private static (HorizontalStackLayout renamed, HorizontalStackLayout renameTo, Label renameInfo) RenamingControls()
        {
            var doRename = LbldChck("rename", nameof(DoRename)).Wrapper
                .BindVisible(nameof(RenameRequired),
                    converter: Converters.Predicate<RequiredRename>(rr => rr == RequiredRename.None));

            var renamed = HStack(5, doRename,
                new RadioButton { Content = "yours", Value = false }.BindVisible(nameof(DoRename)),
                new RadioButton { Content = "import", Value = true }.BindVisible(nameof(DoRename)))
                .BindRadioButtonGroupSelectedValue(nameof(RenameImport))
                .BindVisible(nameof(SelectedAction),
                    converter: Converters.Predicate<Actions>(action => action == Actions.KeepBoth));

            var renameInfo = Info("You can use the placeholders \"$name\" (the current)," +
                " \"$today\" or \"$now\" and apply the same template to other conflicts.");

            BindVisibleToKeepingBothAndRenaming(renameInfo);
            var renameTo = HStack(5, Lbl("to"), Entr(nameof(RenameTo)));
            BindVisibleToKeepingBothAndRenaming(renameTo);
            return (renamed, renameTo, renameInfo);
        }

        private static void BindVisibleToKeepingBothAndRenaming<T>(T renameTo) where T : VisualElement
            => renameTo.BindVisible(new Binding(nameof(SelectedAction)), new Binding(nameof(DoRename)),
                predicate: ((Actions? action, bool? doRename) values) => values.doRename == true && values.action == Actions.KeepBoth);
    }
}
