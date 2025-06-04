# FOMOcal - discover & share local events

<img src="./Gui/Resources/AppIcon/appicon.svg" align="right" height="50"
    title="😱📅" />

Tired of missing great shows? **FOMOcal** helps you stay on top of your local music scene by creating tailored event calendars based on venue listings.

🎵 Discover events that match your taste.
📅 Export them to your calendar.
🤝 Share with friends to sync up.


## How it works

1. ➕ **Add a Venue**: Tell FOMOcal where to look! Configure how events can be
   [scraped](https://en.wikipedia.org/wiki/Web_scraping) from a venue’s program page.

   - Select the **event containers** (the boxes holding event details).
   - Relative to an event container, define [Scrape Jobs](#scrape-job) to select and extract different [event details](#event-details).

2. ⛏ **Scrape the events** from the venue's program page.

3. 🔍 **Filter the list** by event details. Only interested in rock concerts? Apply filters to keep only what you care about.

4. ✨ **Select the events** you want to attend or share.

5. 🥡 **Export your selection** as an [📆 iCalendar (.ics)](https://en.wikipedia.org/wiki/ICalendar) file for your calendar app or as a [📊 CSV Data Export](https://en.wikipedia.org/wiki/Comma-separated_values) file.
    Share your venue configs with friends so they can pull the same listings.

### Event details
❗ *Name* and 📆 *Date* are **required**.

**Optional** details include ‼ *SubTitle*, 📜 *Description*, 🎶 *Genres*,
the 🏛 *Stage*, 🚪 *Doors* and 🎼 *Start* times, 💳 *Pre-sale* and 💵 *Doors price*,
an 📰 *Event page* and 🎫 *Tickets* links 📡 - and an 🖼 *Image*.

### Scrape Job
Every [event detail](#event-details) is represented as a *Scrape Job*, in which you tell FOMOcal how to
- 🥢 [Pick the element](#-selecting-elements) holding the text to extract - from within the event container by default,
though selecting from the outside is possible
- ⚗ [Extract text from it](#-extracting-text)
- 🧹 [Clean it up](#-cleaning-up-extracted-text) and
- ♻ [Convert it into a different type](#-converting-extracted-text) if necessary.

### 🥢 Selecting elements
Tell FOMOcal which element to pick using either a [CSS](https://en.wikipedia.org/wiki/CSS#Selector)
or [XPath](https://en.wikipedia.org/wiki/XPath#Syntax_and_semantics_(XPath_1.0))
selector.

What's the difference? In most cases you'll want to use
[CSS](https://www.w3schools.com/css/css_selectors.asp) for its shorter and simpler selector syntax.
For advanced scenarios you may want to try [XPath](https://www.w3schools.com/xml/xpath_syntax.asp) -
which has more powerful functions, e.g. for filtering.

Sounds hard? It probably will be the first few times you do this.
But fear not! FOMOcal comes with the tools to help you along every step of the way.
Most of the time, you shouldn't even have to type anything.

First off, you pick the element **visually** like from the
[inspector](https://developer.mozilla.org/en-US/docs/Learn_web_development/Howto/Tools_and_setup/What_are_browser_developer_tools#the_inspector_dom_explorer_and_css_editor)
in your browser's developer tools.
> You can open the inspector from the right-click context menu of a web page in most browsers via
an item called *Inspect element* or similar - and are encouraged to do so to
[debug your selectors](#debugging-selectors).

Once you've picked an element, FOMOcal guides you through the choice of selector syntax and -detail
via **contextual hints** including helpful links that give you enough background to make an informed decision.

Having chosen a selctor, an **event detail preview** will display either what it matches
or which errors occured for a configurable range of events on the page you're scraping.

#### Debugging selectors

To test selectors, load the page in your browser and start up a
[developer console](https://developer.mozilla.org/en-US/docs/Learn_web_development/Howto/Tools_and_setup/What_are_browser_developer_tools#the_javascript_console).
In there, use [`document.querySelectorAll('.css-selector')`](https://www.w3schools.com/jsref/met_document_queryselectorall.asp)
or [`document.evaluate('//xpath/selector', document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null).snapshotLength`](https://developer.mozilla.org/en-US/docs/Web/API/Document/evaluate)
depending on your chosen selector syntax.

### ⚗ Extracting text
You can get the text of a [selected element](#-selecting-elements) from either
- an [**Attribute**](https://en.wikipedia.org/wiki/HTML_attribute) you specify,
- the full text content (including that of child nodes) by default - or
- the text content of only the selected node **ignoring nested text**, i.e. without text of
  [nested nodes](https://en.wikipedia.org/wiki/Document_Object_Model#DOM_tree_structure) -
  for when those contain noise.

### 🧹 Cleaning up extracted text
Not every event detail is cleanly selectable.
Sometimes you have no choice but to extract text that contains noise like labels or other details
or structures one detail in a weird way.
For these occasions, a *Scrape Job* offers some options to clean it up.
> Note that for most *Scrape Jobs*, this is not necessary - if you don't care about the noise in the event list.
These settings are mainly required for the 📆 *Date* job, which needs text in a very specific format - or for program pages that
render different event details into the same element.

#### Normalizing whitespace

Before further processing, extra spaces, tabs and line breaks are replaced so there's only one space between words.
> This makes later [replacements](#replacements) and [matching](#regex-match) easier, but has the disadvantage that meaningful line breaks from multi-line texts
like the 📜 *Description* are lost. So this may change if a better solution comes up.

#### Replacements
Sometimes you have to replace parts of the extracted text.
E.g. if a program page uses a non-standard name for a particular month that you want to parse a date from.

For these occasions, you can specify a comma-separated list of replacements in the form
`Pattern => Replacement, Pattern2 =>` with every `Pattern` being plain text or a
[Regular Expression](https://en.wikipedia.org/wiki/Regular_expression) in .NET flavor that matches the part of the extracted text
you want to swap out with the `Replacement`.

Note that the `Replacement` may be left empty to remove the matched part -
which may be easier than [writing a RegEx to match](#regex-match) the part you want to keep.

#### RegEx Match
An optional [Regular Expression](https://en.wikipedia.org/wiki/Regular_expression) in .NET flavor
that matches the part of the extracted text you're interested in.

> [regex101](https://regex101.com/) is great to debug your RegEx, learn and find existing patterns. If you can't be bothered or are struggling - ask a chat bot for help, they're pretty good at this.

### ♻ Converting extracted text
Event details may be of a data type other than text.
Currently, the only one where this matters and is used is the event 📆 *Date*.

> This is necessary because FOMOcal uses it to sort the event list and decide which events are past and hide them.
For that purpose, the text representations different program pages use, need to be converted into a `date` type
to be comparable to other `date`s. Otherwise, only alphabetic sorting would be supported -
and you wouldn't like the result of that when mixing formats from different pages like `Sa, 26. April` and `26. April`.

To parse a `date` from text, FOMOcal needs to know the exact
.NET [custom](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings)
or [standard](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings)
date format and [culture](https://en.wikipedia.org/wiki/Language_code) to convert it correctly.


## What do I need?

To run FOMOcal, make sure you have [.NET 9 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime) installed.
On Windows 10+, chances are you have it already. Try installing it if FOMOcal doesn't run without.

Look for the latest build for your Operating System in the Assets of the [latest release](https://github.com/h0lg/FOMOcal/releases/latest).
E.g. If you're on Windows 10 or higher, try the `*.win10-x64.zip` download.

## What's this about?

> TL;DR: Supporting small venues & upcoming bands by enabling easy grass-roots promotion.

Smaller venues hosting concerts often lack the professional promotion - or the social media guru -
to spread their events where they're easily discovered.
Instead, many of them **rely on their patrons visiting their web page**
to find out about the upcoming program like we did in the golden noughties.

Yet **your local music pub is vital to the diversity of the music ecosystem:
Lesser known bands depend on it** for a chance to play a gig - because for them,
established venues are often out-priced or simply not interested.
That's why it's not uncommon for **smaller locations** to **carry the interesting fringes of the music scene**.

### It's a team effort

This app is **intended for you, the patron of the fringe side of music** - to pick up the torch 🔥 and carry it for a little.
Lend those cool little local spots a hand by promoting their events - that way keeping them and your scene alive,
bringing people together for some good music and hopefully more great bands into your town in the future.

With FOMOcal you can support different scenes by **sharing relevant venues and their events in easily digestable formats**.
Go figure out how to get event info from the web page of a concert location and share it.
Or curate a list of shows over the next few months for the fine lads and lasses yourself.
FOMOcal assumes you have no clue how web pages are built and tries to give you the tools -
and enough help for them - to let you succeed anyway.
