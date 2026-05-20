using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

partial class ScrapeJobEditor
{
    // ScrapeJob proxy properties
    public string? Closest
    {
        get => ScrapeJob.Closest;
        set
        {
            if (ScrapeJob.Closest == value) return;
            ScrapeJob.Closest = value;
            OnPropertyChanged();
        }
    }

    public string? Selector
    {
        get => ScrapeJob.Selector;
        set
        {
            if (ScrapeJob.Selector == value) return;
            ScrapeJob.Selector = value;
            OnPropertyChanged();
            GuessDateFormat();
        }
    }

    public bool IgnoreNestedText
    {
        get => ScrapeJob.IgnoreNestedText;
        set
        {
            if (ScrapeJob.IgnoreNestedText == value) return;
            ScrapeJob.IgnoreNestedText = value;
            OnPropertyChanged();
            GuessDateFormat();
        }
    }

    public string? Attribute
    {
        get => ScrapeJob.Attribute;
        set
        {
            if (ScrapeJob.Attribute == value) return;
            ScrapeJob.Attribute = value;
            OnPropertyChanged();
            GuessDateFormat();
        }
    }

    public string? Replace
    {
        get => ScrapeJob.Replace;
        set
        {
            if (ScrapeJob.Replace == value) return;
            ScrapeJob.Replace = value;
            OnPropertyChanged();
            GuessDateFormat();
        }
    }

    public string? Match
    {
        get => ScrapeJob.Match;
        set
        {
            if (ScrapeJob.Match == value) return;
            ScrapeJob.Match = value;
            OnPropertyChanged();
            GuessDateFormat();
        }
    }

    public string? Comment
    {
        get => ScrapeJob.Comment;
        set
        {
            if (ScrapeJob.Comment == value) return;
            ScrapeJob.Comment = value;
            OnPropertyChanged();
        }
    }

    // DateScrapeJob proxies
    public string? Format
    {
        /* No need to handle model.scrapeJob being initialized lazily.
         * We currently only have one DateScrapeJob and it is required i.e. initialized. */
        get => DateScrapeJob!.Format;
        set
        {
            if (DateScrapeJob!.Format == value) return;
            DateScrapeJob.UpdateFormat(value);
            OnPropertyChanged();
        }
    }

    public string Culture
    {
        get => DateScrapeJob!.Culture;
        set
        {
            if (DateScrapeJob!.Culture == value) return;
            DateScrapeJob.Culture = value;
            OnPropertyChanged();
        }
    }
}
