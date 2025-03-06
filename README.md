# 😱📅 FOMOcal - discover & share local events

Tired of missing great shows? **FOMOcal** helps you stay on top of your local music scene by creating tailored event calendars based on venue listings.

🎵 Discover events that match your taste.
📅 Export them to your calendar.
🤝 Share with friends to sync up.

## How It Works

1. **Add a Venue**: Tell FOMOcal where to look! Configure how events can be [scraped](https://en.wikipedia.org/wiki/Web_scraping) from a venue’s program page.

   - Select the **event containers** (the boxes that hold event details).
   - From within, select and extract event details like name, date, and time.
   - A **Scrape Job** consists of:
     - A [**CSS Selector**](https://en.wikipedia.org/wiki/CSS) to find the right element.
     - Whether to **ignore nested text** (for cases where text from [nested elements](https://en.wikipedia.org/wiki/HTML_element) should be excluded).
     - An optional [**attribute**](https://en.wikipedia.org/wiki/HTML_attribute) to extract from the selected element.
     - An optional [**RegEx Match**](https://en.wikipedia.org/wiki/Regular_expression) to 🔍 fine-tune the extracted text.
     - Only for the event date: A **[format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings) & [culture](https://en.wikipedia.org/wiki/Language_code)** for parsing it correctly.

2. **Scrape the events** from the venue's program page.

3. **Filter the list** by event details. Only interested in rock concerts? Apply filters to keep only what you care about.

4. **Select the events** you want to attend or share.

5. **Export your selection** as an [📆 iCalendar (.ics)](https://en.wikipedia.org/wiki/ICalendar) file for your calendar app or as a [📊 CSV Data Export](https://en.wikipedia.org/wiki/Comma-separated_values) file.
    Share your venue configs with friends so they can pull the same listings.

## What Do I Need?

To run FOMOcal, make sure you have [.NET 9 Runtime](https://dotnet.microsoft.com/) installed.
